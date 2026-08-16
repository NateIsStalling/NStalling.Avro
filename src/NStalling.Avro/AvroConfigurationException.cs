using System;

namespace NStalling.Avro
{
    /// <summary>
    /// Thrown before deserialization for deterministic configuration defects such as duplicate
    /// equal-precedence mappings, structurally invalid discriminator paths, ambiguous marker
    /// attributes, or invalid combinations of polymorphism options.
    /// This is deliberately not part of the <see cref="Serialization.AvroSerializationException"/>
    /// hierarchy because deserialization has not begun.
    /// </summary>
    public sealed class AvroConfigurationException : Exception
    {
        public AvroConfigurationException()
        {
        }

        public AvroConfigurationException(string? message)
            : base(message)
        {
        }

        public AvroConfigurationException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
