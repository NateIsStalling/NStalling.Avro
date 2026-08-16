using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using Avro;
using Avro.IO;
using Avro.Reflect;
using NStalling.Avro;

namespace Annotations;

// Two CLR types share a single Avro record identity ("Acme.Directory.Profile") but represent different
// schema versions. [AvroSchemaVersion] supplies only the version qualifier; the Avro name comes from
// [DataContract]. The value-directed version discriminator picks which CLR type to materialize.
public interface IProfile
{
    string Describe();
}

[DataContract(Name = "Profile", Namespace = "Acme.Directory")]
[AvroSchemaVersion("1")]
public sealed class LegacyProfile : IProfile
{
    public string Name { get; init; } = "";

    public string Describe() => $"LegacyProfile(Name={Name})";
}

[DataContract(Name = "Profile", Namespace = "Acme.Directory")]
[AvroSchemaVersion("2")]
public sealed class CurrentProfile : IProfile
{
    public string Name { get; init; } = "";

    public string Describe() => $"CurrentProfile(Name={Name})";
}

// The envelope is configured entirely through annotations:
//  - [AvroTypeDiscriminator]    marks the member carrying type identity for the opaque payload.
//  - [AvroVersionDiscriminator] marks the member carrying runtime schema-version context.
//  - [AvroPolymorphic]          marks the opaque payload member to materialize in a second pass.
// Only the payload schema source (inherently code, not metadata) is supplied via configuration.
public sealed class ProfileEnvelope
{
    public string EventId { get; init; } = "";

    [AvroTypeDiscriminator]
    public string PayloadType { get; init; } = "";

    [AvroVersionDiscriminator]
    public string? PayloadVersion { get; init; }

    [AvroPolymorphic]
    public object Payload { get; set; } = null!;
}

internal static class Program
{
    private const string ProfileSchemaJson = @"{
      ""type"":""record"",""name"":""Profile"",""namespace"":""Acme.Directory"",
      ""fields"":[{""name"":""Name"",""type"":""string""}]}";

    private const string EnvelopeSchemaJson = @"{
      ""type"":""record"",""name"":""ProfileEnvelope"",""namespace"":""Acme.Directory"",
      ""fields"":[
        {""name"":""EventId"",""type"":""string""},
        {""name"":""PayloadType"",""type"":""string""},
        {""name"":""PayloadVersion"",""type"":[""null"",""string""]},
        {""name"":""Payload"",""type"":""bytes""}
      ]}";

    private static void Main()
    {
        var profileSchema = (RecordSchema)Schema.Parse(ProfileSchemaJson);
        var envelopeSchema = (RecordSchema)Schema.Parse(EnvelopeSchemaJson);

        // Discriminators are discovered from annotations; only the schema source is configured in code.
        var config = new AvroOptions()
            .Types(t => t.Add<LegacyProfile>().Add<CurrentProfile>())
            .Polymorphic<ProfileEnvelope>(p => p
                .Member(e => e.Payload)
                .PayloadSchema(new ProfileSchemaSource(profileSchema)))
            .Build();

        // Identical inner payload bytes; only the version discriminator differs on the wire.
        var innerBytes = WriteProfile(profileSchema, new CurrentProfile { Name = "Ada" });

        var v1 = WriteEnvelope(envelopeSchema,
            new EnvelopeWire { EventId = "evt-1", PayloadType = "Profile", PayloadVersion = "1", Payload = innerBytes });
        var v2 = WriteEnvelope(envelopeSchema,
            new EnvelopeWire { EventId = "evt-2", PayloadType = "Profile", PayloadVersion = "2", Payload = innerBytes });

        foreach (var bytes in new[] { v1, v2 })
        {
            var envelope = config.Serializer.Deserialize<ProfileEnvelope>(bytes, envelopeSchema);
            var profile = (IProfile)envelope.Payload;
            Console.WriteLine($"{envelope.EventId} version={envelope.PayloadVersion} -> {profile.Describe()}");
        }
    }

    // Supplies the inner writer schema for the opaque payload. It maps a discriminator to an Avro schema
    // only; it never names or loads a CLR type (that remains the resolver's job).
    private sealed class ProfileSchemaSource : IAvroPayloadSchemaSource
    {
        private readonly Schema _profileSchema;

        public ProfileSchemaSource(Schema profileSchema) => _profileSchema = profileSchema;

        public bool TryGetWriterSchema(AvroPayloadSchemaContext context, [NotNullWhen(true)] out Schema? schema)
        {
            if (context.TypeDiscriminator == "Profile")
            {
                schema = _profileSchema;
                return true;
            }

            schema = null;
            return false;
        }
    }

    // Writer-side envelope DTO whose opaque payload is a raw byte buffer for the Avro `bytes` field.
    private sealed class EnvelopeWire
    {
        public string EventId { get; init; } = "";

        public string PayloadType { get; init; } = "";

        public string? PayloadVersion { get; init; }

        public byte[] Payload { get; init; } = Array.Empty<byte>();
    }

    private static byte[] WriteProfile(RecordSchema schema, CurrentProfile profile)
    {
        var cache = new ClassCache();
        cache.LoadClassCache(typeof(CurrentProfile), schema);
        var writer = new ReflectWriter<CurrentProfile>(schema, cache);
        using var stream = new MemoryStream();
        writer.Write(profile, new BinaryEncoder(stream));
        return stream.ToArray();
    }

    private static byte[] WriteEnvelope(RecordSchema schema, EnvelopeWire value)
    {
        var cache = new ClassCache();
        cache.LoadClassCache(typeof(EnvelopeWire), schema);
        var writer = new ReflectWriter<EnvelopeWire>(schema, cache);
        using var stream = new MemoryStream();
        writer.Write(value, new BinaryEncoder(stream));
        return stream.ToArray();
    }
}
