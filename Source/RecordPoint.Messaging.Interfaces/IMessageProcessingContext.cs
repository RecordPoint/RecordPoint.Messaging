using System;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// An interface that represents the processing environment for a single message.
    /// </summary>
    public interface IMessageProcessingContext
    {
        /// <summary>
        /// If a message ID was provided by the sender, it can be accessed via this method.
        /// </summary>
        /// <returns></returns>
        string GetMessageId();

        /// <summary>
        /// If a tenant ID was provided by the sender, it can be accessed via this method.
        /// </summary>
        /// <returns></returns>
        string GetTenantId();

        /// <summary>
        /// Dead-letters the currently processing message. The message will be moved
        /// out of the queue and to a separate dead-letter store where it can be inspected
        /// and troubleshooted manually. After a message is dead-lettered it will not appear
        /// in the queue any more.
        /// 
        /// The underlying implementation of this method must include a retry policy to handle transient failures against the message queue. 
        /// The calling code should not attempt to add additional retry logic when dead lettering.
        /// </summary>
        /// <param name="reason">An application-defined reason for dead-lettering the message. This will appear on the dead lettered message and can be used for troubleshooting.</param>
        /// <param name="description">An application-defined description for dead-lettering the message. This will appear on the dead lettered message and can be used for troubleshooting.</param>
        /// <returns></returns>
        Task DeadLetter(string reason, string description = null);

        /// <summary>
        /// Completes the currently processing message. The message will be deleted from the queue
        /// and will not appear in the queue any more. Use this method when processing of a message
        /// in a handler has completed successfully.
        /// 
        /// The underlying implementation of this method must include a retry policy to handle transient failures against the message queue. 
        /// The calling code should not attempt to add additional retry logic when completing.
        /// </summary>
        /// <returns></returns>
        Task Complete();

        /// <summary>
        /// Abandons the currently processing message. The message will be returned to the queue 
        /// and will be picked up by a message handler again in future.
        /// 
        /// The underlying implementation of this method must include a retry policy to handle transient failures against the message queue. 
        /// The calling code should not attempt to add additional retry logic when abandoning.
        /// </summary>
        /// <returns></returns>
        Task Abandon();

        /// <summary>
        /// Defers the currently processing message. Returns the message back to the queue. The message will not be
        /// received again until the tryAgainIn time span has elapsed. 
        /// 
        /// The underlying implementation of this method must include a retry policy to handle transient failures against the message queue. 
        /// The calling code should not attempt to add additional retry logic when deferring.
        /// </summary>
        /// <param name="tryAgainIn">The amount of time to elapse before the message will be picked up by a handler again.</param>
        /// <returns></returns>
        Task Defer(TimeSpan tryAgainIn);

        /// <summary>
        /// Creates a message sender that sends messages in a transaction that is scoped to the current
        /// message processing context.
        /// Messages sent via this sender will not actually send unless the processing of the current
        /// message completes successfully.
        /// Note that for messsaging to be transactional, this must be used inside a handler whose class
        /// is decorated with the Transactional attribute.
        /// </summary>
        /// <param name="destination"></param>
        /// <returns></returns>
        IMessageSender CreateTransactionalSender(string destination);
    }
}
