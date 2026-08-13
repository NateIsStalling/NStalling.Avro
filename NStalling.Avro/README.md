# NStalling.Avro

`NStalling.Avro` is a .NET ergonomics layer over Apache Avro.

## What this includes now

- `Schema -> Type` resolution via `IAvroTypeResolver`
- `Type -> Schema` generation via `IAvroSchemaResolver`
- `DataContract` / `DataMember` metadata support
- Schema-assisted and code-first serializer convenience APIs
- Deterministic `Resolve`, `TryResolve`, and `ResolveOrDefault` behavior
- DI registration via `services.AddAvro(...)`
- Explicit union configuration with deterministic branch ordering and validation

## Quick usage

```csharp
var typeResolver = AvroResolvers.CreateTypeResolver(types => types
    .Map<CustomerCreated>(new AvroSchemaName("CustomerCreated", "Acme.Events"))
    .FromAssemblyContaining<CustomerCreated>());

var schemaResolver = AvroResolvers.CreateSchemaResolver(unions => unions
    .Union<IPayment>(union => union
        .Add<CardPayment>()
        .Add<WirePayment>()));

var schema = schemaResolver.Resolve<CustomerCreated>();
var bytes = AvroSerializer.Serialize(new CustomerCreated { CustomerId = Guid.NewGuid() }, schema);
var value = AvroSerializer.Deserialize<CustomerCreated>(bytes, schema, schema);
```

## DI usage

```csharp
services.AddAvro(options => options
    .Types(types => types
        .FromAssemblyContaining<CustomerCreated>()
        .Map<CustomerCreated>(new AvroSchemaName("CustomerCreated", "Acme.Events")))
    .Union<IPayment>(union => union
        .Add<CardPayment>()
        .Add<WirePayment>())
    .For<Order>(order => order
        .Member(x => x.Payment)
        .Union(union => union
            .Add<CardPayment>()
            .Add<WirePayment>())));
```

## Test fixtures

Avro fixtures are under `NStalling.Avro.Tests/Fixtures/*.avsc` and cover primitive, nullable, nested, recursive, union, and versioned customer schema scenarios.
