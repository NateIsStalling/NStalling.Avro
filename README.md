# NStalling.Avro

A thin extension over [Apache.Avro](https://www.nuget.org/packages/Apache.Avro) that adds **runtime CLR
type resolution** to reflection-based Avro deserialization.

Apache.Avro understands Avro and performs the decoding. NStalling.Avro supplies the application CLR type
to materialize for a given Avro record schema — including `object`, interface, and abstract members,
union record branches, and version-qualified records that share one Avro name.

- **Targets:** `netstandard2.1`
- **Depends on:** `Apache.Avro` 1.12.1

## Why

Apache's reflection machinery ultimately associates an Avro record schema with a concrete CLR type. That
becomes limiting when:

- a member is declared as `object`, an interface, or an abstract base;
- a union has multiple record branches;
- the same Avro full name must map to **different** CLR types depending on an externally supplied schema
  version; or
- an outer record carries an inner payload as opaque `bytes`, with its writer schema supplied separately.

NStalling.Avro stays focused on CLR materialization around Apache's reflection reader. It does **not**
generate schemas, implement a codec, add a schema registry client, or replace Apache's Avro behavior.

## Install

> **NuGet package coming soon.**

For now, reference the project directly from source.

## Quick start

Register the CLR types behind your Avro records, build a resolver, and deserialize. When a record is
encountered through an `object`, interface, abstract member, or Avro union branch, NStalling resolves the
concrete CLR type for Apache.

```csharp
using Avro;
using NStalling.Avro;
using NStalling.Avro.Serialization;

// 1. Register CLR types behind your Avro records.
var resolver = new AvroTypeRegistry()
    .Add<CustomerCreated>()
    .Add<OrderPlaced>()
    .BuildResolver();

var serializer = new AvroSerializer(resolver);

// 2. Deserialize. The payload materializes as the concrete record type.
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
public sealed class CustomerCreated
{
    public string CustomerId { get; init; } = "";
}

public sealed class Envelope
{
    public string EventId { get; init; } = "";
    public object Payload { get; init; } = null!;
}
```

## Registering types

`AvroTypeRegistry` can derive an Avro full name from `[DataContract]` (`Namespace + "." + Name`) or from
the exact CLR full name, or you can map a type explicitly.

Simple-name matching is never used, and schema versions are never inferred from CLR names.

```csharp
var resolver = new AvroTypeRegistry()
    .Add<CustomerCreated>()
    .Map<OrderPlaced>(
        "OrderPlaced",
        "Acme.Events")
    .Map<Product>(
        "Product",
        "Acme.Events",
        schemaVersion: "3")
    .FromAssemblyContaining<CustomerCreated>()
    .BuildResolver();
```

Within a selected resolution bucket, precedence is:

```text
Explicit > DataContract > CLR full-name convention
```

Conflicts between equal-precedence mappings fail fast at build time with `AvroConfigurationException`.

## Attributes

NStalling.Avro can keep type-resolution metadata close to CLR models when that is the most natural place
for it.

```csharp
[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("2")]
public sealed class Customer
{
}
```

For members that need runtime discriminator metadata:

```csharp
public sealed class ProfileEnvelope
{
    [AvroTypeDiscriminator]
    public string PayloadType { get; init; } = "";

    [AvroVersionDiscriminator]
    public string? PayloadVersion { get; init; }

    [AvroPolymorphic]
    public object Payload { get; set; } = null!;
}
```

The attributes have distinct roles:

- `DataContract` maps a CLR type to an Avro record name.
- `[AvroSchemaVersion]` declares which externally supplied schema version(s) a CLR type can represent.
- `[AvroTypeDiscriminator]` identifies metadata used to locate an opaque payload's writer schema.
- `[AvroVersionDiscriminator]` supplies runtime schema-version context.
- `[AvroPolymorphic]` marks or configures a member that needs runtime materialization behavior.

Fluent configuration can be used instead when this metadata belongs in application configuration rather
than on the model.

## Schema versions

One CLR type may represent multiple schema versions, and one Avro name may map to different CLR types
under different supplied versions.

```csharp
[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("1")]
public sealed class LegacyCustomer : ICustomer
{
}

[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("2")]
public sealed class CurrentCustomer : ICustomer
{
}
```

```csharp
var customerSchema = (RecordSchema)Schema.Parse(customerSchemaJson);

var v1 = serializer.Deserialize(
    bytes,
    customerSchema,
    schemaVersion: "1"); // LegacyCustomer

var v2 = serializer.Deserialize(
    bytes,
    customerSchema,
    schemaVersion: "2"); // CurrentCustomer
```

A single CLR type can also represent several compatible versions:

```csharp
[DataContract(Name = "Customer", Namespace = "Acme.Events")]
[AvroSchemaVersion("2")]
[AvroSchemaVersion("3")]
[AvroSchemaVersion("4")]
public sealed class Customer
{
}
```

Resolution is two-stage:

1. Select the version bucket.
2. Apply mapping precedence within that bucket.

An exact version wins. If version-specific mappings exist for an Avro name, an unknown version does
**not** silently fall back to an unqualified mapping.

A type that declares `[AvroSchemaVersion]` is never placed in the unqualified bucket.

## Configuration and dependency injection

`AvroOptions` compiles an immutable `AvroConfiguration` exposing a ready resolver and serializer.

```csharp
using NStalling.Avro;

var config = new AvroOptions()
    .Types(t => t
        .Add<CustomerCreated>()
        .Add<OrderPlaced>())
    .Build();

var result = config.Serializer.Deserialize<Envelope>(bytes, schema);
```

The `NStalling.Avro.DependencyInjection` project adds `AddAvro` for
`Microsoft.Extensions.DependencyInjection`. It compiles eagerly so configuration defects surface during
registration.

```csharp
using NStalling.Avro.DependencyInjection;

services.AddAvro(o =>
    o.Types(t => t
        .Add<CustomerCreated>()
        .Add<OrderPlaced>()));
```

`AddAvro` registers:

- `AvroConfiguration`
- `IAvroTypeResolver`
- `AvroSerializer`

as singletons.

## Opaque payloads

When an outer Avro record carries an inner payload as opaque `bytes`, NStalling can perform a second
decode after the outer record has been read.

The application supplies the inner writer schema through `IAvroPayloadSchemaSource`. NStalling then uses
the resulting Avro schema to resolve the CLR type and lets Apache perform the inner decode.

```text
outer record
    ↓
payload metadata / discriminator
    ↓
IAvroPayloadSchemaSource
    ↓
inner Avro schema
    ↓
CLR type resolution
    ↓
Apache second-pass decode
```

Example:

```csharp
public sealed class ProfileEnvelope
{
    [AvroTypeDiscriminator]
    public string PayloadType { get; init; } = "";

    [AvroVersionDiscriminator]
    public string? PayloadVersion { get; init; }

    [AvroPolymorphic]
    public object Payload { get; set; } = null!;
}

var config = new AvroOptions()
    .Types(t => t
        .Add<LegacyProfile>()
        .Add<CurrentProfile>())
    .Polymorphic<ProfileEnvelope>(p => p
        .Member(e => e.Payload)
        .PayloadSchema(myPayloadSchemaSource))
    .Build();
```

`IAvroPayloadSchemaSource.TryGetWriterSchema` distinguishes an ordinary not-found from an infrastructure
failure.

Discriminator values are never treated as arbitrary CLR type names. Resolved CLR types remain limited to
the configured type registry and explicitly scanned assemblies.

Unknown or missing **type identity** is governed by `AvroUnrecognizedTypeDiscriminatorHandling`:

| Value | Behavior |
|---|---|
| `Fail` *(default)* | Throw `AvroTypeResolutionException`. |
| `PreservePayload` | Keep the raw payload when it is assignable to the target member. |
| `UseFallbackType` | Use a configured fallback CLR type from the closed allowlist. The writer schema must still be supplied. |

A missing version discriminator is different: it simply means no version qualifier was supplied, so
normal unqualified version resolution applies.

Payload-schema, CLR-resolution, and inner-decode failures are typed separately and are **never** diverted
by the type-discriminator handling policy.

## Exceptions

Failures inside the Avro materialization pipeline derive from `AvroSerializationException` and carry
relevant path/schema/version/discriminator context while preserving the originating exception as
`InnerException`.

- `AvroPayloadSchemaException` — the payload schema source failed.
- `AvroTypeResolutionException` — no or ambiguous CLR mapping, declared-type incompatibility, or
  unrecognized type identity under `Fail`.
- `AvroPayloadDecodeException` — the second-pass decode of an isolated payload buffer failed.

Configuration defects use `AvroConfigurationException`.

Cancellation and ordinary API argument-validation exceptions retain normal .NET semantics and are not
wrapped.

## Samples

Runnable samples live under [`NStalling.Avro.Samples`](src/NStalling.Avro.Samples):

- **EventEnvelope** — resolves an `object` payload backed by an Avro union of record branches.
- **Annotations** — demonstrates `[AvroTypeDiscriminator]`, `[AvroVersionDiscriminator]`,
  `[AvroPolymorphic]`, and `[AvroSchemaVersion]`, including version-qualified CLR mappings that share one
  Avro name.

```bash
dotnet run --project NStalling.Avro.Samples/EventEnvelope
dotnet run --project NStalling.Avro.Samples/Annotations
```

## Build and test

```bash
dotnet build NStalling.Avro.sln
dotnet test NStalling.Avro.sln
```

## License

Apache License 2.0. See [LICENSE](LICENSE).
