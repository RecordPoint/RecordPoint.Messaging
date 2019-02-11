using LightInject;
using Microsoft.Azure.ServiceBus;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    public class DeferralTestMessage
    {
        public int NumberOfTimesToDefer { get; set; }

        public int NumberOfTimesToAbandon { get; set; }

        /// <summary>
        /// When true, the message will be completed after its been deferred and abandoned, when false it will be dead lettered
        /// </summary>
        public bool Complete { get; set; }

        public string ALongString { get; set; }
    }

    public class DeferralTestMessageHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        private bool _hasDeadLettered = false;

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.UtcNow);
            var deferralTestMessage = message as DeferralTestMessage;
            if(deferralTestMessage == null)
            {
                return;
            }
            if (Calls.Count <= deferralTestMessage.NumberOfTimesToDefer)
            {
                await context.Defer(TimeSpan.FromSeconds(3));
            }
            else if (Calls.Count - deferralTestMessage.NumberOfTimesToDefer <= deferralTestMessage.NumberOfTimesToAbandon)
            {
                await context.Abandon();
            }
            else
            {
                if (deferralTestMessage.Complete)
                {
                    await context.Complete();
                }
                else
                {
                    if (!_hasDeadLettered)
                    {
                        await context.DeadLetter("Deadlettering this message");
                        _hasDeadLettered = true;
                    }
                }
            }
        }
    }
    
    [TestClass]
    public class DeferralTest
    {
        [DataRow(1,     0, 0, true, 1)]    // 1kb, completed rightaway
        [DataRow(300,   0, 0, true, 1)]    // 300kb, completed rightaway
        [DataRow(1,     0, 1, true, 2)]     // 1kb, abandoned and completed
        [DataRow(300,   0, 1, true, 2)]     // 300kb, abandoned and completed
        [DataRow(1,     1, 0, true, 2)]    // 1kb, deferred and completed
        [DataRow(300,   1, 0, true, 2)]    // 300kb, deferred and completed
        [DataRow(1,     3, 0, true, 4)]    // 1kb, deferred 3 times and completed
        [DataRow(300,   3, 0, true, 4)]    // 300kb, deferred 3 times and completed
        [DataRow(1,     1, 1, true, 3)]    // 1kb, deferred, abandoned and completed
        [DataRow(300,   1, 1, true, 3)]    // 300kb, deferred, abandoned and completed
        [DataRow(1,     2, 1, true, 4)]    // 1kb, deferred twice, abandoned and completed
        [DataRow(300,   2, 1, true, 4)]    // 300kb, deferred twice, abandoned and completed
        [DataRow(1,     2, 2, true, 5)]    // 1kb, deferred twice, abandoned twice and completed        
        [DataRow(300,   2, 2, true, 5)]    // 300kb, deferred twice, abandoned twice and completed        
        [DataRow(1,     0, 0, false, 1)]   // 1kb, dead lettered right away
        [DataRow(300,   0, 0, false, 1)]   // 300kb, dead lettered right away
        [DataRow(1,     0, 1, false, 2)]   // 1kb, abandoned and dead lettered
        [DataRow(300,   0, 1, false, 2)]   // 300kb, abandoned and dead lettered
        [DataRow(1,     1, 0, false, 2)]   // 1kb, deferred and then dead lettered
        [DataRow(300,   1, 0, false, 2)]   // 300kb, deferred and then dead lettered
        [DataRow(1,     1, 1, false, 3)]   // 1kb, deferred, abandoned and then dead lettered
        [DataRow(300,   1, 1, false, 3)]   // 300kb deferred, abandoned and then dead lettered
        [DataRow(1,     2, 2, false, 5)]   // 1kb, deferred twice, abandoned twice and then dead lettered
        [DataRow(300,   2, 2, false, 5)]   // 300kb, deferred twice, abandoned twice and then dead lettered
        [DataTestMethod]
        public async Task MessagesCanBeDeferred(int messageSizeKb, int timesToDefer, int timesToAbandon, bool complete, int expectedCalls)
        {
            var settings = TestHelpers.GetSettings();
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(DeferralTestMessage).FullName, new DeferralTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();
            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(DeferralTestMessage)) as DeferralTestMessageHandler;

            var queueName = "my-first-queue";

            var sender = factory.CreateMessageSender(queueName);

            var message = new DeferralTestMessage()
            {
                NumberOfTimesToDefer = timesToDefer,
                NumberOfTimesToAbandon = timesToAbandon,
                Complete = complete,
            };

            // Pad the ALongString property out to meet the required message size
            for(int i = 0; i<messageSizeKb * 16; ++i)
            {
                message.ALongString += "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            }

            // Send a message to be deferred, then abandoned, then deadlettered or completed
            await sender.Send(message).ConfigureAwait(false);

            await TestHelpers.PumpQueueUntil(factory, queueName, async () =>
            {
                if(handler.Calls.Count != expectedCalls)
                {
                    return false;
                }

                // if we deferred any messages, there should be 2 raw messages in the DLQ - 1 actual message and 1 control message from the deferral
                var expectedDeadLetteredItems = 0;
                if (complete == false)
                {
                    expectedDeadLetteredItems = timesToDefer > 0 ? 2 : 1;
                }
                var deadLetterQueue = EntityNameHelper.FormatDeadLetterPath(queueName);
                var deadLetteredItems = await TestHelpers.GetAllRawMessagesByContextId(settings, deadLetterQueue).ConfigureAwait(false);
                return expectedDeadLetteredItems == deadLetteredItems.Count;
            }).ConfigureAwait(false);
            
            Assert.AreEqual(expectedCalls, handler.Calls.Count);
        }
    }
}
