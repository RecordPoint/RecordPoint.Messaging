using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    [TestClass]
    public class PeekMessagePumpTest
    {
        [TestMethod]
        public async Task PeekMessagePump_WhenMessageWithTenantIdExists_ReturnsTrue()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var queueName = "my-first-queue";

            var sender = factory.CreateMessageSender(queueName);

            var tenantId = Guid.Parse("CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE");

            await sender.Send(new TestMessage1(), null, tenantId);

            var messagePump = factory.CreatePeekMessagePump(queueName);

            var hasMessage = await messagePump.PeekMessage(tenantId, new CancellationToken());

            Assert.IsTrue(hasMessage);
        }
        [TestMethod]
        public async Task PeekMessagePump_WhenMessageWithTenantIdDoesNotExists_ReturnsFalse()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var queueName = "my-first-queue";

            var sender = factory.CreateMessageSender(queueName);

            var tenantId = Guid.Parse("CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE");

            await sender.Send(new TestMessage1(), null, tenantId);

            var nonExistingTenantId = Guid.Parse("313E92AB-2719-4C6B-8BC4-4A88240E4FB8");

            var messagePump = factory.CreatePeekMessagePump(queueName);

            var hasMessage = await messagePump.PeekMessage(nonExistingTenantId, new CancellationToken());

            Assert.IsFalse(hasMessage);
        }

        [TestMethod]
        public async Task PeekMessagePump_WhenCanceled_ReturnsTrue()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var queueName = "my-first-queue";

            var sender = factory.CreateMessageSender(queueName);

            var tenantId = Guid.Parse("CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE");

            var messagePump = factory.CreatePeekMessagePump(queueName);

            var cancelledToken = new CancellationToken(true);
            var hasMessage = await messagePump.PeekMessage(tenantId, cancelledToken);

            Assert.IsTrue(hasMessage);
        }
    }
}
