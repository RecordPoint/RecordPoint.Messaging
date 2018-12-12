using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    [TestClass]
    public class QueueDeploymentManagerTest
    {
        [TestMethod]
        public async Task QueueInstallUninstallTest()
        {
            var newQueue = new Microsoft.Azure.ServiceBus.Management.QueueDescription("alex-queue-test") { MaxSizeInMB = 1024 };
            var queueManager = new QueueDeploymentManager(TestHelpers.GetSettings().ServiceBusConnectionString, newQueue);
            var installed = await queueManager.Install();
            Assert.AreEqual(true, installed);
            var installedAgain = await queueManager.Install();
            Assert.AreEqual(false, installedAgain);
            var enabled = await queueManager.IsEnabled();
            Assert.AreEqual(true, enabled);
            var uninstalled = await queueManager.Uninstall();
            Assert.AreEqual(true, uninstalled);
            var uninstalledAgain = await queueManager.Uninstall();
            Assert.AreEqual(false, uninstalledAgain);
        }
    }
}
