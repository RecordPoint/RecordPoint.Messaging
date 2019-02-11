using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Threading.Tasks;
using System.Transactions;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessageProcessingContext : IMessageProcessingContext
    {
        private readonly AzureServiceBusSettings _settings;
        private readonly MessageReceiver _messageReceiver;
        private readonly Message _message;
        private readonly Message _deferralControlMessage;
        private readonly MessagingFactory _messagingFactory;
        private readonly bool _isTransactional;

        public bool IsAbandoned { get; set; } = false;

        public MessageProcessingContext(AzureServiceBusSettings settings,
            MessageReceiver messageReceiver, 
            Message message, 
            Message controlMessage,
            MessagingFactory factory, 
            bool isTransactional)
        {
            _settings = settings;
            _messageReceiver = messageReceiver;
            _message = message;
            _deferralControlMessage = controlMessage;
            _messagingFactory = factory;
            _isTransactional = isTransactional;
        }

        private bool UseTransaction()
        {   
            // We want to use a transaction during Abandon, Complete, DeadLetter 
            // and Defer only when we 
            //   - want to do multiple operations at once
            //   - are not already in a transaction
            // we want to do multiple operations at once when we are
            // dealing with a deferred message, where we need to complete both the
            // control message and the actual message.
            return _deferralControlMessage != null && !_isTransactional;
        }

        public string GetMessageId()
        {
            return _message.MessageId;
        }

        public string GetTenantId()
        {
            return _message.GetTenantId();
        }

        public Task Abandon()
        {
            // Don't abandon right away - the message pump will abandon the message outside of the transaction.
            // There is some odd behaviour in Service Bus when trying to Abandon a message inside a transaction -
            // the abandoned message doesn't show up for processing again for a long time (sometimes several minutes).
            IsAbandoned = true;
            return Task.CompletedTask;
        }

        public async Task Complete()
        {
            using (var tx = UseTransaction() ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null)
            {
                if (_deferralControlMessage != null)
                {
                    await _messageReceiver.CompleteAsync(_deferralControlMessage.SystemProperties.LockToken).ConfigureAwait(false);
                }
                
                await _messageReceiver.CompleteAsync(_message.SystemProperties.LockToken).ConfigureAwait(false);

                tx?.Complete();         
            }

            if (_message.UserProperties.TryGetValue(Constants.HeaderKeys.RPBlobSizeBytes, out var blobSizeBytes))
            {
                await DeleteMessageBodyFromBlobStorage(_message.MessageId).ConfigureAwait(false);
            }
        }

        private async Task DeleteMessageBodyFromBlobStorage(string messageId)
        {
            if (CloudStorageAccount.TryParse(_settings.BlobStorageConnectionString, out var storageAccount))
            {
                var cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var container = cloudBlobClient.GetContainerReference(_settings.BlobStorageContainerName);
                var blob = container.GetBlockBlobReference(messageId);
                await blob.DeleteIfExistsAsync().ConfigureAwait(false);
            }
            else
            {
                throw new ApplicationException("Invalid blob storage connection string");
            }
        }

        public async Task DeadLetter(string reason, string description = null)
        {
            using (var tx = UseTransaction() ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null)
            {
                if (_deferralControlMessage != null)
                {
                    await _messageReceiver.DeadLetterAsync(_deferralControlMessage.SystemProperties.LockToken, reason, description).ConfigureAwait(false);
                }
                await _messageReceiver.DeadLetterAsync(_message.SystemProperties.LockToken, reason, description).ConfigureAwait(false);

                tx?.Complete();
            }
        }

        public Interfaces.IMessageSender CreateTransactionalSender(string destination)
        {
            return _messagingFactory.CreateMessageSender(destination, this);
        }

        internal Message GetMessage()
        {
            return _message;
        }

        internal MessageReceiver GetMessageReceiver()
        {
            return _messageReceiver;
        }

        public async Task Defer(TimeSpan tryAgainIn)
        {
            // Deferral of a message is implemented per the recommendation given here:
            // https://docs.microsoft.com/en-us/azure/service-bus-messaging/message-deferral#message-deferral-apis
            // We defer the message in Azure Service Bus, effectively hiding it indefinitely.
            // We then send a new "control" message to the same queue that contains the sequence number of the deferred message.
            // The control message is scheduled for delivery at a certain time.
            // The message pump will handle the control message by getting the sequence number and using it to obtain the deferred message.
            var sender = new Microsoft.Azure.ServiceBus.Core.MessageSender(_messageReceiver.ServiceBusConnection,
                _messageReceiver.Path, _messageReceiver.RetryPolicy);

            var controlMessage = new Message();
            controlMessage.UserProperties.Add(Constants.HeaderKeys.RPDeferredMessageSequenceNumber, _message.SystemProperties.SequenceNumber);
            controlMessage.UserProperties.Add(Constants.HeaderKeys.RPContextId, _settings.ContextId);
            //Copy the tenant Id
            if (_message.UserProperties.TryGetValue(Constants.HeaderKeys.RPTenantId, out var messageTenantId))
            {
                controlMessage.UserProperties.Add(Constants.HeaderKeys.RPTenantId, messageTenantId);
            }

            controlMessage.ScheduledEnqueueTimeUtc = DateTime.UtcNow.Add(tryAgainIn);
            controlMessage.PartitionKey = _message.PartitionKey; // Ensure the control message goes to the same partition as the underlying message so transactions work

            // We want to use a transaction if we're not already in a transaction scope
            var useTransaction = !_isTransactional;

            using (var tx = useTransaction ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null)
            {
                await sender.SendAsync(controlMessage).ConfigureAwait(false);
                
                if (_deferralControlMessage == null)
                {
                    await _messageReceiver.DeferAsync(_message.SystemProperties.LockToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await _messageReceiver.AbandonAsync(_message.SystemProperties.LockToken)
                        .ConfigureAwait(false);
                }

                // If we are deferring a message that was already deferred, we are sending a new control message, so complete 
                // the current control message.
                if (_deferralControlMessage != null)
                {
                    await _messageReceiver.CompleteAsync(_deferralControlMessage.SystemProperties.LockToken).ConfigureAwait(false);
                }
                
                tx?.Complete();
            }
        }
    }
}
