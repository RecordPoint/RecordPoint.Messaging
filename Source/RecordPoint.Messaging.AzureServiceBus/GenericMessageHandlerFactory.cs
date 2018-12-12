using RecordPoint.Messaging.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.AzureServiceBus
{
    public class GenericMessageHandlerFactory : IMessageHandlerFactory
    {
        private Dictionary<string, IMessageHandler> _messageHandlers;

        public GenericMessageHandlerFactory(Dictionary<string, IMessageHandler> messageHandlers)
        {
            _messageHandlers = messageHandlers;
        }

        public IMessageHandler CreateMessageHandler(Type messageType)
        {
            IMessageHandler result = null;
            if(_messageHandlers.TryGetValue(messageType.FullName, out result))
            {
                return result;
            }
            throw new ArgumentOutOfRangeException(nameof(messageType), messageType.FullName, $"The MessageType {messageType} was not found in the Dictionary");
        }
    }
}
