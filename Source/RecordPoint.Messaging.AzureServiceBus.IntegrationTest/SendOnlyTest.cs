using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.AzureServiceBus.Test;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.IntegrationTest
{
    [TestClass]
    public class SendOnlyTest
    {
        [TestMethod]
        public async Task SendWithIMessageSenderFactory()
        {
            var settings = TestHelpers.GetSettings();
            var queueName = "my-first-queue";
            var tenantId = Guid.Parse("CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE");

            // Do a send using only the sending infrastucture
            {
                var sendOnlyContainer = TestHelpers.GetSendOnlyContainer(settings);
                var senderFactory = sendOnlyContainer.GetInstance<IMessageSenderFactory>();
                var sender = senderFactory.CreateMessageSender(queueName);
                await sender.Send(new TestMessage1(), null, tenantId);
            }
           
            // Receive using the full infrastructure to test that the send worked
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new MessageSenderTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            
            var factory = container.GetInstance<IMessagingFactory>();
            
            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as MessageSenderTestMessageHandler;

            await TestHelpers.PumpQueueUntil(factory, queueName, () =>
            {
                return handler.Calls.Count == 1;
            }).ConfigureAwait(false);

            Assert.AreEqual(1, handler.Calls.Count);
            Assert.AreEqual(true, handler.ExpectedTenantIdFound);
        }
    }
}
