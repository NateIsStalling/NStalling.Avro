using Avro;
using NStalling.Avro;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Resolution
{
    public class AvroTypeResolverTests
    {
        private static RecordSchema Record(string name, string ns) =>
            (RecordSchema)Schema.Parse($@"{{""type"":""record"",""name"":""{name}"",""namespace"":""{ns}"",""fields"":[]}}");

        [Fact]
        public void ExplicitUnqualifiedMapping_Resolves()
        {
            var resolver = new AvroTypeRegistry()
                .Map<CustomerCreated>("CustomerCreated", "Acme.Events")
                .BuildResolver();

            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(Record("CustomerCreated", "Acme.Events")));
        }

        [Fact]
        public void DataContractMapping_Resolves()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(Record("CustomerCreated", "Acme.Events")));
        }

        [Fact]
        public void ClrFullNameMapping_Resolves()
        {
            var resolver = new AvroTypeRegistry().Add<PlainClrType>().BuildResolver();
            var schema = Record("PlainClrType", "NStalling.Avro.Tests.Resolution");
            Assert.Equal(typeof(PlainClrType), resolver.Resolve(schema));
        }

        [Fact]
        public void NoCandidate_Throws_ReturnsFalse_ReturnsNull()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var schema = Record("Missing", "Acme.Events");

            Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(schema));
            Assert.False(resolver.TryResolve(schema, null, null, out _));
            Assert.Null(resolver.ResolveOrDefault(schema));
        }

        [Fact]
        public void DeclaredTypeCompatible_Resolves()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var schema = Record("CustomerCreated", "Acme.Events");
            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(schema, typeof(IEvent)));
        }

        [Fact]
        public void DeclaredTypeIncompatible_Fails()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var schema = Record("CustomerCreated", "Acme.Events");

            Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(schema, typeof(ICustomer)));
            Assert.False(resolver.TryResolve(schema, typeof(ICustomer), null, out _));
            Assert.Throws<AvroTypeResolutionException>(() => resolver.ResolveOrDefault(schema, typeof(ICustomer)));
        }

        [Fact]
        public void ObjectDeclaredType_IsUnrestricted()
        {
            var resolver = new AvroTypeRegistry().Add<CustomerCreated>().BuildResolver();
            var schema = Record("CustomerCreated", "Acme.Events");
            Assert.Equal(typeof(CustomerCreated), resolver.Resolve(schema, typeof(object)));
        }

        [Fact]
        public void ExplicitMapping_OverridesAnnotationDerivedMapping()
        {
            // DataContract derives CustomerCreated -> CustomerCreated (unqualified). Explicit maps the same
            // key to OrderPlaced; explicit precedence wins within the bucket.
            var resolver = new AvroTypeRegistry()
                .Add<CustomerCreated>()
                .Map<OrderPlaced>("CustomerCreated", "Acme.Events")
                .BuildResolver();

            Assert.Equal(typeof(OrderPlaced), resolver.Resolve(Record("CustomerCreated", "Acme.Events")));
        }
    }

    public sealed class PlainClrType
    {
        public string Value { get; set; } = "";
    }
}
