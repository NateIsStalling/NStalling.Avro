# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

NStalling.Avro is a thin extension over `Apache.Avro` (reflection-based reader/writer) that adds runtime
CLR type resolution to Avro deserialization: `object`/interface/abstract members, union record branches,
version-qualified records sharing one Avro name, and opaque `bytes` payloads whose inner schema is
supplied out-of-band. It does **not** generate schemas, implement a codec, or provide a schema registry
client — Apache.Avro does all actual encoding/decoding; this library only tells Apache which CLR type to
materialize.

Target framework: `netstandard2.1`. Depends on `Apache.Avro` 1.12.1.

## Commands

```bash
dotnet build src/NStalling.Avro.sln
dotnet test src/NStalling.Avro.sln

# Single test class / method
dotnet test src/NStalling.Avro.sln --filter "FullyQualifiedName~VersionResolutionTests"
dotnet test src/NStalling.Avro.sln --filter "FullyQualifiedName~AvroTypeResolverTests.SomeMethodName"

# Run samples
dotnet run --project src/NStalling.Avro.Samples/EventEnvelope
dotnet run --project src/NStalling.Avro.Samples/Annotations
```

Solution layout: `NStalling.Avro` (core library, `netstandard2.1`), `NStalling.Avro.DependencyInjection`
(the `AddAvro` extension for `Microsoft.Extensions.DependencyInjection`), `NStalling.Avro.Tests` (xUnit,
`net10.0`), `NStalling.Avro.Samples/{EventEnvelope,Annotations}` (runnable samples, `net10.0`). Tests
reference the library via `InternalsVisibleTo`, so tests routinely exercise `internal` types directly
(resolver, `AvroTypeIndex`, adapters) rather than only the public surface.

## Architecture

### Two-phase pipeline: configuration-time compile, then read-time resolve

`AvroOptions` (fluent root: `.Types(...)`, `.Resolution(...)`, `.Polymorphism(...)`, `.Polymorphic<T>(...)`)
accumulates configuration and compiles it via `AvroConfiguration.Compile` into an immutable
`AvroConfiguration` exposing a ready `Serializer`. All deterministic configuration defects (conflicting
mappings, invalid polymorphic member declarations, bad fallback types) are meant to surface at build/compile
time as `AvroConfigurationException`, not at read time. `services.AddAvro(...)` (DI) compiles eagerly for
the same reason — so a bad config fails at registration, not on first message.

Everything downstream of a built `AvroConfiguration`/`AvroTypeResolver` is treated as immutable and
concurrency-safe for concurrent reads.

### Type resolution: `AvroTypeRegistry` → `AvroTypeIndex` → `AvroTypeResolver`

- `AvroTypeRegistry` accumulates `AvroTypeMapping`s from three sources, in **precedence order**:
  `Explicit` (`.Map<T>(...)`) > `DataContract` (`[DataContract(Name=, Namespace=)]`) > `ClrConvention`
  (CLR full name). Simple-name matching is never used; schema versions are never inferred from CLR names.
- `AvroTypeIndex.Build` compiles mappings into an immutable `(fullName, version) -> (source -> Type)` map
  plus a closed **allowlist** of every registered CLR type. Equal-precedence conflicts for the same
  effective key throw `AvroConfigurationException` at build time (fail fast, not at first mismatched read).
- Resolution (`AvroTypeIndex.Resolve`) is **two-stage**: (1) select the version bucket — an exact
  `(fullName, version)` match wins; if version-qualified mappings exist for that full name but not the
  requested version, resolution fails rather than falling back to the unqualified bucket; only when *no*
  versioned mappings exist for the name does it fall back to the unqualified bucket; (2) within the
  selected bucket, apply source precedence (Explicit > DataContract > ClrConvention).
- `AvroTypeResolver` wraps the index and adds declared-type compatibility checks (`Resolve`/`TryResolve`/
  `ResolveOrDefault`), throwing `AvroTypeResolutionException` on failure/ambiguity.

### Bridging into Apache's reflection reader: `ApacheReflectionAdapter`

Apache's `ClassCache`/`ReflectReader<T>` need every reachable record schema mapped to a concrete CLR type
up front. `ApacheReflectionAdapter.BuildClassCache` walks the writer schema graph (records, unions, arrays,
maps) starting at the root type, resolving each record via the configured `IAvroTypeResolver` when the
declared member type isn't already concrete (i.e. it's `object`, an interface, an abstract class, or a
union branch), then registers all discovered `(schema, type)` pairs into Apache's cache before letting
Apache populate its own auxiliary caches. **The built cache is scoped to a single resolution context**
(writer schema + root type + version) — Apache keys its cache by record full name only, so a cache must
never be reused across contexts where the same Avro full name maps to different CLR types under different
versions. `AvroSerializer.ReadCore` builds a fresh cache per read via `ApacheReflectionAdapter`, then
invokes `ReflectReader<T>` through cached reflection (`ConstructorInfo`/`MethodInfo`) since the target type
is only known at runtime.

