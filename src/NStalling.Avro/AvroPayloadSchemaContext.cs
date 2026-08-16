using System;

namespace NStalling.Avro
{
    /// <summary>
    /// Minimal, evidence-driven context handed to an <see cref="IAvroPayloadSchemaSource"/> so a
    /// caller can locate the inner writer schema for an opaque payload. It never carries a CLR type name
    /// to load and the source must not use it to fetch a registry, generate a schema from a CLR type, or
    /// resolve arbitrary CLR types.
    /// </summary>
    public sealed class AvroPayloadSchemaContext
    {
        public AvroPayloadSchemaContext(
            string? typeDiscriminator,
            string? versionDiscriminator,
            Type parentType,
            string memberName,
            Type declaredMemberType)
        {
            ParentType = parentType ?? throw new ArgumentNullException(nameof(parentType));
            MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
            DeclaredMemberType = declaredMemberType ?? throw new ArgumentNullException(nameof(declaredMemberType));
            TypeDiscriminator = typeDiscriminator;
            VersionDiscriminator = versionDiscriminator;
        }

        /// <summary>Decoded type discriminator value, when present.</summary>
        public string? TypeDiscriminator { get; }

        /// <summary>Decoded version discriminator value, when present.</summary>
        public string? VersionDiscriminator { get; }

        /// <summary>CLR type of the outer object containing the polymorphic member.</summary>
        public Type ParentType { get; }

        /// <summary>Name of the polymorphic member.</summary>
        public string MemberName { get; }

        /// <summary>Declared CLR type of the polymorphic member.</summary>
        public Type DeclaredMemberType { get; }
    }
}
