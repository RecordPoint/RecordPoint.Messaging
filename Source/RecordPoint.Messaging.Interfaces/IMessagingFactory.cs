using System;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for creating message senders and message pumps.
    /// </summary>
    public interface IMessagingFactory
    {
        /// <summary>
        /// Creates a message sender for a given destination.
        /// </summary>
        /// <param name="destination">The name of the queue to which messages will be sent.</param>
        /// <param name="context">An optional IMessageProcessingContext, used for transactional sending.</param>
        /// <returns></returns>
        IMessageSender CreateMessageSender(string destination, IMessageProcessingContext context = null);

        /// <summary>
        /// Creates a message pump for a given input queue.
        /// </summary>
        /// <param name="inputQueue">The name of the queue from which messages will be pulled.</param>
        /// <returns></returns>
        IMessagePump CreateMessagePump(string inputQueue);
        
        /// <summary>
        /// 
        /// </summary>
        IMessageHandlerFactory MessageHandlerFactory { get; }
    }
}
