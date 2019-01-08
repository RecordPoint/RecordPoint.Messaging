using Microsoft.Azure.ServiceBus;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Text;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessageSender : IMessageSender
    {
        private readonly AzureServiceBusSettings _settings;
        private readonly Microsoft.Azure.ServiceBus.Core.MessageSender _messageSender;
        private MessageProcessingContext _messageProcessingContext;

        public MessageSender(AzureServiceBusSettings serviceBusSettings, 
            ServiceBusConnection serviceBusConnection,
            MessageProcessingContext context,
            string destinationQueue)
        {
            _messageProcessingContext = context;
            _settings = serviceBusSettings;
            
            // The default retry policy passed in to the sender provides an exponential backoff for transient failures. 
            if (context == null)
            {
                _messageSender = new Microsoft.Azure.ServiceBus.Core.MessageSender(serviceBusConnection, destinationQueue, RetryPolicy.Default);
            }
            else
            {
                var viaQueue = context.GetMessageReceiver().Path;
                if (viaQueue == destinationQueue)
                {
                    _messageSender = new Microsoft.Azure.ServiceBus.Core.MessageSender(serviceBusConnection, destinationQueue, RetryPolicy.Default);
                }
                else
                {
                    _messageSender = new Microsoft.Azure.ServiceBus.Core.MessageSender(serviceBusConnection, destinationQueue, viaQueue, RetryPolicy.Default);
                }
            }            
        }

        public async Task Send<T>(T message, string messageId = null, Guid? tenantId = null, DateTime? scheduleAt = null)
        {
            // serialize
            var serializedMessage = JsonConvert.SerializeObject(message);
            var messageBytes = Encoding.Default.GetBytes(serializedMessage);

            var effectiveMessageId = messageId;
            if (string.IsNullOrEmpty(effectiveMessageId))
            {
                effectiveMessageId = Guid.NewGuid().ToString();
            }

            string messageUri = null;
            // Check the length of the serialized message body, if it is too long, upload it 
            // to blob storage. Subtract 64kb from the max size to allow room for the message header,
            // which is also included in the message size. 
            if (messageBytes.Length > (_settings.MaxMessageSizeKb - 64) * 1024)
            {
                messageUri = await UploadMessageBodyToBlob(effectiveMessageId, messageBytes).ConfigureAwait(false);
            }

            Message brokerMessage = null;
            if (messageUri == null)
            {
                brokerMessage = new Message(messageBytes);
            }
            else
            {
                brokerMessage = new Message();
                brokerMessage.UserProperties.Add(Constants.HeaderKeys.RPBlobSizeBytes, messageBytes.Length);
            }
            
            brokerMessage.MessageId = effectiveMessageId;

            // Set the type so it can be deserialized on receive
            brokerMessage.UserProperties.Add(Constants.HeaderKeys.RPMessageType, message.GetType().AssemblyQualifiedName);

            // set the delivery time if it was specified
            if (scheduleAt != null)
            {
                brokerMessage.ScheduledEnqueueTimeUtc = scheduleAt.Value;
            }

            // set the partition key if present in order to make transactions work
            if (_messageProcessingContext != null)
            {
                var innerContext = _messageProcessingContext as MessageProcessingContext;
                var contextMessage = innerContext.GetMessage();
                brokerMessage.ViaPartitionKey = contextMessage.PartitionKey;
            }
            
            if (!string.IsNullOrEmpty(_settings.ContextId))
            {
                brokerMessage.UserProperties.Add(Constants.HeaderKeys.RPContextId, _settings.ContextId);
            }

            if (tenantId.HasValue)
            {
                brokerMessage.UserProperties.Add(Constants.HeaderKeys.RPTenantId, tenantId.Value);
            }
            // send
            // Send exceptions are handled internally by the _messageSender with a retry policy.
            // exceptions that fail all stages of the retry policy will be thrown out.
            await _messageSender.SendAsync(brokerMessage).ConfigureAwait(false);
        }

        private async Task<string> UploadMessageBodyToBlob(string messageId, byte[] bytes)
        {
            if (CloudStorageAccount.TryParse(_settings.BlobStorageConnectionString, out var storageAccount))
            {
                var cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var container = cloudBlobClient.GetContainerReference(_settings.BlobStorageContainerName);
                var blob = container.GetBlockBlobReference(messageId);
                await blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                return blob.Uri.ToString();
            }
            else
            {
                throw new ApplicationException("Invalid blob storage connection string");
            }
        }
    }
}
