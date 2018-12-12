using System;

namespace RecordPoint.Messaging.AzureServiceBus
{
    /// <summary>
    /// Class that encapsulates the settings required to connect to an Azure Service Bus namespace.
    /// </summary>
    public class AzureServiceBusSettings
    {
        /// <summary>
        /// Connection string to an Azure Service Bus namespace.
        /// </summary>
        public string ServiceBusConnectionString { get; set; }

        /// <summary>
        /// Connection string to an Azure Blob Storage instance for use with large message bodies.
        /// Messages larger than the MaxMessageSizeKb value are sent via blob storage. Messages smaller
        /// than this value do not use blob storage at all.
        /// </summary>
        public string BlobStorageConnectionString { get; set; }

        /// <summary>
        /// Blob storage container in which to place large message bodies.
        /// </summary>
        public string BlobStorageContainerName { get; set; }

        /// <summary>
        /// The maximum message size for the Azure Service Bus namespace. 256 kb for basic and standard
        /// and 1024 kb for premium.
        /// </summary>
        public int MaxMessageSizeKb { get; set; } = 256;

        /// <summary>
        /// When set to true, all message handlers will automatically complete all messages.
        /// When set to false, messages need to be explicitly Completed.
        /// </summary>
        public bool AutoComplete { get; set; } = false;

        /// <summary>
        /// The maximum number of messages that a message pump will consume concurrently.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = 4;

        /// <summary>
        /// Number of messages for message pumps to prefetch from the broker.
        /// See https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements#prefetching 
        /// for details and guidance.
        /// </summary>
        public int PrefetchCount { get; set; } = 0;

        /// <summary>
        /// An ID that uniquely identifies the running context. When this is not null or empty, 
        /// message pumps will automatically abandon any message whose RPContextID header value doesn't match this value.
        /// This is useful for automated tests that share a common broker that run in parallel.
        /// </summary>
        public string ContextId { get; set; } = null;
    }
}
