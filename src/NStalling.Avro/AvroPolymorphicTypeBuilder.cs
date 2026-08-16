using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace NStalling.Avro
{
    /// <summary>Fluent builder for the polymorphic members of <typeparamref name="T"/>.</summary>
    public sealed class AvroPolymorphicTypeBuilder<T>
    {
        private readonly Dictionary<PropertyInfo, PolymorphicMemberOptions> _members;

        internal AvroPolymorphicTypeBuilder(Dictionary<PropertyInfo, PolymorphicMemberOptions> members)
        {
            _members = members;
        }

        /// <summary>Begins configuration of a polymorphic member.</summary>
        public AvroPolymorphicMemberBuilder<T> Member<TMember>(Expression<Func<T, TMember>> selector)
        {
            var property = ResolveProperty(selector);
            if (!_members.TryGetValue(property, out var options))
            {
                options = new PolymorphicMemberOptions(property);
                _members[property] = options;
            }

            return new AvroPolymorphicMemberBuilder<T>(options);
        }

        private static PropertyInfo ResolveProperty(LambdaExpression selector)
        {
            var body = selector.Body;
            if (body is UnaryExpression { Operand: MemberExpression innerMember })
            {
                body = innerMember;
            }

            if (body is MemberExpression { Member: PropertyInfo property })
            {
                return property;
            }

            throw new AvroConfigurationException("A polymorphic member selector must reference a property.");
        }
    }

    /// <summary>Fluent builder for a single polymorphic member.</summary>
    public sealed class AvroPolymorphicMemberBuilder<T>
    {
        private readonly PolymorphicMemberOptions _options;

        internal AvroPolymorphicMemberBuilder(PolymorphicMemberOptions options)
        {
            _options = options;
        }

        /// <summary>Configures the type discriminator member/path.</summary>
        public AvroPolymorphicMemberBuilder<T> DiscriminateBy<TMember>(Expression<Func<T, TMember>> selector)
        {
            _options.TypeDiscriminatorPath = MemberPath(selector);
            return this;
        }

        /// <summary>Configures the type discriminator by explicit path (supports nested paths).</summary>
        public AvroPolymorphicMemberBuilder<T> DiscriminateBy(string path)
        {
            _options.TypeDiscriminatorPath = path;
            return this;
        }

        /// <summary>Configures the version discriminator member/path.</summary>
        public AvroPolymorphicMemberBuilder<T> VersionBy<TMember>(Expression<Func<T, TMember>> selector)
        {
            _options.VersionDiscriminatorPath = MemberPath(selector);
            return this;
        }

        /// <summary>Configures the version discriminator by explicit path (supports nested paths).</summary>
        public AvroPolymorphicMemberBuilder<T> VersionBy(string path)
        {
            _options.VersionDiscriminatorPath = path;
            return this;
        }

        /// <summary>Supplies the payload schema source for the value-directed path.</summary>
        public AvroPolymorphicMemberBuilder<T> PayloadSchema(IAvroPayloadSchemaSource source)
        {
            _options.PayloadSchemaSource = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        /// <summary>Sets the unrecognized-type-discriminator handling for this member.</summary>
        public AvroPolymorphicMemberBuilder<T> OnUnrecognizedTypeDiscriminator(
            AvroUnrecognizedTypeDiscriminatorHandling handling)
        {
            _options.Handling = handling;
            return this;
        }

        /// <summary>Configures the fallback CLR type used with <c>UseFallbackType</c>.</summary>
        public AvroPolymorphicMemberBuilder<T> FallbackTo<TFallback>()
        {
            _options.FallbackType = typeof(TFallback);
            if (_options.Handling == AvroUnrecognizedTypeDiscriminatorHandling.Unspecified)
            {
                _options.Handling = AvroUnrecognizedTypeDiscriminatorHandling.UseFallbackType;
            }

            return this;
        }

        /// <summary>Explicitly inherits the parent/root schema version for this member.</summary>
        public AvroPolymorphicMemberBuilder<T> InheritSchemaVersion()
        {
            _options.InheritVersion = true;
            return this;
        }

        private static string MemberPath(LambdaExpression selector)
        {
            var body = selector.Body;
            if (body is UnaryExpression { Operand: MemberExpression innerMember })
            {
                body = innerMember;
            }

            if (body is MemberExpression { Member: PropertyInfo property })
            {
                return property.Name;
            }

            throw new AvroConfigurationException("A discriminator selector must reference a property.");
        }
    }
}
