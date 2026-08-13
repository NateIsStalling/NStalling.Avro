using System;

namespace NStalling.Avro;

public static class AvroResolvers
{
    public static IAvroTypeResolver CreateTypeResolver(Action<AvroTypeRegistryBuilder>? configure = null)
    {
        var builder = AvroTypeRegistryBuilder.CreateDefault();
        configure?.Invoke(builder);
        return new DefaultAvroTypeResolver(builder.Build());
    }

    public static IAvroSchemaResolver CreateSchemaResolver(Action<AvroUnionConfigurationBuilder>? configure = null)
    {
        if (configure is null)
        {
            return new DefaultAvroSchemaResolver();
        }

        var builder = new AvroUnionConfigurationBuilder();
        configure(builder);
        return new DefaultAvroSchemaResolver(builder.Build());
    }
}