using System;
using System.Threading;
using System.Threading.Tasks;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// </summary>
    public interface IPeekMessagePump
    {
        /// <summary>
        /// Checks if message exists in the message queue with the given tenantId
        /// Does NOT call any handlers.
        /// </summary>
        /// <param name="tenantId">ID to check in the messages</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<bool> PeekMessage(Guid tenantId, CancellationToken ct);
    }
}
