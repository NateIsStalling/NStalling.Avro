using Microsoft.Extensions.DependencyInjection;

namespace NStalling.Avro;

public static class AvroServiceCollectionExtensions
{
    public static IServiceCollection AddAvro(this IServiceCollection services,
        Action<AvroOptionsBuilder>? configure = null)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        var options = new AvroOptionsBuilder();
        configure?.Invoke(options);

        var registry = options.BuildRegistry();
        var unionConfiguration = options.BuildUnionConfiguration();

        services.AddSingleton(registry);
        services.AddSingleton(unionConfiguration);
        services.AddSingleton<IAvroTypeResolver>(_ => new DefaultAvroTypeResolver(registry));
        services.AddSingleton<IAvroSchemaResolver>(_ => new DefaultAvroSchemaResolver(unionConfiguration));

        return services;
    }
}