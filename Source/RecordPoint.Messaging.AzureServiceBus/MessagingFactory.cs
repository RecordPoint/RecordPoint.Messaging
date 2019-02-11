using RecordPoint.Messaging.Interfaces;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessagingFactory : MessageSenderFactory, IMessagingFactory
    {
        private readonly IMessageHandlerFactory _messageHandlerFactory;

        public MessagingFactory(AzureServiceBusSettings settings, IMessageHandlerFactory messageHandlerFactory)
            : base(settings)
        {
            _messageHandlerFactory = messageHandlerFactory;
        }

        public IMessageHandlerFactory MessageHandlerFactory
        {
            get { return _messageHandlerFactory; }
        }

        public IMessagePump CreateMessagePump(string inputQueue)
        {
            return new MessagePump(Settings, _messageHandlerFactory, ServiceBusConnection, this, inputQueue) as IMessagePump;
        }

        public IPeekMessagePump CreatePeekMessagePump(string inputQueue)
        {
            return new PeekMessagePump(Settings, _messageHandlerFactory, ServiceBusConnection, this, inputQueue) as IPeekMessagePump;
        }
    }
}
