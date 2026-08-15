using System;

namespace NStalling.Avro.Polymorphism
{
    /// <summary>
    /// Marks a decoded member that provides runtime schema-version context. Useful even when type
    /// identity remains schema-directed. It never selects a CLR type by itself.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class AvroVersionDiscriminatorAttribute : Attribute
    {
    }
}
