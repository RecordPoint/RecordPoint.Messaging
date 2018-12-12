using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for handling messages of a given type.
    /// To handle messages, define classes that implement this
    /// interface and register them in the IoC container used to initialize
    /// the messaging implementation. Message Pumps will resolve the correct
    /// handler from the container and call the HandleMessage method.
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// Handles a message of a given type. 
        /// HandleMessage may be called one or more times for each instance of a message
        /// in the queue (i.e., at-least-once delivery). Therefore, message handling logic
        /// should be idempotent.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        /// <param name="context">The context of the processing.</param>
        /// <returns></returns>
        Task HandleMessage(object message, IMessageProcessingContext context);
    }
}
