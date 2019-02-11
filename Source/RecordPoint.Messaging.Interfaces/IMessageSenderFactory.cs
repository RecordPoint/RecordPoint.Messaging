namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for creating message senders.
    /// </summary>
    public interface IMessageSenderFactory
    {
        /// <summary>
        /// Creates a message sender for a given destination.
        /// </summary>
        /// <param name="destination">The name of the queue to which messages will be sent.</param>
        /// <param name="context">An optional IMessageProcessingContext, used for transactional sending.</param>
        /// <returns></returns>
        IMessageSender CreateMessageSender(string destination, IMessageProcessingContext context = null);
    }
}
