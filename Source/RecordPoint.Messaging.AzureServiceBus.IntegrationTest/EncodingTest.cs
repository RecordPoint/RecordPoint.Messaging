using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.AzureServiceBus.Test;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.IntegrationTest
{
    public class EncodingTestHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public bool ExpectedContentFound { get; set; } = false;

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            var testMessage = message as TestMessage1;
            ExpectedContentFound = testMessage.TestProperty == "巴黎";

            Calls.Add(DateTime.Now);
            await context.Complete().ConfigureAwait(false);
        }
    }

    [TestClass]
    public class EncodingTest
    {
        [TestMethod]
        public async Task WhenUsingExtendedCharacters_IsDecodedCorrectly()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new EncodingTestHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var queueName = "my-first-queue";
            var sender = factory.CreateMessageSender(queueName);
            await sender.Send(new TestMessage1 { TestProperty = "巴黎" });

            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as EncodingTestHandler;

            await TestHelpers.PumpQueueUntil(factory, queueName, () =>
            {
                return handler.Calls.Count == 1;
            }).ConfigureAwait(false);

            Assert.AreEqual(1, handler.Calls.Count);
            Assert.AreEqual(true, handler.ExpectedContentFound);
        }
    }
}
