using Microsoft.Azure.ServiceBus;
using RecordPoint.Messaging.Interfaces;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessagingFactory : IMessagingFactory
    {
        private AzureServiceBusSettings _settings;
        private IMessageHandlerFactory _messageHandlerFactory;
        private ServiceBusConnection _serviceBusConnection;

        public MessagingFactory(AzureServiceBusSettings settings, IMessageHandlerFactory messageHandlerFactory)
        {
            _settings = settings;
            _messageHandlerFactory = messageHandlerFactory;
            _serviceBusConnection = new ServiceBusConnection(_settings.ServiceBusConnectionString);
        }

        public IMessageHandlerFactory MessageHandlerFactory
        {
            get { return _messageHandlerFactory; }
        }

        public IMessagePump CreateMessagePump(string inputQueue)
        {
            return new MessagePump(_settings, _messageHandlerFactory, _serviceBusConnection, this, inputQueue) as IMessagePump;
        }

        public IMessageSender CreateMessageSender(string destination, IMessageProcessingContext context = null)
        {
            return new MessageSender(_settings, _serviceBusConnection, context as MessageProcessingContext, destination) as IMessageSender;
        }
    }
}
