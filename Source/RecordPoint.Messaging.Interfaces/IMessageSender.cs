using System;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for sending messages.
    /// All messages sent from an IMessageSender instance go to the same queue.
    /// </summary>
    public interface IMessageSender
    {
        /// <summary>
        /// Sends a message.
        /// 
        /// The underlying implementation of this method may include a retry policy to handle transient failures against the message queue. 
        /// The calling code should not attempt to add additional retry logic when sending.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="message">The message payload.</param>
        /// <param name="messageId">If specified, an ID that uniquely identifies the message. This may be used for deduplication in the message broker.</param>
        /// <param name="scheduleAt">If specified, the DateTime that the message will become available for processing in the message broker.</param>
        /// <returns></returns>
        Task Send<T>(T message, string messageId = null, DateTime? scheduleAt = null);
    }
}
