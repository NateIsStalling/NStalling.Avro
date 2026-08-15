using System;
using Microsoft.Extensions.DependencyInjection;
using NStalling.Avro.Configuration;
using NStalling.Avro.Resolution;
using NStalling.Avro.Serialization;

namespace NStalling.Avro.DependencyInjection
{
    /// <summary>
    /// Dependency-injection convenience for registering NStalling.Avro. DI is only one configuration
    /// path; the resolver and serializer never depend on a container.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Compiles an <see cref="AvroConfiguration"/> from the supplied options and registers it along
        /// with <see cref="IAvroTypeResolver"/> and <see cref="AvroSerializer"/> as singletons.
        /// </summary>
        public static IServiceCollection AddAvro(this IServiceCollection services, Action<AvroOptions> configure)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure is null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var options = new AvroOptions();
            configure(options);

            // Compile eagerly so deterministic configuration defects surface at registration time.
            var configuration = options.Build();

            services.AddSingleton(configuration);
            services.AddSingleton(configuration.Resolver);
            services.AddSingleton(configuration.Serializer);
            return services;
        }
    }
}
