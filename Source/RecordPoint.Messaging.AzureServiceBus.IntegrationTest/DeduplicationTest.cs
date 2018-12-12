using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    public class DeduplicationTestMessageHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public bool ExpectedIdFound { get; set; } = false;

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.Now);

            ExpectedIdFound = context.GetMessageId() == "1234";

            await context.Complete().ConfigureAwait(false);
        }
    }

    [TestClass]
    public class DeduplicationTest
    {
        [TestMethod]
        public async Task DuplicateMessageIdsAreNotReceivedWhenUsingADedupQueue()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new DeduplicationTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            // "dedup-queue" has been configured to deduplicate messages
            var sender = factory.CreateMessageSender("dedup-queue");

            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");

            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as DeduplicationTestMessageHandler;

            await TestHelpers.PumpQueueUntil(factory, "dedup-queue", () =>
            {
                return handler.Calls.Count == 1;
            }).ConfigureAwait(false);

            Assert.AreEqual(1, handler.Calls.Count);
            Assert.AreEqual(true, handler.ExpectedIdFound);
        }

        [TestMethod]
        public async Task DuplicateMessageIdsAreReceivedWhenUsingANonDedupQueue()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new DeduplicationTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            // "my-first-queue" has not been configured to deduplicate messages
            var sender = factory.CreateMessageSender("my-first-queue");

            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as DeduplicationTestMessageHandler;

            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");
            await sender.Send(new TestMessage1(), "1234");

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return handler.Calls.Count == 4;
            }).ConfigureAwait(false);
            
            Assert.AreEqual(4, handler.Calls.Count);
            Assert.AreEqual(true, handler.ExpectedIdFound);
        }
    }
}
