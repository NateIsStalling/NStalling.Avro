using System;

namespace NStalling.Avro;

public sealed class AvroTypeResolutionException : InvalidOperationException
{
    public AvroTypeResolutionException(string message) : base(message)
    {
    }
}

