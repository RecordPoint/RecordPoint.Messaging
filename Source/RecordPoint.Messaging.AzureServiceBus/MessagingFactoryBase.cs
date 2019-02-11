using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public abstract class MessagingFactoryBase
    {
        protected AzureServiceBusSettings Settings { get; private set; }
        protected ServiceBusConnection ServiceBusConnection { get; private set; }

        private NamespaceInfo _namespaceInfo = null;
        private SemaphoreSlim _namespaceInfoSemaphore = new SemaphoreSlim(1);

        protected NamespaceInfo GetNamespaceInfo()
        {
            if (_namespaceInfo == null)
            {
                _namespaceInfoSemaphore.Wait();

                try
                {
                    if (_namespaceInfo == null)
                    {
                        var managementClient = new ManagementClient(Settings.ServiceBusConnectionString);
                        // https://stackoverflow.com/a/34518914/4407112
                        Task<NamespaceInfo> task = Task.Run<NamespaceInfo>(async () => await managementClient.GetNamespaceInfoAsync());
                        _namespaceInfo = task.Result;
                    }
                }
                finally
                {
                    _namespaceInfoSemaphore.Release();
                }
            }

            return _namespaceInfo;
        }

        public MessagingFactoryBase(AzureServiceBusSettings settings)
        {
            Settings = settings;
            ServiceBusConnection = new ServiceBusConnection(Settings.ServiceBusConnectionString);
        }
    }
}
