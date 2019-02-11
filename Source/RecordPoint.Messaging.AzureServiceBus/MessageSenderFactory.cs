using RecordPoint.Messaging.Interfaces;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class MessageSenderFactory : MessagingFactoryBase, IMessageSenderFactory
    {
        public MessageSenderFactory(AzureServiceBusSettings settings)
            : base(settings)
        {
        }

        public IMessageSender CreateMessageSender(string destination, IMessageProcessingContext context = null)
        {
            return new MessageSender(Settings,
                ServiceBusConnection,
                GetNamespaceInfo(),
                context as MessageProcessingContext, 
                destination) as IMessageSender;
        }
    }
}
