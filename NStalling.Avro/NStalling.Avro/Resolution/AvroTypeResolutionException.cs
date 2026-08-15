using System;
using NStalling.Avro.Serialization;

namespace NStalling.Avro.Resolution
{
    /// <summary>
    /// Thrown when NStalling.Avro cannot determine a single valid CLR type for a record schema:
    /// no mapping, ambiguous mapping, declared-type incompatibility, or unknown/missing type identity
    /// under <c>Fail</c> handling.
    /// </summary>
    public sealed class AvroTypeResolutionException : AvroSerializationException
    {
        public AvroTypeResolutionException()
        {
        }

        public AvroTypeResolutionException(string? message)
            : base(message)
        {
        }

        public AvroTypeResolutionException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
