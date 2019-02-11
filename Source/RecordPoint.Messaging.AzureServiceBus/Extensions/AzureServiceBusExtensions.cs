using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace RecordPoint.Messaging.AzureServiceBus.Extensions
{
    public static class AzureServiceBusExtensions
    {
        public static bool TryGetContainer(this AzureServiceBusSettings settings, out CloudBlobContainer container)
        {
            if (CloudStorageAccount.TryParse(settings.BlobStorageConnectionString, out var storageAccount))
            {
                if(!string.IsNullOrEmpty(settings.BlobStorageContainerName))
                {
                    container = storageAccount.CreateCloudBlobClient().GetContainerReference(settings.BlobStorageContainerName);
                    return true;
                }
            }

            container = null;
            return false;
        }
    }
}
