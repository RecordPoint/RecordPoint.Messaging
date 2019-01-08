using LightInject;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus.Test
{
    public static class TestHelpers
    {
        private const string EnvironmentVariable_ServiceBusConnectionString = "RP_M_SBCONNECTION";
        private const string EnvironmentVariable_BlobConnectionString = "RP_M_BLOBCONNECTION";

        public static AzureServiceBusSettings GetSettings()
        {
            var serviceBusConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable_ServiceBusConnectionString);
            if (string.IsNullOrEmpty(serviceBusConnectionString))
            {
                throw new ApplicationException($"No Service Bus connection string found in environment variable {EnvironmentVariable_ServiceBusConnectionString}");
            }

            var blobConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariable_BlobConnectionString);
            if (string.IsNullOrEmpty(blobConnectionString))
            {
                throw new ApplicationException($"No Blob connection string found in environment variable {EnvironmentVariable_BlobConnectionString}");
            }

            var settings = new AzureServiceBusSettings
            {
                ServiceBusConnectionString = serviceBusConnectionString,
                BlobStorageConnectionString = blobConnectionString,
                BlobStorageContainerName = "messagebodies",
                MaxDegreeOfParallelism = 8,
                AutoComplete = false,
                ContextId = Guid.NewGuid().ToString()   // provide a unique value for each test run - this ensures that tests only consume messages intended for them and don't interfere with other tests.
            };

            return settings;
        }

        public static IServiceContainer GetContainer(AzureServiceBusSettings settings, IMessageHandlerFactory messageHandlerFactory)
        {
            var result = new ServiceContainer();
            result.Register<IServiceContainer>(x => result, new PerContainerLifetime());
            result.Register<AzureServiceBusSettings>(x => settings, new PerContainerLifetime());
            result.Register<IMessageHandlerFactory>(x =>
            {
                return messageHandlerFactory;
            }, new PerContainerLifetime());
            result.Register<IMessagingFactory, MessagingFactory>(new PerContainerLifetime());
            result.Register<RecordPoint.Messaging.Interfaces.IMessageSender, MessageSender>(new PerContainerLifetime());
            result.Register<IMessagePump, MessagePump>(new PerContainerLifetime());
            return result;
        }

        private static async Task AwaitWithTimeout(this Task task, int timeoutSeconds)
        {
            var delayTask = Task.Delay(timeoutSeconds * 1000);
            var firstTask = await Task.WhenAny(task, delayTask ).ConfigureAwait(false);
            if (delayTask == firstTask)
            {
                throw new TimeoutException();
            }
        }

        public static async Task PumpQueueUntil(IMessagingFactory factory, string queue, Func<Task<bool>> completionCondition)
        {
            await AwaitWithTimeout(InnerPumpQueueUntil(factory, queue, completionCondition), 60);
        }

        public static async Task PumpQueueUntil(IMessagingFactory factory, string queue, Func<bool> completionCondition)
        {
            var messagePump = factory.CreateMessagePump(queue);
            await messagePump.Start().ConfigureAwait(false);

            while (true)
            {
                if (completionCondition())
                {
                    // Wait a few seconds before stopping, to allow any outgoing operations (e.g., dead lettering) to be flushed
                    await Task.Delay(5000).ConfigureAwait(false);
                    await messagePump.Stop().ConfigureAwait(false);
                    break;
                }
                else
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        private static async Task InnerPumpQueueUntil(IMessagingFactory factory, string queue, Func<Task<bool>> completionCondition)
        {
            var messagePump = factory.CreateMessagePump(queue);
            await messagePump.Start().ConfigureAwait(false);

            while (true)
            {
                if (await completionCondition().ConfigureAwait(false))
                {
                    // Wait a few seconds before stopping, to allow any outgoing operations (e.g., dead lettering) to be flushed
                    await Task.Delay(5000).ConfigureAwait(false);
                    await messagePump.Stop().ConfigureAwait(false);
                    break;
                }
                else
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        }

        public static async Task<IList<Message>> GetAllRawMessagesByContextId(AzureServiceBusSettings settings, string queue)
        {
            var receiver = new MessageReceiver(settings.ServiceBusConnectionString, queue, ReceiveMode.PeekLock);
            List<Message> allPeekedMessages = new List<Message>();
            IList<Message> peekedMessages = await receiver.PeekAsync(1000).ConfigureAwait(false);
            while (peekedMessages.Count > 0)
            {
                allPeekedMessages.AddRange(peekedMessages);
                peekedMessages = await receiver.PeekAsync(1000).ConfigureAwait(false);
            }
            
            return allPeekedMessages.Where(x => x.UserProperties.TryGetValue(Constants.HeaderKeys.RPContextId, out var contextId) && (string)contextId == settings.ContextId).ToList();
        }

        public static async Task CheckRawMessagesByContextIdUntil(AzureServiceBusSettings settings, string queue, Func<IList<Message>, bool> completionCondition)
        {
            await AwaitWithTimeout(InnerCheckRawMessagesByContextIdUntil(settings, queue, completionCondition), 120);
        }

        private static async Task InnerCheckRawMessagesByContextIdUntil(AzureServiceBusSettings settings, string queue, Func<IList<Message>, bool> completionCondition)
        {
            while (true)
            {
                var allMessages = await GetAllRawMessagesByContextId(settings, queue).ConfigureAwait(false);
                if (completionCondition(allMessages))
                {
                    break;
                }
                else
                {
                    await Task.Delay(1000);
                }
            }
        }
    }
}
