using Avro;
using NStalling.Avro.Configuration;
using NStalling.Avro.Resolution;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Resolution
{
    public class VersionResolutionTests
    {
        private static RecordSchema Customer() =>
            (RecordSchema)Schema.Parse(Schemas.Customer);

        private static IAvroTypeResolver VersionedResolver() =>
            new AvroTypeRegistry().Add<LegacyCustomer>().Add<CurrentCustomer>().BuildResolver();

        [Fact]
        public void ExactVersionBucket_Wins()
        {
            var resolver = VersionedResolver();
            Assert.Equal(typeof(LegacyCustomer), resolver.Resolve(Customer(), null, "1"));
            Assert.Equal(typeof(CurrentCustomer), resolver.Resolve(Customer(), null, "2"));
        }

        [Fact]
        public void UnknownRequestedVersion_Fails_WhenVersionSpecificMappingsExist()
        {
            var resolver = VersionedResolver();
            Assert.Throws<AvroTypeResolutionException>(() => resolver.Resolve(Customer(), null, "99"));
        }

        [Fact]
        public void NoRequestedVersion_NeverGuessesAmongVersionedMappings()
        {
            var resolver = VersionedResolver();
            // Only version-qualified mappings exist; unqualified request finds nothing.
            Assert.Null(resolver.ResolveOrDefault(Customer()));
        }

        [Fact]
        public void UnknownVersion_UsesUnqualified_WhenNoVersionSpecificMappingsExist()
        {
            var resolver = new AvroTypeRegistry()
                .Map<CurrentCustomer>("Customer", "Acme.Events")
                .BuildResolver();

            // No version-qualified mappings for Customer, so a requested version falls back to unqualified.
            Assert.Equal(typeof(CurrentCustomer), resolver.Resolve(Customer(), null, "7"));
        }

        [Fact]
        public void VersionedMapping_DoesNotLoseToUnqualifiedExplicitMapping()
        {
            // Version "2" exists (CurrentCustomer) and an unqualified explicit maps to LegacyCustomer.
            var resolver = new AvroTypeRegistry()
                .Add<CurrentCustomer>()
                .Map<LegacyCustomer>("Customer", "Acme.Events")
                .BuildResolver();

            Assert.Equal(typeof(CurrentCustomer), resolver.Resolve(Customer(), null, "2"));
            Assert.Equal(typeof(LegacyCustomer), resolver.Resolve(Customer(), null, null));
        }

        [Fact]
        public void MultipleVersionsOnOneClrType_ResolveToSameType()
        {
            var resolver = new AvroTypeRegistry().Add<Product>().BuildResolver();
            var product = (RecordSchema)Schema.Parse(
                @"{""type"":""record"",""name"":""Product"",""namespace"":""Acme.Events"",""fields"":[]}");

            Assert.Equal(typeof(Product), resolver.Resolve(product, null, "2"));
            Assert.Equal(typeof(Product), resolver.Resolve(product, null, "3"));
            Assert.Null(resolver.ResolveOrDefault(product, null, "4"));
        }

        [Fact]
        public void ClrClassNameWithVersionSuffix_HasNoVersionSemantics()
        {
            var resolver = new AvroTypeRegistry().Add<WidgetV2>().BuildResolver();
            var widget = (RecordSchema)Schema.Parse(
                @"{""type"":""record"",""name"":""Widget"",""namespace"":""Acme.Events"",""fields"":[]}");

            // WidgetV2 carries no version attribute, so it is unqualified regardless of its name.
            Assert.Equal(typeof(WidgetV2), resolver.Resolve(widget));
            Assert.Equal(typeof(WidgetV2), resolver.Resolve(widget, null, "2"));
        }
    }

    public class ConfigurationConflictTests
    {
        [Fact]
        public void DuplicateEqualPrecedenceMappings_FailFast()
        {
            var registry = new AvroTypeRegistry()
                .Map<LegacyCustomer>("Customer", "Acme.Events", "2")
                .Map<CurrentCustomer>("Customer", "Acme.Events", "2");

            Assert.Throws<AvroConfigurationException>(() => registry.BuildResolver());
        }

        [Fact]
        public void SameTypeSameKey_IsNotAConflict()
        {
            var resolver = new AvroTypeRegistry()
                .Map<CurrentCustomer>("Customer", "Acme.Events", "2")
                .Map<CurrentCustomer>("Customer", "Acme.Events", "2")
                .BuildResolver();

            var customer = (RecordSchema)Schema.Parse(Schemas.Customer);
            Assert.Equal(typeof(CurrentCustomer), resolver.Resolve(customer, null, "2"));
        }
    }
}
