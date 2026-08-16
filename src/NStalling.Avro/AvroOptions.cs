using System;
using System.Collections.Generic;
using System.Reflection;

namespace NStalling.Avro
{
    /// <summary>
    /// Root configuration surface for NStalling.Avro. Accumulates type registrations, resolution
    /// behavior, polymorphism defaults, and per-type polymorphic member configuration, and compiles them
    /// into an immutable, concurrency-safe runtime configuration.
    /// </summary>
    public sealed class AvroOptions
    {
        private readonly AvroTypeRegistry _registry = new();
        private readonly AvroResolutionOptions _resolution = new();
        private readonly AvroPolymorphismDefaults _polymorphismDefaults = new();

        // type -> (member property -> options)
        private readonly Dictionary<Type, Dictionary<PropertyInfo, PolymorphicMemberOptions>> _polymorphic = new();

        /// <summary>Configures the type registry (explicit mappings and controlled discovery).</summary>
        public AvroOptions Types(Action<AvroTypeRegistry> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(_registry);
            return this;
        }

        /// <summary>Configures global resolution behavior.</summary>
        public AvroOptions Resolution(Action<AvroResolutionOptions> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(_resolution);
            return this;
        }

        /// <summary>Configures global polymorphism defaults.</summary>
        public AvroOptions Polymorphism(Action<AvroPolymorphismDefaults> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            configure(_polymorphismDefaults);
            return this;
        }

        /// <summary>Configures polymorphic members of <typeparamref name="T"/>.</summary>
        public AvroOptions Polymorphic<T>(Action<AvroPolymorphicTypeBuilder<T>> configure)
        {
            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (!_polymorphic.TryGetValue(typeof(T), out var members))
            {
                members = new Dictionary<PropertyInfo, PolymorphicMemberOptions>();
                _polymorphic[typeof(T)] = members;
            }

            configure(new AvroPolymorphicTypeBuilder<T>(members));
            return this;
        }

        internal AvroTypeRegistry Registry => _registry;

        internal AvroResolutionOptions ResolutionOptions => _resolution;

        internal AvroPolymorphismDefaults PolymorphismDefaults => _polymorphismDefaults;

        internal IReadOnlyDictionary<Type, Dictionary<PropertyInfo, PolymorphicMemberOptions>> PolymorphicMembers
            => _polymorphic;

        /// <summary>Compiles the immutable runtime configuration, failing fast on configuration defects.</summary>
        public AvroConfiguration Build() => AvroConfiguration.Compile(this);
    }
}
