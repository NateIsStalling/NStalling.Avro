using System;

namespace NStalling.Avro
{
    /// <summary>
    /// Marks a member whose decoded value can help locate an inner payload schema when the outer Avro
    /// schema cannot discriminate. It supplies type/schema identity context for the value-directed path;
    /// it never directly names or loads a CLR type.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Property | AttributeTargets.Field,
        AllowMultiple = false,
        Inherited = true)]
    public sealed class AvroTypeDiscriminatorAttribute : Attribute
    {
    }
}
