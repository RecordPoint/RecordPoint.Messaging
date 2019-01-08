using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using RecordPoint.Messaging.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class PeekMessagePump: IPeekMessagePump
    {
        private AzureServiceBusSettings _settings;
        private MessageReceiver _messageReceiver;
        private IMessageHandlerFactory _messageHandlerFactory;
        private MessagingFactory _messagingFactory;

        public PeekMessagePump(AzureServiceBusSettings settings,
            IMessageHandlerFactory messageHandlerFactory,
            ServiceBusConnection connection,
            MessagingFactory messagingFactory,
            string inputQueue)
        {
            _settings = settings;
            _messageHandlerFactory = messageHandlerFactory;
            _messagingFactory = messagingFactory;

            // The RetryPolicy.Default supplied to the receiver uses an exponential backoff 
            // for transient failures. 
            _messageReceiver = new MessageReceiver(connection, inputQueue,
                ReceiveMode.PeekLock, RetryPolicy.Default);
            _messageReceiver.PrefetchCount = _settings.PrefetchCount;
        }

        private bool IsSameTenantId(Message message, Guid tenantId)
        {
            return tenantId.ToString().Equals(message.GetTenantId(), StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task<bool> PeekMessage(Guid tenantId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    //look at messages from Queue
                    var message = await _messageReceiver.PeekAsync().ConfigureAwait(false);
                    if (message == null)
                    {
                        //no more messages in the queue
                        return false;
                    }

                    if (IsSameTenantId(message, tenantId))
                    {
                        return true;                            
                    }
                }
                catch (Exception)
                {
                    //TODO: Log the exception
                    // Continue with next message
                }
            }
            //cancelation requested.
            return true;

        }
    }
}
