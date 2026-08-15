using System;
using System.IO;
using Avro;
using Avro.IO;
using Avro.Reflect;
using NStalling.Avro.Reflection;
using NStalling.Avro.Resolution;

namespace NStalling.Avro.Tests.Fixtures
{
    /// <summary>
    /// Test-only helper that serializes CLR objects to Avro binary using Apache's reflect writer and a
    /// class cache built by NStalling.Avro's own adapter. This lets integration tests produce wire bytes with
    /// the same mappings the deserialize path uses.
    /// </summary>
    internal static class AvroWriteHelper
    {
        public static byte[] Serialize<T>(T value, Schema writerSchema, IAvroTypeResolver resolver, string? version = null)
        {
            var adapter = new ApacheReflectionAdapter(resolver);
            var cache = adapter.BuildClassCache(writerSchema, typeof(T), version);
            var writer = new ReflectWriter<T>(writerSchema, cache);
            using var stream = new MemoryStream();
            writer.Write(value, new BinaryEncoder(stream));
            return stream.ToArray();
        }

        public static byte[] SerializeBytesField(byte[] payload) => payload;
    }
}
