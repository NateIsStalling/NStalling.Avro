using Avro;
using NStalling.Avro.Tests.Models;

namespace NStalling.Avro.Tests;

public class AvroTypeRegistryBuilderTests
{
    [Fact]
    public void Resolve_WithDeclaredType_ValidatesCompatibility()
    {
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Add<CardPayment>()
                .Build());

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "CardPayment",
                                                  "fields": []
                                                }
                                                """);

        // Resolving with a compatible interface should succeed
        var type = resolver.Resolve(schema, typeof(IPayment));
        Assert.Equal(typeof(CardPayment), type);
    }

    [Fact]
    public void Resolve_WithIncompatibleDeclaredType_Throws()
    {
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Add<CardPayment>()
                .Build());

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "CardPayment",
                                                  "fields": []
                                                }
                                                """);

        // Resolving with an incompatible type should throw
        Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(schema, typeof(WirePayment)));
    }

    [Fact]
    public void Resolve_WithObjectDeclaredType_Succeeds()
    {
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Add<CardPayment>()
                .Build());

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "CardPayment",
                                                  "fields": []
                                                }
                                                """);

        // Resolving with object (unrestricted) should always succeed
        var type = resolver.Resolve(schema, typeof(object));
        Assert.Equal(typeof(CardPayment), type);
    }
}