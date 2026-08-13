using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Avro;
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro;

namespace NStalling.Avro.Tests
{
    public sealed class AvroIntegrationTests
    {
        [Fact]
        public void Resolve_UsesDataContractNameAndNamespace()
        {
            var resolver = new DefaultAvroTypeResolver(
                AvroTypeRegistryBuilder.CreateDefault()
                    .Add<CustomerCreated>()
                    .Build());

            var type = resolver.Resolve(new AvroSchemaName("CustomerCreated", "Acme.Events"));

            Assert.Equal(typeof(CustomerCreated), type);
        }

        [Fact]
        public void ResolveOrDefault_ReturnsNull_ForUnknownSchema()
        {
            var resolver = new DefaultAvroTypeResolver(AvroTypeRegistry.Empty);

            var resolved = resolver.ResolveOrDefault(new AvroSchemaName("Missing", "Acme"));

            Assert.Null(resolved);
        }

        [Fact]
        public void Resolve_Throws_ForAmbiguousSimpleName()
        {
            var resolver = new DefaultAvroTypeResolver(
                AvroTypeRegistryBuilder.CreateDefault()
                    .Add<Contracts.Customer>()
                    .Add<Messages.Customer>()
                    .Build());

            Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(new AvroSchemaName("Customer")));
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

            var v1 = resolver.Resolve(schemaName, "1");
            var v2 = resolver.Resolve(schemaName, "2");

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
        public void SchemaAssistedDeserialize_RoundTripsPoco()
        {
            var avsc = ReadFixture("customer-v1.avsc");
            var schema = Schema.Parse(avsc);

            var original = new CustomerV1
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Ada"
            };

            var bytes = AvroSerializer.Serialize(original, schema);
            var clone = AvroSerializer.Deserialize<CustomerV1>(bytes, schema, schema);

            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.Name, clone.Name);
        }

        [Fact]
        public void DynamicDeserialize_UsesTypeResolver_FromWriterSchema()
        {
            var avsc = ReadFixture("customer-v1.avsc");
            var schema = Schema.Parse(avsc);

            var original = new CustomerV1
            {
                Id = "1",
                Name = "Grace"
            };

            var bytes = AvroSerializer.Serialize(original, schema);

            var typeResolver = new DefaultAvroTypeResolver(
                AvroTypeRegistryBuilder.CreateDefault()
                    .Map<CustomerV1>(new AvroSchemaName("Customer", "Acme.Events"))
                    .Build());

            var deserialized = AvroSerializer.Deserialize(bytes, schema, typeResolver, schema);

            var typed = Assert.IsType<CustomerV1>(deserialized);
            Assert.Equal("1", typed.Id);
            Assert.Equal("Grace", typed.Name);
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

            var type = typeResolver.Resolve(new AvroSchemaName("Customer", "Acme.Events"));
            var schema = schemaResolver.Resolve(typeof(Order));

            Assert.Equal(typeof(CustomerV1), type);
            Assert.NotNull(schema);
        }

        private static string ReadFixture(string fixtureName)
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
            return File.ReadAllText(fixturePath);
        }

        [DataContract(Name = "CustomerCreated", Namespace = "Acme.Events")]
        public sealed class CustomerCreated
        {
            [DataMember(Name = "customer_id", Order = 1)]
            public Guid CustomerId { get; init; }
        }

        public sealed class Node
        {
            public string Name { get; init; } = string.Empty;

            public Node? Parent { get; init; }
        }

        public sealed class CustomerV1
        {
            public string Id { get; init; } = string.Empty;

            public string Name { get; init; } = string.Empty;
        }

        public sealed class CustomerVersion1
        {
            public string Id { get; init; } = string.Empty;
        }

        public sealed class CustomerVersion2
        {
            public string Id { get; init; } = string.Empty;

            public string? Email { get; init; }
        }

        public sealed class NullableUnionEnvelope
        {
            public object? Value { get; init; }
        }

        public sealed class InvalidUnionEnvelope
        {
            public object Payload { get; init; } = new();
        }

        public interface IPayment
        {
        }

        public sealed class CardPayment : IPayment
        {
            public string Last4 { get; init; } = string.Empty;
        }

        public sealed class WirePayment : IPayment
        {
            public string Iban { get; init; } = string.Empty;
        }

        public sealed class Order
        {
            public IPayment? Payment { get; init; }
        }
    }
}

namespace NStalling.Avro.Tests.Contracts
{
    public sealed class Customer
    {
        public string Id { get; init; } = string.Empty;
    }
}

namespace NStalling.Avro.Tests.Messages
{
    public sealed class Customer
    {
        public string Name { get; init; } = string.Empty;
    }
}