Because Apache's reflect reader requires `bytes` fields to decode into `byte[]`, opaque polymorphic payload
members (typically declared `object`) are handled via a process-wide `OpaquePayloadPassthroughConverter`
registered per declared type — it lets the first pass hold the raw `byte[]` until value-directed resolution
runs a second pass.

### Value-directed (opaque payload) resolution: the "second decode" path

For a member marked `[AvroPolymorphic]` (or configured via `.Polymorphic<T>(...)`), the first Apache pass
decodes the field as raw `byte[]`. `PolymorphicBindingFactory` compiles a `PolymorphicMemberBinding` per
member — locating type/version discriminators (via `[AvroTypeDiscriminator]`/`[AvroVersionDiscriminator]`
attributes, `DiscriminatorLocator`, or fluent config), resolving `AvroUnrecognizedTypeDiscriminatorHandling`
(`Fail` default / `PreservePayload` / `UseFallbackType`), and validating that the declared member type can
actually hold a `byte[]` and that any fallback type is in the resolver's allowlist. At read time,
`ValueDirectedPayloadBinder` (invoked from `AvroSerializer.ApplyPolymorphicBindings` after the outer object
is materialized) reads the discriminator values off the already-decoded outer object, asks the configured
`IAvroPayloadSchemaSource` for the inner writer schema, resolves the CLR type through the same
`IAvroTypeResolver`, and calls back into `AvroSerializer.DecodeInnerPayload` for the actual second-pass
Apache decode. Resolved types are always constrained to the closed allowlist built by `AvroTypeIndex` —
discriminator string values are never used as arbitrary CLR type names.

Flow: `outer record` → `discriminator fields` → `IAvroPayloadSchemaSource` → `inner Avro schema` →
`IAvroTypeResolver` → second-pass Apache decode.

### Exception taxonomy

All pipeline failures derive from `AvroSerializationException` and carry path/schema/version/discriminator
context, preserving the original as `InnerException`:
- `AvroPayloadSchemaException` — the payload schema source failed.
- `AvroTypeResolutionException` — no/ambiguous CLR mapping, declared-type incompatibility, or unrecognized
  type identity under `Fail` handling.
- `AvroPayloadDecodeException` — the second-pass decode of an isolated payload buffer failed.

`AvroConfigurationException` is reserved for configuration-time defects (registry/build/compile time), kept
distinct from read-time failures. Cancellation and ordinary argument-validation exceptions are never wrapped.

### Where things live

- `src/NStalling.Avro/*.cs` (root namespace) — public configuration surface: `AvroOptions`,
  `AvroTypeRegistry`, `AvroConfiguration`, attributes, exception types, `IAvroTypeResolver`.
- `src/NStalling.Avro/Provider/` — internal machinery: type providers (`DataContractTypeProvider`,
  `ClrConventionTypeProvider`, `AssemblyTypeProvider`), the Apache bridge (`ApacheReflectionAdapter`), and
  the polymorphic binding pipeline (`PolymorphicBindingFactory`, `PolymorphicBindingRegistry`,
  `ValueDirectedPayloadBinder`, `DiscriminatorLocator`).
- `src/NStalling.Avro/Serialization/` — `AvroSerializer` (the public read entry point) and serialization
  exception types.
- `src/NStalling.Avro.DependencyInjection/` — `AddAvro` extension for `Microsoft.Extensions.DependencyInjection`,
  a separate project from the core library.
- `src/NStalling.Avro.Tests/` mirrors the core library by concern (`Resolution/`, `Provider/`,
  `Serialization/`, `DependencyInjection/`), with shared fixtures (domain types, hand-built schemas, an
  Avro write helper) in `Fixtures/`.

## Conventions to preserve

- Resolution precedence and the two-stage version algorithm (above) are deliberate, tested behavior — do
  not reintroduce simple-name matching or version fallback-guessing.
- Configuration defects must fail at build/compile time wherever the input is deterministic; don't defer
  detectable config errors to read time.
- A `ClassCache` built by `ApacheReflectionAdapter` must stay scoped to one resolution context; don't share
  or cache it across different root types/versions.
- Keep exception types and their carried context (`SchemaFullName`, `SchemaVersion`, `Path`, etc.) aligned
  with the taxonomy above — payload-schema, type-resolution, and inner-decode failures are typed separately
  and must not be diverted through the type-discriminator handling policy.
