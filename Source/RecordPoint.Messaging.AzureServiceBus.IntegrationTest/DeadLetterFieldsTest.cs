using LightInject;
using Microsoft.Azure.ServiceBus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.AzureServiceBus.Test;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.IntegrationTest
{
    public class DeadLetterFieldTestHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();
        
        public Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.Now);

            throw new ApplicationException("This is an exception that should cause the message to be dead-lettered eventually.");
        }
    }

    [TestClass]
    public class DeadLetterFieldsTest
    {
        [TestMethod]
        public async Task ExceptionsDeadLetterMessagesWithUsefulDeadLetterFields()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new DeadLetterFieldTestHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            // "dedup-queue" has been configured to deduplicate messages
            var sender = factory.CreateMessageSender("my-first-queue");

            await sender.Send(new TestMessage1(), "1234");

            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as DeadLetterFieldTestHandler;

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return handler.Calls.Count == 10;
            }).ConfigureAwait(false);

            Assert.AreEqual(10, handler.Calls.Count);

            var deadLetterQueue = EntityNameHelper.FormatDeadLetterPath("my-first-queue");
            var deadLetteredItems = await TestHelpers.GetAllRawMessagesByContextId(settings, deadLetterQueue).ConfigureAwait(false);

            Assert.AreEqual(1, deadLetteredItems.Count);

            Assert.AreEqual("System.ApplicationException: This is an exception that should cause the message to be dead-lettered eventually.", deadLetteredItems[0].UserProperties["DeadLetterReason"]);
            var deadLetterErrorDescription = deadLetteredItems[0].UserProperties["DeadLetterErrorDescription"].ToString();
            var expectedStartOfDeadLetterErrorDescription = "System.ApplicationException: This is an exception that should cause the message to be dead-lettered eventually.\r\n   at RecordPoint.Messaging.AzureServiceBus.IntegrationTest.DeadLetterFieldTestHandler";
            Assert.IsTrue(deadLetterErrorDescription.StartsWith(expectedStartOfDeadLetterErrorDescription));
            Assert.IsTrue(deadLetterErrorDescription.Length <= 1024);
        }
    }
}
