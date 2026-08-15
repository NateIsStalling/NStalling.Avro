using System;
using NStalling.Avro.Polymorphism;
using NStalling.Avro.Resolution;
using NStalling.Avro.Serialization;

namespace NStalling.Avro.Configuration
{
    /// <summary>
    /// Immutable, concurrency-safe runtime configuration compiled from <see cref="AvroOptions"/>. Exposes
    /// the resolver and a serializer wired with the compiled polymorphic bindings.
    /// </summary>
    public sealed class AvroConfiguration
    {
        internal AvroConfiguration(AvroTypeResolver resolver, AvroSerializer serializer)
        {
            Resolver = resolver;
            Serializer = serializer;
        }

        /// <summary>The immutable type resolver.</summary>
        public IAvroTypeResolver Resolver { get; }

        /// <summary>A serializer wired with the compiled polymorphic bindings.</summary>
        public AvroSerializer Serializer { get; }

        internal static AvroConfiguration Compile(AvroOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var resolver = options.Registry.BuildResolver();
            var bindings = PolymorphicBindingFactory.BuildAll(options, resolver);
            var provider = new PolymorphicBindingRegistry(bindings);
            var serializer = new AvroSerializer(resolver, provider);
            return new AvroConfiguration(resolver, serializer);
        }
    }
}
