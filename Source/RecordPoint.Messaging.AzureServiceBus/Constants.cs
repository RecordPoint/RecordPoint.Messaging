namespace RecordPoint.Messaging.AzureServiceBus
{
    public static class Constants
    {
        public static class HeaderKeys
        {
            /// <summary>
            /// Indicates the .NET type that the message body should be deserialized in to.
            /// This should be present on all messages flowing through this library.
            /// </summary>
            public const string RPMessageType = nameof(RPMessageType);

            /// <summary>
            /// An ID that uniquely identifies the running context. When this is not null or empty, 
            /// message pumps will automatically abandon any message whose RPContextID header value don't
            /// match the expected value.
            /// This is useful for automated tests that share a common broker that run in parallel.
            /// </summary>
            public const string RPContextId = nameof(RPContextId);

            /// <summary>
            /// Only used when a message is deferred using IMessageProcessingContext.Defer.
            /// The deferred message is Deferred in Azure Service Bus, and a control message is sent
            /// with scheduled delivery at the time when the message is to be tried again. The control
            /// message will have this header, whose value indicates the sequence number of the deferred message that is
            /// to be retried.
            /// </summary>
            public const string RPDeferredMessageSequenceNumber = nameof(RPDeferredMessageSequenceNumber);

            /// <summary>
            /// Only used for messages whose payload is too big for a single message.
            /// The presence of this header indicates that the message body is stored in blob storage.
            /// The value is the size of the blob in blob storage in bytes.
            /// </summary>
            public const string RPBlobSizeBytes = nameof(RPBlobSizeBytes);
        }
    }
}
