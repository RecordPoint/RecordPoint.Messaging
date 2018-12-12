using System;

namespace RecordPoint.Messaging.Interfaces
{
    /// <summary>
    /// Indicates that a message handler methods should be run in a messaging transaction.
    /// When run in a messaging transaction, any messages sent from that handler will be
    /// sent atomically with the completion of the current message.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class TransactionalAttribute : Attribute
    {
    }
}
