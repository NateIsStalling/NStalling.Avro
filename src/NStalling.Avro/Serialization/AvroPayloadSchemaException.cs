using System;

namespace NStalling.Avro.Serialization
{
    /// <summary>
    /// Thrown when a payload schema source encounters an infrastructure/contract failure while
    /// acquiring an inner writer schema. Ordinary "not found / unrecognized discriminator" results are
    /// not represented by this type; they are routed to the unrecognized-type-discriminator policy.
    /// </summary>
    public sealed class AvroPayloadSchemaException : AvroSerializationException
    {
        public AvroPayloadSchemaException()
        {
        }

        public AvroPayloadSchemaException(string? message)
            : base(message)
        {
        }

        public AvroPayloadSchemaException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
