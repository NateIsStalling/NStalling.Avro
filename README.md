# NStalling.Avro

A thin extension over [Apache.Avro](https://www.nuget.org/packages/Apache.Avro) that adds **runtime CLR
type resolution** and **polymorphic type annotations** to reflection-based Avro deserialization.

Apache.Avro understands Avro and performs the decoding. NStalling.Avro supplies the application CLR type
to materialize for a given Avro record schema — including the cases Apache cannot infer on its own:
`object`, interface, and abstract members, union record branches, and version-qualified records that share
one Avro name.

- **Targets:** `netstandard2.1`
- **Depends on:** `Apache.Avro` 1.12.1

## Why

Apache's reflect reader materializes a record by mapping its schema full name to a single concrete CLR
type. That falls short when:

- a member is declared as `object`, an interface, or an abstract base;
- a union has multiple record branches; or
- the same Avro full name must map to **different** CLR types depending on an externally supplied schema
  version.

NStalling.Avro fills exactly these gaps and nothing more. It does **not** generate schemas, implement a
codec, add a schema registry client, or replace any Apache behavior.

## Install

Add a project/package reference to `NStalling.Avro` (and `Apache.Avro`, which comes transitively).

## Namespaces

| Namespace | Contents |
|---|---|
| `NStalling.Avro` | Configuration, type registry/resolver, attributes, options |
| `NStalling.Avro.Serialization` | `AvroSerializer` and the exception hierarchy |
| `NStalling.Avro.DependencyInjection` | `AddAvro` extension for `IServiceCollection` |

## Quick start

Register the CLR types behind your Avro records, build a resolver, and deserialize. When a member is
`object`/interface/abstract or a union branch, NStalling resolves the concrete type for you.

```csharp
using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;

// 1. Map Avro records to CLR types (via [DataContract], CLR full name, or explicit Map).
var resolver = new AvroTypeRegistry()
    .Add<CustomerCreated>()
    .Add<OrderPlaced>()
    .BuildResolver();

var serializer = new AvroSerializer(resolver);

// 2. Deserialize. The `object`/interface payload materializes as the concrete record type.
var schema = Schema.Parse(envelopeSchemaJson);
var envelope = serializer.Deserialize<Envelope>(bytes, schema);

// 3. Ordinary C# pattern matching just works.
switch (envelope.Payload)
{
    case CustomerCreated c: Handle(c); break;
    case OrderPlaced o:     Handle(o); break;
}
```

```csharp
[DataContract(Name = "CustomerCreated", Namespace = "Acme.Events")]
public sealed class CustomerCreated { public string CustomerId { get; init; } = ""; }

public sealed class Envelope
{
    public string EventId { get; init; } = "";
    public object Payload { get; init; } = null!; // materialized as the concrete record type
}
```

## Registering types

`AvroTypeRegistry` derives an Avro full name from `[DataContract]` (`Namespace + "." + Name`) **and** from
the exact CLR full name, or you can map explicitly. Simple-name matching is never used, and versions are
never inferred from CLR names.

```csharp
var resolver = new AvroTypeRegistry()
    .Add<CustomerCreated>()                                   // [DataContract] + CLR full name
    .Map<OrderPlaced>("OrderPlaced", "Acme.Events")           // explicit Avro name
    .Map<Product>("Product", "Acme.Events", schemaVersion: "3") // explicit, version-qualified
    .FromAssemblyContaining<CustomerCreated>()                // controlled assembly scan
    .BuildResolver();
```

Precedence within a resolution bucket is `Explicit` > `DataContract` > `ClrConvention`. Conflicts between
equal-precedence mappings fail fast at build time with `AvroConfigurationException`.

## Schema versions

One CLR type may represent multiple schema versions, and one Avro name may map to different CLR types
under different supplied versions. Annotate candidate types with `[AvroSchemaVersion]`; supply the runtime
version at read time.

```csharp
[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("1")]
public sealed class LegacyCustomer : ICustomer { /* ... */ }

[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("2")]
public sealed class CurrentCustomer : ICustomer { /* ... */ }
```

```csharp
var customer = (RecordSchema)Schema.Parse(customerSchemaJson);
var v1 = serializer.Deserialize(bytes, customer, schemaVersion: "1"); // LegacyCustomer
var v2 = serializer.Deserialize(bytes, customer, schemaVersion: "2"); // CurrentCustomer
```

Resolution is two-stage: the version bucket is selected first (exact version, else the unqualified bucket
only if no versioned mappings exist — **never a guess**), then source precedence applies. A type that
declares `[AvroSchemaVersion]` is never placed in the unqualified bucket.

## Fluent configuration and dependency injection

`AvroOptions` compiles an immutable `AvroConfiguration` exposing a ready `Resolver` and `Serializer`.

```csharp
using NStalling.Avro;

var config = new AvroOptions()
    .Types(t => t.Add<CustomerCreated>().Add<OrderPlaced>())
    .Build();

var result = config.Serializer.Deserialize<Envelope>(bytes, schema);
```

With `Microsoft.Extensions.DependencyInjection`, `AddAvro` compiles eagerly (configuration defects surface
at registration) and registers `AvroConfiguration`, `IAvroTypeResolver`, and `AvroSerializer` as
singletons.

```csharp
using NStalling.Avro.DependencyInjection;

services.AddAvro(o => o.Types(t => t.Add<CustomerCreated>().Add<OrderPlaced>()));
```

## Value-directed (opaque) payloads

When the outer schema carries the payload as opaque `bytes` and cannot identify the inner record, NStalling
decodes it in a **second pass** directed by discriminator values read from the fully decoded outer object
(so field order does not matter). You supply the inner writer schema through an
`IAvroPayloadSchemaSource`; NStalling still resolves the CLR type from the resulting Avro `RecordSchema`.

Configure discriminators with attributes and supply the schema source in code:

```csharp
public sealed class ProfileEnvelope
{
    [AvroTypeDiscriminator]    public string PayloadType { get; init; } = "";
    [AvroVersionDiscriminator] public string? PayloadVersion { get; init; }
    [AvroPolymorphic]          public object Payload { get; set; } = null!; // opaque bytes -> concrete type
}

var config = new AvroOptions()
    .Types(t => t.Add<LegacyProfile>().Add<CurrentProfile>())
    .Polymorphic<ProfileEnvelope>(p => p
        .Member(e => e.Payload)
        .PayloadSchema(myPayloadSchemaSource)) // IAvroPayloadSchemaSource
    .Build();
```

`IAvroPayloadSchemaSource.TryGetWriterSchema` must distinguish an ordinary not-found (return `false`) from
an infrastructure failure (throw). Unknown/missing **type** identity is governed by
`AvroUnrecognizedTypeDiscriminatorHandling`:

| Value | Behavior |
|---|---|
| `Fail` *(default)* | Throw `AvroTypeResolutionException`. |
| `PreservePayload` | Keep the raw payload when it is assignable to the member. |
| `UseFallbackType` | Materialize a configured fallback type from the closed allowlist. `UseFallbackType` never fabricates a writer schema. |

Payload-schema, CLR-resolution, and inner-decode failures are typed separately and are **never** diverted
by this policy.

## Exceptions

All materialization-pipeline failures derive from `AvroSerializationException` and carry relevant
path/schema/version/discriminator context, preserving the originating exception as `InnerException`:

- `AvroPayloadSchemaException` — the payload schema source failed (infrastructure).
- `AvroTypeResolutionException` — no/ambiguous CLR mapping, declared-type incompatibility, or unrecognized
  identity under `Fail`.
- `AvroPayloadDecodeException` — the second-pass decode of an isolated payload buffer failed.

Configuration defects use `AvroConfigurationException`. Cancellation and ordinary API argument-validation
retain normal .NET semantics (they are not wrapped).

## Samples

Runnable samples live under [`samples/`](NStalling.Avro/samples):

- **EventEnvelope** — schema-directed polymorphism: an `object` payload over a union of record branches.
- **Annotations** — attribute-driven configuration: `[AvroTypeDiscriminator]`,
  `[AvroVersionDiscriminator]`, `[AvroPolymorphic]`, and `[AvroSchemaVersion]`, where a version
  discriminator selects between two CLR types sharing one Avro name.

```bash
dotnet run --project NStalling.Avro/samples/EventEnvelope
dotnet run --project NStalling.Avro/samples/Annotations
```

## Build and test

```bash
dotnet build NStalling.Avro/NStalling.Avro.sln
dotnet test  NStalling.Avro/NStalling.Avro.sln
```

## License

Apache License 2.0. See [LICENSE](LICENSE).
