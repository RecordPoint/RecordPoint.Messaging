using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessagePump : IMessagePump
    {
        private AzureServiceBusSettings _settings;
        private MessageReceiver _messageReceiver;
        private IMessageHandlerFactory _messageHandlerFactory;
        private MessagingFactory _messagingFactory;
        private ServiceBusConnection _serviceBusConnection;
        private string _inputQueue;
        private QueueDescription _queueDescription;

        public MessagePump(AzureServiceBusSettings settings,
            IMessageHandlerFactory messageHandlerFactory,
            ServiceBusConnection connection,
            MessagingFactory messagingFactory,
            string inputQueue)
        {
            _settings = settings;
            _messageHandlerFactory = messageHandlerFactory;
            _messagingFactory = messagingFactory;
            _serviceBusConnection = connection;
            _inputQueue = inputQueue;

            // The RetryPolicy.Default supplied to the receiver uses an exponential backoff 
            // for transient failures. 
            _messageReceiver = new MessageReceiver(connection, inputQueue,
                ReceiveMode.PeekLock, RetryPolicy.Default);
        }

        public async Task Start()
        {
            var managementClient = new ManagementClient(_settings.ServiceBusConnectionString);
            _queueDescription = await managementClient.GetQueueAsync(_inputQueue).ConfigureAwait(false);
            
            _messageReceiver.PrefetchCount = _settings.PrefetchCount;
            _messageReceiver.RegisterMessageHandler(MessageHandler,
               new MessageHandlerOptions(ExceptionHandler)
               {
                   AutoComplete = _settings.AutoComplete,
                   MaxConcurrentCalls = _settings.MaxDegreeOfParallelism
               });
        }

        public async Task Stop()
        {
            await _messageReceiver.CloseAsync().ConfigureAwait(false);
        }

        private async Task MessageHandler(Message message, CancellationToken ct)
        {
            // First validate the RPContextId. If it doesn't match the expected value, this message isn't intended
            // for us and we should return it to the queue immediately. 
            if (message.UserProperties.TryGetValue(Constants.HeaderKeys.RPContextId, out var contextId))
            {
                if (contextId.ToString() != _settings.ContextId)
                {
                    await _messageReceiver.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                    return;
                }
            }

            // Handle deferral control messages
            if (message.UserProperties.TryGetValue(Constants.HeaderKeys.RPDeferredMessageSequenceNumber, out var sequenceNumber))
            {
                var sequenceNumberLong = long.Parse(sequenceNumber.ToString());
                Message deferredMessage = null;
                try
                {
                    deferredMessage = await _messageReceiver.ReceiveDeferredMessageAsync(sequenceNumberLong).ConfigureAwait(false);
                }
                catch (MessageNotFoundException)
                {
                }
                
                if (deferredMessage != null)
                {
                    await InnerMessageHandler(deferredMessage, message, ct).ConfigureAwait(false);
                }
                else
                {
                    // If the corresponding deferred message couldn't be found, dead-letter the control message
                    await _messageReceiver.DeadLetterAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                }
                return;
            }

            await InnerMessageHandler(message, null, ct).ConfigureAwait(false);
        }

        internal static void GetDeadLetterFields(Exception ex, out string deadLetterReason, out string deadLetterDescription)
        {
            if (ex is AggregateException agex)
            {
                ex = agex.Flatten().InnerException;
            }

            deadLetterReason = ex.GetType().FullName + ": " + ex.Message;
            deadLetterDescription = ex.ToString();

            // Restrict the length of these fields to 1024 chars.
            deadLetterReason = deadLetterReason.Substring(0, Math.Min(deadLetterReason.Length, 1024));
            deadLetterDescription = deadLetterDescription.Substring(0, Math.Min(deadLetterDescription.Length, 1024));
        }

        private async Task InnerMessageHandler(Message message, Message controlMessage, CancellationToken ct)
        {
            byte[] messageBody = null;

            // Check if this is a large message that we need to download the payload for
            if (message.UserProperties.TryGetValue(Constants.HeaderKeys.RPBlobSizeBytes, out var blobSizeBytes))
            {
                messageBody = await DownloadMessageBodyFromBlob(message.MessageId, long.Parse(blobSizeBytes.ToString())).ConfigureAwait(false);
            }
            else
            {
                messageBody = message.Body;
            }

            // Deserialize
            if (message.UserProperties.TryGetValue(Constants.HeaderKeys.RPMessageType, out var assemblyQualifiedNameObject))
            {
                var type = Type.GetType(assemblyQualifiedNameObject.ToString());

                IMessageHandler handler = _messageHandlerFactory.CreateMessageHandler(type);

                object deserializedMessage;
                try
                {
                    var messageString = Encoding.UTF8.GetString(messageBody);
                    deserializedMessage = JsonConvert.DeserializeObject(messageString, type);
                }
                catch (Exception ex)
                {
                    await _messageReceiver.DeadLetterAsync(message.SystemProperties.LockToken,
                        $"Error deserializing message body: {ex}")
                        .ConfigureAwait(false);
                    return;
                }

                // Determine if we need to use a transaction for this handler - check the "Transactional" attribute
                // on the handler class
                var useTransaction = true;
                var transactionalAttributes = handler.GetType().GetCustomAttributes(typeof(TransactionalAttribute), true);
                if (transactionalAttributes == null || transactionalAttributes.Length == 0)
                {
                    useTransaction = false;
                }

                var context = new MessageProcessingContext(_settings, _messageReceiver, message, controlMessage, _messagingFactory, useTransaction);

                using (var tx = useTransaction ? new TransactionScope(TransactionScopeAsyncFlowOption.Enabled) : null)
                {
                    try
                    {
                        await handler.HandleMessage(deserializedMessage, context).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // If an exception was thrown on the last attempt, explicitly dead-letter the message.
                        // If we don't do this, ASB will dead letter the message with a DeadLetterReason field 
                        // value of MaxDeliveryCountExceeded, which is not particularly useful for diagnosis. 
                        // Instead, we explicitly dead-letter the message with some details of the exception that
                        // was thrown. 
                        if (message.SystemProperties.DeliveryCount == _queueDescription.MaxDeliveryCount)
                        {
                            var deadLetterReason = "";
                            var deadLetterDescription = "";
                            GetDeadLetterFields(ex, out deadLetterReason, out deadLetterDescription);

                            await context.DeadLetter(deadLetterReason, deadLetterDescription).ConfigureAwait(false);
                        }

                        throw;
                    }

                    if (!context.IsAbandoned)
                    {
                        tx?.Complete();
                    }
                }

                if (context.IsAbandoned)
                {
                    await _messageReceiver.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
                    if (controlMessage != null)
                    {
                        await _messageReceiver.AbandonAsync(controlMessage.SystemProperties.LockToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                await _messageReceiver.DeadLetterAsync(message.SystemProperties.LockToken,
                    $"Message has no type specified in the {Constants.HeaderKeys.RPMessageType} header, cannot deserialize message.")
                    .ConfigureAwait(false);
                return;
            }
        }

        private async Task<byte[]> DownloadMessageBodyFromBlob(string messageId, long messageSizeBytes)
        {
            if (CloudStorageAccount.TryParse(_settings.BlobStorageConnectionString, out var storageAccount))
            {
                byte[] result = new byte[messageSizeBytes];
                var cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var container = cloudBlobClient.GetContainerReference(_settings.BlobStorageContainerName);
                var blob = container.GetBlockBlobReference(messageId);
                await blob.DownloadToByteArrayAsync(result, 0).ConfigureAwait(false);
                return result;
            }
            else
            {
                throw new ApplicationException("Invalid blob storage connection string");
            }
        }

        Task ExceptionHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            return Task.CompletedTask;
            // TODO;
        }
    }
}
