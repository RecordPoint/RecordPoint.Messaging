using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    [Transactional]
    public class TransactionalHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();
        
        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.UtcNow);

            if (Calls.Count > 1)
            {
                await context.Complete();
                return;
            }

            var textMessage1 = (TestMessage1)message;
            var sender = context.CreateTransactionalSender(textMessage1.TestProperty);
            await sender.Send(new TestMessage2());

            throw new Exception("something went wrong");
        }
    }

    public class NonTransactionalHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.UtcNow);

            if (Calls.Count > 1)
            {
                await context.Complete();
                return;
            }
            var textMessage1 = (TestMessage1)message;
            var sender = context.CreateTransactionalSender(textMessage1.TestProperty);
            await sender.Send(new TestMessage2());

            throw new Exception("something went wrong");
        }
    }

    public class SecondStageHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.UtcNow);

            await context.Complete();
        }
    }

    [TestClass]
    public class TransactionalHandlerTest
    {
        [TestMethod]
        public async Task TransactionalHandlerDoesntSendMessageToDifferentQueueOnFailure()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new TransactionalHandler());
            handlers.Add(typeof(TestMessage2).FullName, new SecondStageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var sender = factory.CreateMessageSender("my-first-queue");

            var transactionalHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as TransactionalHandler;

            // Send a message to the first queue, the handler will send a message to the second queue
            // (but will throw an exception and abort the transaction, and the message to the second queue
            // will not actually be sent)
            await sender.Send(new TestMessage1 { TestProperty = "my-second-queue" });

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return transactionalHandler.Calls.Count == 2;
            });

            // pump the second queue for 5 seconds and verify no message was received
            var pump2 = factory.CreateMessagePump("my-second-queue");
            
            await pump2.Start();
            
            await Task.Delay(5000);
            
            await pump2.Stop();

            var secondStageHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage2)) as SecondStageHandler;

            Assert.AreEqual(2, transactionalHandler.Calls.Count);
            Assert.AreEqual(0, secondStageHandler.Calls.Count);
        }

        [TestMethod]
        public async Task NonTransactionalHandlerSendsMessageToDifferentQueueOnFailure()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new NonTransactionalHandler());
            handlers.Add(typeof(TestMessage2).FullName, new SecondStageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var nonTransactionalHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as NonTransactionalHandler;

            // Send a message to the first queue, the handler will send a message to the second queue
            var sender = factory.CreateMessageSender("my-first-queue");
            await sender.Send(new TestMessage1 { TestProperty = "my-second-queue" });

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return nonTransactionalHandler.Calls.Count == 2;
            });

            var secondStageHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage2)) as SecondStageHandler;

            await TestHelpers.PumpQueueUntil(factory, "my-second-queue", () =>
            {
                return secondStageHandler.Calls.Count == 1;
            });
            
            Assert.AreEqual(2, nonTransactionalHandler.Calls.Count);
            Assert.AreEqual(1, secondStageHandler.Calls.Count);
        }

        [TestMethod]
        public async Task TransactionalHandlerDoesntSendMessageToSameQueueOnFailure()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new TransactionalHandler());
            handlers.Add(typeof(TestMessage2).FullName, new SecondStageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);
            var factory = container.GetInstance<IMessagingFactory>();

            var transactionalHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as TransactionalHandler;
            var secondStageHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage2)) as SecondStageHandler;

            var sender = factory.CreateMessageSender("my-first-queue");
            
            await sender.Send(new TestMessage1 { TestProperty = "my-first-queue" });

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return transactionalHandler.Calls.Count == 2;
            });
            
            Assert.AreEqual(2, transactionalHandler.Calls.Count);
            Assert.AreEqual(0, secondStageHandler.Calls.Count);
        }

        [TestMethod]
        public async Task NonTransactionalHandlerSendsMessageToSameQueueOnFailure()
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(TestMessage1).FullName, new NonTransactionalHandler());
            handlers.Add(typeof(TestMessage2).FullName, new SecondStageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);
            var factory = container.GetInstance<IMessagingFactory>();

            var nonTransactionalHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage1)) as NonTransactionalHandler;
            var secondStageHandler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(TestMessage2)) as SecondStageHandler;

            var sender = factory.CreateMessageSender("my-first-queue");
            
            await sender.Send(new TestMessage1 { TestProperty = "my-first-queue" });

            await TestHelpers.PumpQueueUntil(factory, "my-first-queue", () =>
            {
                return nonTransactionalHandler.Calls.Count == 2 &&
                    secondStageHandler.Calls.Count == 1;
            });
            
            Assert.AreEqual(2, nonTransactionalHandler.Calls.Count);
            Assert.AreEqual(1, secondStageHandler.Calls.Count);
        }
    }
}
