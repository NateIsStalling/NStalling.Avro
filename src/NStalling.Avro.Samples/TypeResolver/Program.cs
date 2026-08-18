using System.Runtime.Serialization;
using Avro;
using NStalling.Avro;

namespace TypeResolver;

// Exercises IAvroTypeResolver directly, outside of AvroSerializer.Deserialize: given an Avro record
// schema (plus an optional externally supplied version and declared member type), what CLR type would
// NStalling.Avro materialize?
public interface IProfile
{
}

[DataContract(Name = "Profile", Namespace = "Acme.Directory")]
[AvroSchemaVersion("1")]
public sealed class ProfileV1 : IProfile
{
}

[DataContract(Name = "Profile", Namespace = "Acme.Directory")]
[AvroSchemaVersion("2")]
public sealed class ProfileV2 : IProfile
{
}

// Registered, but does not implement IProfile -- used to demonstrate declared-type incompatibility.
[DataContract(Name = "Address", Namespace = "Acme.Directory")]
public sealed class Address
{
}

internal static class Program
{
    private const string ProfileSchemaJson =
        @"{""type"":""record"",""name"":""Profile"",""namespace"":""Acme.Directory"",""fields"":[]}";

    private const string AddressSchemaJson =
        @"{""type"":""record"",""name"":""Address"",""namespace"":""Acme.Directory"",""fields"":[]}";

    private const string UnknownSchemaJson =
        @"{""type"":""record"",""name"":""Unknown"",""namespace"":""Acme.Directory"",""fields"":[]}";

    private static void Main()
    {
        var profileSchema = (RecordSchema)Schema.Parse(ProfileSchemaJson);
        var addressSchema = (RecordSchema)Schema.Parse(AddressSchemaJson);
        var unknownSchema = (RecordSchema)Schema.Parse(UnknownSchemaJson);

        var config = new AvroOptions()
            .Types(t => t.Add<ProfileV1>().Add<ProfileV2>().Add<Address>())
            .Build();
        var resolver = config.Resolver;

        // Exact (fullName, version) match: two CLR types share the Avro name "Profile", selected by
        // the externally supplied version.
        Console.WriteLine($"Resolve(Profile, v1) -> {resolver.Resolve(profileSchema, typeof(IProfile), "1").Name}");
        Console.WriteLine($"Resolve(Profile, v2) -> {resolver.Resolve(profileSchema, typeof(IProfile), "2").Name}");

        // Declared-type incompatibility: Address is registered, but isn't assignable to IProfile.
        try
        {
            resolver.Resolve(addressSchema, typeof(IProfile));
        }
        catch (AvroTypeResolutionException ex)
        {
            Console.WriteLine($"Resolve(Address, declaredType=IProfile) threw: {ex.Message}");
        }

        // Versioned mappings exist for "Profile", but not for version "3" -- resolution fails rather
        // than silently falling back to an unqualified or differently versioned mapping.
        var missingVersion = resolver.TryResolve(profileSchema, null, "3", out _);
        Console.WriteLine($"TryResolve(Profile, v3) -> {missingVersion}");

        // No mapping at all for "Unknown": TryResolve reports absence instead of throwing.
        var found = resolver.TryResolve(unknownSchema, null, null, out var unknownType);
        Console.WriteLine($"TryResolve(Unknown) -> {found}, type={unknownType?.Name ?? "null"}");

        // ResolveOrDefault mirrors TryResolve for absence, but still throws on ambiguity or a declared-
        // type conflict -- absence is optional, contradiction is not.
        var defaulted = resolver.ResolveOrDefault(unknownSchema);
        Console.WriteLine($"ResolveOrDefault(Unknown) -> {defaulted?.Name ?? "null"}");
    }
}
