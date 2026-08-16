using System;

namespace NStalling.Avro.Serialization
{
    /// <summary>
    /// Base type for failures that occur inside the NStalling.Avro materialization/deserialization
    /// pipeline. Ordinary .NET API-contract failures (argument validation, cancellation, disposal)
    /// are intentionally not represented by this hierarchy.
    /// </summary>
    public class AvroSerializationException : Exception
    {
        public AvroSerializationException()
        {
        }

        public AvroSerializationException(string? message)
            : base(message)
        {
        }

        public AvroSerializationException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }

        /// <summary>Logical member path to the failing value, e.g. <c>EventEnvelope.Payload</c>.</summary>
        public string? Path { get; init; }

        /// <summary>Avro record full name involved in the failure, when known.</summary>
        public string? SchemaFullName { get; init; }

        /// <summary>Effective schema version context involved in the failure, when known.</summary>
        public string? SchemaVersion { get; init; }

        /// <summary>Configured discriminator path that produced the failure, when applicable.</summary>
        public string? DiscriminatorPath { get; init; }

        /// <summary>Decoded discriminator value involved in the failure, when present.</summary>
        public string? DiscriminatorValue { get; init; }
    }
}
