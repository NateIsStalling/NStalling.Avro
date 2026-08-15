using System;

namespace NStalling.Avro.Serialization
{
    /// <summary>
    /// Thrown when the second-pass Apache decode fails after both the payload schema and CLR type have
    /// been identified: truncated/corrupt bytes, payload-vs-schema mismatch, or Apache reflection
    /// materialization failure during the inner decode.
    /// </summary>
    public sealed class AvroPayloadDecodeException : AvroSerializationException
    {
        public AvroPayloadDecodeException()
        {
        }

        public AvroPayloadDecodeException(string? message)
            : base(message)
        {
        }

        public AvroPayloadDecodeException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
