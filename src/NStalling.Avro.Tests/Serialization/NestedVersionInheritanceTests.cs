using Avro;
using NStalling.Avro;
using NStalling.Avro.Provider;
using NStalling.Avro.Tests.Fixtures;
using Xunit;

namespace NStalling.Avro.Tests.Serialization
{
    /// <summary>
    /// Covers the documented global behavior of <see cref="AvroResolutionOptions.InheritSchemaVersion"/>:
    /// when enabled, the root schema version flows into schema-directed nested record, array, and map
    /// resolutions, so version-qualified nested types are selected. When disabled (the default), nested
    /// resolutions receive no version context and version-qualified nested types cannot be resolved.
    ///
    /// The record path is verified end-to-end through the serializer. The array/map paths are verified at
    /// the class-cache level because Apache's reflect reader independently cannot decode polymorphic
    /// elements into an object-typed collection; the cache mapping is where version inheritance takes effect.
    /// </summary>
    public class NestedVersionInheritanceTests
    {
        private static IAvroTypeResolver VersionedResolver()
            => new AvroTypeRegistry().Add<LegacyCustomer>().Add<CurrentCustomer>().BuildResolver();

        private static AvroConfiguration Configure(bool inherit)
        {
            var builder = new AvroOptions()
                .Types(t => t.Add<LegacyCustomer>().Add<CurrentCustomer>());
            if (inherit)
            {
                builder.Resolution(r => r.InheritSchemaVersion());
            }

            return builder.Build();
        }

        private static byte[] WriteRecordEnvelope()
        {
            var schema = Schema.Parse(Schemas.VersionedRecordEnvelope);
            var resolver = new AvroTypeRegistry().Add<LegacyCustomer>().BuildResolver();
            var wire = new VersionedRecordEnvelopeWire { EventId = "e1", Customer = new LegacyCustomer { Name = "Ada" } };
            return AvroWriteHelper.Serialize(wire, schema, resolver);
        }

        // Resolves the CLR type the adapter maps the nested "Acme.Events.Customer" record to, given the
        // root schema, root type, and inheritance setting.
        private static System.Type ResolveNestedCustomerType(
            string schemaJson,
            System.Type rootType,
            string rootVersion,
            bool inherit)
        {
            var schema = Schema.Parse(schemaJson);
            var adapter = new ApacheReflectionAdapter(VersionedResolver(), inheritNestedSchemaVersion: inherit);
            var cache = adapter.BuildClassCache(schema, rootType, rootVersion);
            var customerSchema = NestedCustomerSchema((RecordSchema)schema);
            return cache.GetClass(customerSchema).GetClassType();
        }

        private static RecordSchema NestedCustomerSchema(RecordSchema envelope)
        {
            var field = envelope.Contains("Customers") ? envelope["Customers"] : envelope["Customer"];
            return field.Schema switch
            {
                RecordSchema record => record,
                ArraySchema array => (RecordSchema)array.ItemSchema,
                MapSchema map => (RecordSchema)map.ValueSchema,
                _ => throw new System.InvalidOperationException("Unexpected nested schema shape.")
            };
        }

        [Fact]
        public void InheritEnabled_NestedRecord_ResolvesVersionedTypeByRootVersion()
        {
            var bytes = WriteRecordEnvelope();
            var schema = Schema.Parse(Schemas.VersionedRecordEnvelope);

            var v1 = Configure(inherit: true)
                .Serializer.Deserialize<VersionedRecordEnvelope>(bytes, schema, "1");
            Assert.IsType<LegacyCustomer>(v1.Customer);

            var v2 = Configure(inherit: true)
                .Serializer.Deserialize<VersionedRecordEnvelope>(bytes, schema, "2");
            Assert.IsType<CurrentCustomer>(v2.Customer);
        }

        [Fact]
        public void InheritDisabled_NestedRecord_DoesNotInheritVersion_AndFailsToResolve()
        {
            var bytes = WriteRecordEnvelope();
            var schema = Schema.Parse(Schemas.VersionedRecordEnvelope);

            // Without inheritance the nested Customer resolution receives no version context; because the
            // full name has only version-qualified mappings, resolution fails rather than guessing.
            Assert.Throws<AvroTypeResolutionException>(() => Configure(inherit: false)
                .Serializer.Deserialize<VersionedRecordEnvelope>(bytes, schema, "1"));
        }

        [Fact]
        public void InheritEnabled_NestedArray_ResolvesVersionedElementByRootVersion()
        {
            Assert.Equal(
                typeof(CurrentCustomer),
                ResolveNestedCustomerType(Schemas.VersionedArrayEnvelope, typeof(VersionedArrayEnvelope), "2", inherit: true));
            Assert.Equal(
                typeof(LegacyCustomer),
                ResolveNestedCustomerType(Schemas.VersionedArrayEnvelope, typeof(VersionedArrayEnvelope), "1", inherit: true));
        }

        [Fact]
        public void InheritDisabled_NestedArray_FailsToResolve()
        {
            Assert.Throws<AvroTypeResolutionException>(() =>
                ResolveNestedCustomerType(Schemas.VersionedArrayEnvelope, typeof(VersionedArrayEnvelope), "1", inherit: false));
        }

        [Fact]
        public void InheritEnabled_NestedMap_ResolvesVersionedValueByRootVersion()
        {
            Assert.Equal(
                typeof(LegacyCustomer),
                ResolveNestedCustomerType(Schemas.VersionedMapEnvelope, typeof(VersionedMapEnvelope), "1", inherit: true));
            Assert.Equal(
                typeof(CurrentCustomer),
                ResolveNestedCustomerType(Schemas.VersionedMapEnvelope, typeof(VersionedMapEnvelope), "2", inherit: true));
        }

        [Fact]
        public void InheritDisabled_NestedMap_FailsToResolve()
        {
            Assert.Throws<AvroTypeResolutionException>(() =>
                ResolveNestedCustomerType(Schemas.VersionedMapEnvelope, typeof(VersionedMapEnvelope), "1", inherit: false));
        }
    }
}
