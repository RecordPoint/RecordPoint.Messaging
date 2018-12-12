using System;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IMessageHandlerFactory
    {
        /// <summary>
        /// Returns the handler for the messageType passed
        /// </summary>
        /// <param name="messageType"></param>
        /// <returns></returns>
        IMessageHandler CreateMessageHandler(Type messageType);
    }
}
