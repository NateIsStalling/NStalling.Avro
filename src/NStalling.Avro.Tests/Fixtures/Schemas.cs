namespace NStalling.Avro.Tests.Fixtures
{
    /// <summary>Avro schemas used by integration tests, parsed via <c>Schema.Parse</c>.</summary>
    internal static class Schemas
    {
        // Envelope with a polymorphic union payload of two named records.
        public const string EnvelopeUnion = @"{
          ""type"":""record"",""name"":""EnvelopeObject"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventId"",""type"":""string""},
            {""name"":""Payload"",""type"":[
               {""type"":""record"",""name"":""CustomerCreated"",""namespace"":""Acme.Events"",""fields"":[{""name"":""CustomerId"",""type"":""string""}]},
               {""type"":""record"",""name"":""OrderPlaced"",""namespace"":""Acme.Events"",""fields"":[{""name"":""OrderId"",""type"":""string""}]}
            ]}
          ]}";

        // Envelope with a nullable single-record union (nullability, not polymorphism).
        public const string EnvelopeNullableSingle = @"{
          ""type"":""record"",""name"":""EnvelopeObject"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventId"",""type"":""string""},
            {""name"":""Payload"",""type"":[""null"",
               {""type"":""record"",""name"":""CustomerCreated"",""namespace"":""Acme.Events"",""fields"":[{""name"":""CustomerId"",""type"":""string""}]}
            ]}
          ]}";

        // A bare Customer record (type identity supplied by schema; version supplied externally).
        public const string Customer = @"{
          ""type"":""record"",""name"":""Customer"",""namespace"":""Acme.Events"",
          ""fields"":[{""name"":""Name"",""type"":""string""}]}";

        // The inner payload writer schema for a CustomerCreated record (used by the value-directed path).
        public const string CustomerCreatedRecord = @"{
          ""type"":""record"",""name"":""CustomerCreated"",""namespace"":""Acme.Events"",
          ""fields"":[{""name"":""CustomerId"",""type"":""string""}]}";

        // Opaque envelope: discriminator + version + bytes payload.
        public const string OpaqueEnvelope = @"{
          ""type"":""record"",""name"":""OpaqueEnvelope"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventType"",""type"":""string""},
            {""name"":""SchemaVersion"",""type"":[""null"",""string""]},
            {""name"":""Payload"",""type"":""bytes""}
          ]}";

        // Envelope with a single nested Customer record (version supplied externally, inherited by nesting).
        public const string VersionedRecordEnvelope = @"{
          ""type"":""record"",""name"":""VersionedRecordEnvelope"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventId"",""type"":""string""},
            {""name"":""Customer"",""type"":
               {""type"":""record"",""name"":""Customer"",""namespace"":""Acme.Events"",""fields"":[{""name"":""Name"",""type"":""string""}]}}
          ]}";

        // Envelope with an array of nested Customer records.
        public const string VersionedArrayEnvelope = @"{
          ""type"":""record"",""name"":""VersionedArrayEnvelope"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventId"",""type"":""string""},
            {""name"":""Customers"",""type"":{""type"":""array"",""items"":
               {""type"":""record"",""name"":""Customer"",""namespace"":""Acme.Events"",""fields"":[{""name"":""Name"",""type"":""string""}]}}}
          ]}";

        // Envelope with a map of nested Customer records.
        public const string VersionedMapEnvelope = @"{
          ""type"":""record"",""name"":""VersionedMapEnvelope"",""namespace"":""Acme.Events"",
          ""fields"":[
            {""name"":""EventId"",""type"":""string""},
            {""name"":""Customers"",""type"":{""type"":""map"",""values"":
               {""type"":""record"",""name"":""Customer"",""namespace"":""Acme.Events"",""fields"":[{""name"":""Name"",""type"":""string""}]}}}
          ]}";
    }
}
