using Avro;
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro.Tests.Contracts;
using NStalling.Avro.Tests.Models;
using CustomerId = NStalling.Avro.Tests.Messages.CustomerId;

namespace NStalling.Avro.Tests;

public class DefaultAvroTypeResolverTests
{
    [Fact]
    public void Resolve_UsesDataContractNameAndNamespace()
    {
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Add<CustomerCreated>()
                .Build());

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "CustomerCreated",
                                                  "namespace": "Acme.Events",
                                                  "fields": [
                                                    {"name": "customer_id", "type": "string"}
                                                  ]
                                                }
                                                """);

        var type = resolver.Resolve(schema);

        Assert.Equal(typeof(CustomerCreated), type);
    }

    [Fact]
    public void ResolveOrDefault_ReturnsNull_ForUnknownSchema()
    {
        var resolver = new DefaultAvroTypeResolver(AvroTypeRegistry.Empty);

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "Missing",
                                                  "namespace": "Acme",
                                                  "fields": []
                                                }
                                                """);

        var resolved = resolver.ResolveOrDefault(schema);

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_Throws_ForAmbiguousSimpleName()
    {
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Add<Models.CustomerId>()
                .Add<CustomerId>()
                .Build());

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "Customer",
                                                  "fields": []
                                                }
                                                """);

        Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(schema));
    }

    [Fact]
    public void Resolve_UsesVersionQualifiedMapping_WhenConfigured()
    {
        var schemaName = new AvroSchemaName("Customer", "Acme.Events");
        var resolver = new DefaultAvroTypeResolver(
            AvroTypeRegistryBuilder.CreateDefault()
                .Map<CustomerVersion1>(schemaName, "1")
                .Map<CustomerVersion2>(schemaName, "2")
                .Build());

        var schemaV1 = (RecordSchema)Schema.Parse("""
                                                  {
                                                    "type": "record",
                                                    "name": "Customer",
                                                    "namespace": "Acme.Events",
                                                    "fields": []
                                                  }
                                                  """);

        var schemaV2 = (RecordSchema)Schema.Parse("""
                                                  {
                                                    "type": "record",
                                                    "name": "Customer",
                                                    "namespace": "Acme.Events",
                                                    "fields": []
                                                  }
                                                  """);

        var v1 = resolver.Resolve(schemaV1, schemaVersion: "1");
        var v2 = resolver.Resolve(schemaV2, schemaVersion: "2");

        Assert.Equal(typeof(CustomerVersion1), v1);
        Assert.Equal(typeof(CustomerVersion2), v2);
    }

    [Fact]
    public void SchemaResolver_GeneratesRecursiveRecord()
    {
        var resolver = new DefaultAvroSchemaResolver();

        var schema = resolver.Resolve(typeof(Node));

        Assert.IsType<RecordSchema>(schema);
        var avsc = schema.ToString();
        Assert.Contains("\"Node\"", avsc, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaResolver_NormalizesConfiguredUnionBranches()
    {
        var unionConfig = new AvroUnionConfigurationBuilder()
            .For<NullableUnionEnvelope>(envelope => envelope
                .Member(x => x.Value)
                .Union(union => union
                    .Add<int?>()
                    .Add<string>()))
            .Build();

        var resolver = new DefaultAvroSchemaResolver(unionConfig);

        var schema = resolver.Resolve(typeof(NullableUnionEnvelope));
        var avsc = schema.ToString();

        Assert.Contains("\"Value\"", avsc, StringComparison.Ordinal);
        Assert.Contains("[\"null\",\"int\",\"string\"]", avsc, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaResolver_Throws_ForInvalidConfiguredArrayUnion()
    {
        var unionConfig = new AvroUnionConfigurationBuilder()
            .For<InvalidUnionEnvelope>(envelope => envelope
                .Member(x => x.Payload)
                .Union(union => union
                    .Add<List<string>>()
                    .Add<List<int>>()))
            .Build();

        var resolver = new DefaultAvroSchemaResolver(unionConfig);

        Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(typeof(InvalidUnionEnvelope)));
    }


    [Fact]
    public void AddAvro_RegistersResolvers_WithConfiguredMappings()
    {
        var services = new ServiceCollection();
        services.AddAvro(options => options
            .Types(types => types.Map<CustomerV1>(new AvroSchemaName("Customer", "Acme.Events")))
            .Union<IPayment>(union => union
                .Add<CardPayment>()
                .Add<WirePayment>()));

        using var provider = services.BuildServiceProvider();

        var typeResolver = provider.GetRequiredService<IAvroTypeResolver>();
        var schemaResolver = provider.GetRequiredService<IAvroSchemaResolver>();

        var schema = (RecordSchema)Schema.Parse("""
                                                {
                                                  "type": "record",
                                                  "name": "Customer",
                                                  "namespace": "Acme.Events",
                                                  "fields": []
                                                }
                                                """);

        var type = typeResolver.Resolve(schema);
        var orderSchema = schemaResolver.Resolve(typeof(Order));

        Assert.Equal(typeof(CustomerV1), type);
        Assert.NotNull(orderSchema);
    }
}