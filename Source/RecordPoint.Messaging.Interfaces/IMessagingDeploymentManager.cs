using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Interface for managing deployment of messaging infrastructure.
    /// </summary>
    public interface IMessagingDeploymentManager
    {
        /// <summary>
        /// Installs messaging infrastructure.
        /// </summary>
        /// <returns></returns>
        Task<bool> Install();

        /// <summary>
        /// Uninstalls messaging infrastructure.
        /// </summary>
        /// <returns></returns>
        Task<bool> Uninstall();

        /// <summary>
        /// Determines if messaging infrastructure is enabled.
        /// </summary>
        /// <returns></returns>
        Task<bool> IsEnabled();
    }
}
