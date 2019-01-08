using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    public class MessageSenderTestMessageHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public bool ExpectedTenantIdFound { get; set; } = false;

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            var  ExpectedTenantId = "CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE";
            ExpectedTenantIdFound = ExpectedTenantId.Equals(context.GetTenantId(), StringComparison.InvariantCultureIgnoreCase);
            Calls.Add(DateTime.Now);
            await context.Complete().ConfigureAwait(false);
        }
    }

    [TestClass]
    public class MessageSenderTest
    {
        [TestMethod]
        public async Task MessageSender_WhenTenantIdIsProvided_MessageContainsTenantIdProperty()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new MessageSenderTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var queueName = "my-first-queue";
            var sender = factory.CreateMessageSender(queueName);
            var tenantId = Guid.Parse("CDBFB8A7-FEFF-4559-BFFB-301A2CB4E0BE");
            await sender.Send(new TestMessage1(), null, tenantId);

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
