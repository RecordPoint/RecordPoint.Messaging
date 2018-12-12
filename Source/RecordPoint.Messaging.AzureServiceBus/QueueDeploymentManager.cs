using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using RecordPoint.Messaging.Interfaces;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class QueueDeploymentManager : IMessagingDeploymentManager
    {
        private ManagementClient _client;
        private QueueDescription _queueDescription;

        public QueueDeploymentManager(string serviceBusConnection, QueueDescription queueDescription)
        {
            //Partitioning, TTL, lock duration, delivery count, etc
            _client = new ManagementClient(serviceBusConnection);
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
