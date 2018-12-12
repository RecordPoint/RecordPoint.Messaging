using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Encapsulates a long-running process that pulls messages from an input queue 
    /// and calls an appropriate handler. 
    /// Message handlers are resolved from the IoC container used to initialize the 
    /// Azure Service Bus messaging subsystem.
    /// </summary>
    public interface IMessagePump
    {
        /// <summary>
        /// Starts the message pump.
        /// </summary>
        /// <returns></returns>
        Task Start();

        /// <summary>
        /// Stops the message pump.
        /// </summary>
        /// <returns></returns>
        Task Stop();
    }
}
