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
        if (schema is null) throw new ArgumentNullException(nameof(schema));

        using var stream = new MemoryStream();
        var writer = new ReflectWriter<T>(schema, new ClassCache());
        var encoder = new BinaryEncoder(stream);
        writer.Write(value, encoder);
        return stream.ToArray();
    }

    public static T Deserialize<T>(byte[] payload, Schema writerSchema, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        if (payload is null) throw new ArgumentNullException(nameof(payload));
        if (writerSchema is null) throw new ArgumentNullException(nameof(writerSchema));

        var resolvedReaderSchema = readerSchema ?? (schemaResolver ?? DefaultSchemaResolver).Resolve<T>();
        var reader = new ReflectReader<T>(writerSchema, resolvedReaderSchema, new ClassCache());

        using var stream = new MemoryStream(payload);
        var decoder = new BinaryDecoder(stream);
        return reader.Read(decoder);
    }

    public static object Deserialize(byte[] payload, Schema writerSchema, Type type, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));

        var method = typeof(AvroSerializer)
            .GetMethod(nameof(DeserializeCore), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(type);

        return method.Invoke(null, new object?[] { payload, writerSchema, readerSchema, schemaResolver })!;
    }

    public static object Deserialize(byte[] payload, Schema writerSchema, IAvroTypeResolver typeResolver, Schema? readerSchema = null, IAvroSchemaResolver? schemaResolver = null)
    {
        if (typeResolver is null) throw new ArgumentNullException(nameof(typeResolver));
        var type = typeResolver.Resolve(writerSchema);
        return Deserialize(payload, writerSchema, type, readerSchema, schemaResolver);
    }

    private static object DeserializeCore<T>(byte[] payload, Schema writerSchema, Schema? readerSchema, IAvroSchemaResolver? schemaResolver)
    {
        return Deserialize<T>(payload, writerSchema, readerSchema, schemaResolver)!;
    }
}

