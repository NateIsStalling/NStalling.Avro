using Avro;

namespace NStalling.Avro;

public static class AvroSchema
{
    private static readonly IAvroSchemaResolver DefaultResolver = new DefaultAvroSchemaResolver();

    public static Schema For<T>() => DefaultResolver.Resolve<T>();
}

