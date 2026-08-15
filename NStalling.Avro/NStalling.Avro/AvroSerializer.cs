using System;
using System.IO;
using Avro;
using Avro.IO;
using Avro.Reflect;

namespace NStalling.Avro;

public static class AvroSerializer
{
    private static readonly IAvroSchemaResolver DefaultSchemaResolver = new DefaultAvroSchemaResolver();

    public static byte[] Serialize<T>(T value, IAvroSchemaResolver? schemaResolver = null)
    {
        var resolver = schemaResolver ?? DefaultSchemaResolver;
        var schema = resolver.Resolve<T>();
        return Serialize(value, schema);
    }

    public static byte[] Serialize<T>(T value, Schema schema)
    {
        return Serialize(value, schema, typeResolver: null);
    }

    public static byte[] Serialize<T>(T value, Schema schema, IAvroTypeResolver? typeResolver, string? schemaVersion = null)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));

        var classCache = new ClassCache();
        if (typeResolver is not null)
        {
            var adapter = new ApacheReflectionAdapter(typeResolver, classCache);
            adapter.PrepareSchema(schema, declaredType: typeof(T), schemaVersion: schemaVersion);
        }

        using var stream = new MemoryStream();
        var writer = new ReflectWriter<T>(schema, classCache);
        var encoder = new BinaryEncoder(stream);
        writer.Write(value, encoder);
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[] payload, Schema writerSchema, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        return Deserialize<T>(payload, writerSchema, typeResolver: null, readerSchema, schemaResolver);
    }

    public static T Deserialize<T>(
        byte[] payload,
        Schema writerSchema,
        IAvroTypeResolver? typeResolver,
        Schema? readerSchema = null,
        IAvroSchemaResolver? schemaResolver = null,
        string? schemaVersion = null)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (writerSchema is null) throw new ArgumentNullException(nameof(writerSchema));

        var resolvedReaderSchema = readerSchema ?? (schemaResolver ?? DefaultSchemaResolver).Resolve<T>();
        var classCache = new ClassCache();
        if (typeResolver is not null)
        {
            var adapter = new ApacheReflectionAdapter(typeResolver, classCache);
            adapter.PrepareSchema(writerSchema, declaredType: typeof(T), schemaVersion: schemaVersion);
            adapter.PrepareSchema(resolvedReaderSchema, declaredType: typeof(T), schemaVersion: schemaVersion);
        }

        var reader = new ReflectReader<T>(writerSchema, resolvedReaderSchema, classCache);

        using var stream = new MemoryStream(payload);
        var decoder = new BinaryDecoder(stream);
        return reader.Read(decoder);
    }

    /// <summary>
    /// Deserializes Avro binary data using a runtime-determined CLR type.
    /// </summary>
    public static object Deserialize(byte[] payload, Schema writerSchema, Type type, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));

        var method = typeof(AvroSerializer)
            .GetMethod(nameof(DeserializeCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(type);

        return method.Invoke(null, new object?[] { payload, writerSchema, readerSchema, schemaResolver })!;
    }

    /// <summary>
    /// Deserializes Avro binary data, using an IAvroTypeResolver to determine the CLR type
    /// from the writer schema.
    /// 
    /// The resolver must be able to resolve NamedSchema instances from the writer schema.
    /// If the writer schema is not a NamedSchema, this method will throw.
    /// </summary>
    public static object Deserialize(byte[] payload, Schema writerSchema, IAvroTypeResolver typeResolver, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        if (typeResolver is null) throw new ArgumentNullException(nameof(typeResolver));
        if (writerSchema is null) throw new ArgumentNullException(nameof(writerSchema));

        if (writerSchema is not NamedSchema namedSchema)
        {
            throw new ArgumentException($"Writer schema must be a NamedSchema for type resolution. Got {writerSchema.Tag}.", nameof(writerSchema));
        }

        var type = typeResolver.Resolve(namedSchema);
        return Deserialize(payload, writerSchema, type, readerSchema, schemaResolver);
    }

    /// <summary>
    /// Deserializes Avro binary data, using an IAvroTypeResolver to determine the CLR type
    /// from the writer schema with an optional declared type constraint.
    /// 
    /// declaredType is useful for properties declared as object, interface, or abstract class.
    /// </summary>
    public static object Deserialize(
        byte[] payload,
        Schema writerSchema,
        Type? declaredType,
        IAvroTypeResolver typeResolver,
        Schema? readerSchema = null,
        IAvroSchemaResolver? schemaResolver = null)
    {
        if (typeResolver is null) throw new ArgumentNullException(nameof(typeResolver));
        if (writerSchema is null) throw new ArgumentNullException(nameof(writerSchema));

        if (writerSchema is not NamedSchema namedSchema)
        {
            throw new ArgumentException($"Writer schema must be a NamedSchema for type resolution. Got {writerSchema.Tag}.", nameof(writerSchema));
        }

        var type = typeResolver.Resolve(namedSchema, declaredType);
        return Deserialize(payload, writerSchema, type, readerSchema, schemaResolver);
    }

    private static object DeserializeCore<T>(byte[] payload, Schema writerSchema, Schema? readerSchema, IAvroSchemaResolver? schemaResolver)
    {
        return Deserialize<T>(payload, writerSchema, readerSchema, schemaResolver)!;
    }
}




