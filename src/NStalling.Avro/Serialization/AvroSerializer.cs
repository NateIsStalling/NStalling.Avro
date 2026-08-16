using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Avro;
using Avro.IO;
using Avro.Reflect;
using NStalling.Avro.Provider;

namespace NStalling.Avro.Serialization
{
    /// <summary>
    /// Thin convenience layer over Apache's reflection reader. It removes the plumbing of building a
    /// version-isolated <see cref="ClassCache"/> and constructing a <see cref="ReflectReader{T}"/>, while
    /// leaving all Avro encoding/decoding semantics to Apache. It intentionally exposes no binding
    /// abstraction and is not modeled around event envelopes.
    /// </summary>
    public sealed class AvroSerializer
    {
        private readonly IAvroTypeResolver _resolver;
        private readonly ApacheReflectionAdapter _adapter;
        private readonly IPolymorphicBindingProvider? _bindingProvider;
        private readonly ValueDirectedPayloadBinder? _binder;
        private static readonly ConcurrentDictionary<Type, (ConstructorInfo Ctor, MethodInfo Read)> ReaderApi = new();

        public AvroSerializer(IAvroTypeResolver resolver)
            : this(resolver, null)
        {
        }

        internal AvroSerializer(IAvroTypeResolver resolver, IPolymorphicBindingProvider? bindingProvider)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _adapter = new ApacheReflectionAdapter(resolver);
            _bindingProvider = bindingProvider;
            _binder = bindingProvider is null ? null : new ValueDirectedPayloadBinder(resolver, this);
        }

        internal IAvroTypeResolver Resolver => _resolver;

        /// <summary>
        /// Second-pass decode of an isolated payload buffer into an already-resolved CLR type. Decode
        /// failures are classified as <see cref="AvroPayloadDecodeException"/>; resolution/configuration
        /// failures for nested members propagate unchanged.
        /// </summary>
        internal object DecodeInnerPayload(byte[] payload, Schema innerWriterSchema, Type type, string? schemaVersion, string? path)
        {
            try
            {
                return ReadCore(payload, innerWriterSchema, innerWriterSchema, type, schemaVersion);
            }
            catch (Exception ex) when (ex is not AvroSerializationException and not AvroConfigurationException and not OperationCanceledException)
            {
                throw new AvroPayloadDecodeException(
                    $"Failed to decode the isolated payload for '{path ?? type.Name}'.", ex)
                {
                    Path = path,
                    SchemaFullName = (innerWriterSchema as NamedSchema)?.Fullname,
                    SchemaVersion = schemaVersion
                };
            }
        }

        /// <summary>Deserializes into <typeparamref name="T"/> using a single writer schema.</summary>
        public T Deserialize<T>(ReadOnlySpan<byte> data, Schema writerSchema)
            => (T)Deserialize(data, writerSchema, typeof(T), null);

        /// <summary>
        /// Deserializes into <typeparamref name="T"/> using a single writer schema and an externally
        /// supplied root schema version.
        /// </summary>
        public T Deserialize<T>(ReadOnlySpan<byte> data, Schema writerSchema, string? schemaVersion)
            => (T)Deserialize(data, writerSchema, typeof(T), schemaVersion);

        /// <summary>
        /// Deserializes using distinct writer and reader schemas. Apache performs schema resolution
        /// between them; no independently configured reader schema is implied by the single-schema
        /// overloads.
        /// </summary>
        public T Deserialize<T>(ReadOnlySpan<byte> data, Schema writerSchema, Schema readerSchema)
            => (T)Deserialize(data, writerSchema, readerSchema, typeof(T), null);

        /// <summary>Deserializes into <paramref name="type"/> using a single writer schema.</summary>
        public object Deserialize(ReadOnlySpan<byte> data, Schema writerSchema, Type type, string? schemaVersion = null)
            => Deserialize(data, writerSchema, writerSchema, type, schemaVersion);

        /// <summary>
        /// Deserializes a record whose writer schema is known, using an externally supplied version.
        /// </summary>
        public object Deserialize(ReadOnlySpan<byte> data, RecordSchema writerSchema, string? schemaVersion = null)
        {
            if (writerSchema is null)
            {
                throw new ArgumentNullException(nameof(writerSchema));
            }

            // Resolve the root type from the schema itself using the supplied version context.
            var rootType = _resolver.Resolve(writerSchema, null, schemaVersion);
            return Deserialize(data, writerSchema, writerSchema, rootType, schemaVersion);
        }

        private object Deserialize(
            ReadOnlySpan<byte> data,
            Schema writerSchema,
            Schema readerSchema,
            Type type,
            string? schemaVersion)
        {
            if (writerSchema is null)
            {
                throw new ArgumentNullException(nameof(writerSchema));
            }

            if (readerSchema is null)
            {
                throw new ArgumentNullException(nameof(readerSchema));
            }

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            // Cache construction may throw AvroTypeResolutionException / AvroConfigurationException; those
            // are NStalling.Avro contract exceptions and are allowed to propagate unchanged.
            var buffer = data.ToArray();
            object result;
            try
            {
                result = ReadCore(buffer, writerSchema, readerSchema, type, schemaVersion);
            }
            catch (Exception ex) when (ex is not AvroSerializationException and not OperationCanceledException)
            {
                throw Wrap(ex, writerSchema, schemaVersion);
            }

            ApplyPolymorphicBindings(result, type, schemaVersion);
            return result;
        }

        private void ApplyPolymorphicBindings(object outer, Type type, string? schemaVersion)
        {
            if (_binder is null || _bindingProvider is null)
            {
                return;
            }

            if (!_bindingProvider.TryGetBindings(type, out var bindings))
            {
                return;
            }

            foreach (var binding in bindings)
            {
                _binder.Bind(outer, binding, schemaVersion);
            }
        }

        /// <summary>
        /// Builds a version-isolated cache and performs the Apache reflection read, unwrapping reflection
        /// invocation exceptions but not classifying failures. Callers apply their own wrapping policy.
        /// </summary>
        internal object ReadCore(byte[] buffer, Schema writerSchema, Schema readerSchema, Type type, string? schemaVersion)
        {
            var cache = _adapter.BuildClassCache(readerSchema, type, schemaVersion);

            var (ctor, read) = ReaderApi.GetOrAdd(type, static t =>
            {
                var readerType = typeof(ReflectReader<>).MakeGenericType(t);
                var c = readerType.GetConstructor(new[] { typeof(Schema), typeof(Schema), typeof(ClassCache) })!;
                var r = readerType.GetMethod("Read", new[] { typeof(Decoder) })!;
                return (c, r);
            });

            object reader = ctor.Invoke(new object[] { writerSchema, readerSchema, cache });

            try
            {
                using var stream = new MemoryStream(buffer, writable: false);
                var decoder = new BinaryDecoder(stream);
                return read.Invoke(reader, new object[] { decoder })!;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw; // unreachable
            }
        }

        private static Exception Wrap(Exception inner, Schema writerSchema, string? schemaVersion)
        {
            if (inner is AvroSerializationException)
            {
                return inner;
            }

            if (inner is OperationCanceledException)
            {
                return inner;
            }

            return new AvroSerializationException(
                $"Failed to deserialize Avro data for schema '{writerSchema.Name}'.", inner)
            {
                SchemaFullName = (writerSchema as NamedSchema)?.Fullname,
                SchemaVersion = schemaVersion
            };
        }
    }
}
