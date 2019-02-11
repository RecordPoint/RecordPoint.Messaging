namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for creating message senders and message pumps.
    /// </summary>
    public interface IMessagingFactory : IMessageSenderFactory
    {
        /// <summary>
        /// Creates a message pump for a given input queue.
        /// </summary>
        /// <param name="inputQueue">The name of the queue from which messages will be pulled.</param>
        /// <returns></returns>
        IMessagePump CreateMessagePump(string inputQueue);

        /// <summary>
        /// Creates a message pump to peek for a given input queue.
        /// </summary>
        /// <param name="inputQueue">The name of the queue from which messages will be pulled.</param>
        /// <returns></returns>
        IPeekMessagePump CreatePeekMessagePump(string inputQueue);

        /// <summary>
        /// Gets an IMessageHandlerFactory.
        /// </summary>
        IMessageHandlerFactory MessageHandlerFactory { get; }
    }
}
