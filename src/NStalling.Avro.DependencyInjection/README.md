# NStalling.Avro.DependencyInjection

`Microsoft.Extensions.DependencyInjection` integration for
[NStalling.Avro](https://www.nuget.org/packages/NStalling.Avro).

Provides `IServiceCollection.AddAvro(...)` for configuring runtime CLR type resolution and registering
the resulting Avro services.

## Install

```bash
dotnet add package NStalling.Avro.DependencyInjection
```

`NStalling.Avro` is included as a dependency.

## Usage

Register the concrete CLR types that correspond to known Avro record schemas:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro.DependencyInjection;

services.AddAvro(options =>
    options.Types(types => types
        .Add<CustomerCreated>()
        .Add<OrderPlaced>()));
```

Then resolve and use the configured serializer:

```csharp
var serializer = serviceProvider.GetRequiredService<AvroSerializer>();

var envelope = serializer.Deserialize<Envelope>(bytes, schema);
```

`AddAvro` registers the following services as singletons:

- `AvroConfiguration`
- `IAvroTypeResolver`
- `AvroSerializer`

Configuration is compiled during registration, so invalid mappings fail when the service collection is
configured rather than on first use.

## Configuration

`AddAvro` uses the same `AvroOptions` configuration API as the core package, including explicit mappings,
assembly registration, schema versions, and polymorphic payload configuration.

For the type-resolution model and full configuration options, see
[NStalling.Avro](https://github.com/NateIsStalling/NStalling.Avro).

## Sample

See the
[DependencyInjection sample](https://github.com/NateIsStalling/NStalling.Avro/tree/main/src/NStalling.Avro.Samples/DependencyInjection)
for a complete runnable example.

## License

Apache License 2.0. See
[LICENSE](https://github.com/NateIsStalling/NStalling.Avro/blob/main/LICENSE).
