using LightInject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    public class PerformanceTestMessage
    {
        public int SomeInt { get; set; } = 17;

        public string SomeString { get; set; } = "this is a string";

        public string AnotherString { get; set; } = "";
    }
   
    public class PerformanceTestMessageHandler : IMessageHandler
    {
        public ConcurrentBag<DateTime> Calls { get; set; } = new ConcurrentBag<DateTime>();

        public async Task HandleMessage(object message, IMessageProcessingContext context)
        {
            Calls.Add(DateTime.Now);

            await context.Complete().ConfigureAwait(false);
        }
    }

    [TestClass]
    public class PerformanceTest
    {
        double smallMessageRatio = 0.8;
        double mediumMessageRatio = 0.15;
        //double largeMessageRatio = 0.05;

        int smallMessageSizeKb = 5;
        int mediumMessageSizeKb = 128;
        int largeMessageSizeKb = 1024;

        int totalMessageCount = 10000;

        ThreadLocal<Random> random = new ThreadLocal<Random>(() => new Random());

        int smallMessageCount = 0;
        int mediumMessageCount = 0;
        int largeMessageCount = 0;

        private SemaphoreSlim sendSemaphore = new SemaphoreSlim(16);

        private async Task SendMessage(IMessageSender sender)
        {
            await sendSemaphore.WaitAsync();
            try
            {
                var randomValue = random.Value.NextDouble();
                var messageSizeKb = 0;
                if (randomValue <= smallMessageRatio)
                {
                    Interlocked.Increment(ref smallMessageCount);
                    messageSizeKb = smallMessageSizeKb;
                }
                if (randomValue > smallMessageRatio && randomValue <= smallMessageRatio + mediumMessageRatio)
                {
                    Interlocked.Increment(ref mediumMessageCount);
                    messageSizeKb = mediumMessageSizeKb;
                }
                else if (randomValue > smallMessageRatio + mediumMessageRatio)
                {
                    Interlocked.Increment(ref largeMessageCount);
                    messageSizeKb = largeMessageSizeKb;
                }

                var performanceTestMessage = new PerformanceTestMessage();
                var stringBuilder = new StringBuilder();
                // Pad the AnotherString property out to meet the required message size
                for (int i = 0; i < messageSizeKb * 16; ++i)
                {
                    stringBuilder.Append("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                }
                performanceTestMessage.AnotherString = stringBuilder.ToString();

                await sender.Send(performanceTestMessage);
            }
            finally
            {
                sendSemaphore.Release();
            }
        }

        [TestMethod]
        public async Task PerfTest()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;

            var settings = TestHelpers.GetSettings();
            settings.PrefetchCount = 300;
            var handlers = new Dictionary<string, IMessageHandler>();
            handlers.Add(typeof(PerformanceTestMessage).FullName, new PerformanceTestMessageHandler());
            var mhf = new GenericMessageHandlerFactory(handlers);
            var container = TestHelpers.GetContainer(settings, mhf);

            var factory = container.GetInstance<IMessagingFactory>();

            var sender = factory.CreateMessageSender("my-first-queue");

            using (var t1 = new Timer($"Send {totalMessageCount} messages"))
            {
                var sendTasks = new Task[totalMessageCount];
                for(int i = 0; i<totalMessageCount; ++i)
                {
                    sendTasks[i] = SendMessage(sender);
                }
                await Task.WhenAll(sendTasks);
            }

            Console.WriteLine($"Sent {smallMessageCount} {smallMessageSizeKb} KB messages, {mediumMessageCount} {mediumMessageSizeKb} KB messages and {largeMessageCount} {largeMessageSizeKb} KB messages.");
            
            var pump = factory.CreateMessagePump("my-first-queue");
            var handler = factory.MessageHandlerFactory.CreateMessageHandler(typeof(PerformanceTestMessage)) as PerformanceTestMessageHandler;

            using (var t2 = new Timer($"Receive {totalMessageCount} messages"))
            {
                await pump.Start();
                
                while (handler.Calls.Count < totalMessageCount)
                {
                    await Task.Delay(1000);
                }

                await pump.Stop();
            }
        }
    }
}
