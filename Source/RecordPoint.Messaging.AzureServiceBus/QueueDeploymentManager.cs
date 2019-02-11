using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using RecordPoint.Messaging.AzureServiceBus.Extensions;
using RecordPoint.Messaging.Interfaces;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class QueueDeploymentManager : IMessagingDeploymentManager
    {
        private AzureServiceBusSettings _settings;
        private ManagementClient _client;
        private QueueDescription _queueDescription;

        public QueueDeploymentManager(AzureServiceBusSettings settings, QueueDescription queueDescription)
        {
            _settings = settings;
            //Partitioning, TTL, lock duration, delivery count, etc
            _client = new ManagementClient(_settings.ServiceBusConnectionString);
            _queueDescription = queueDescription;
        }

        private async Task<QueueDescription> GetQueueAsync()
        {
            try
            {
                return await _client.GetQueueAsync(_queueDescription.Path).ConfigureAwait(false);
            }
            catch (MessagingEntityNotFoundException)
            {
                return null;
            }
        }

        public async Task<bool> Install()
        {
            if (_settings.TryGetContainer(out var container))
            {
                await container.CreateIfNotExistsAsync().ConfigureAwait(false);
            }

            var queue = await GetQueueAsync().ConfigureAwait(false);
            if(queue == null)
            {
                try
                {
                    await _client.CreateQueueAsync(_queueDescription).ConfigureAwait(false);
                    return true;
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                    return false;
                }
            }

            return false;
        }

        public async Task<bool> IsEnabled()
        {
            var queue = await _client.GetQueueAsync(_queueDescription.Path).ConfigureAwait(false);
            if(queue != null)
            {
                return queue.Status == EntityStatus.Active;
            }

            return false;
        }

        public async Task<bool> Uninstall()
        {
            if (_settings.TryGetContainer(out var container))
            {
                await container.DeleteIfExistsAsync().ConfigureAwait(false);
            }

            var queue = await GetQueueAsync().ConfigureAwait(false);
            if (queue != null)
            {
                await _client.DeleteQueueAsync(_queueDescription.Path).ConfigureAwait(false);
                return true;
            }

            return false;
        }
    }
}
