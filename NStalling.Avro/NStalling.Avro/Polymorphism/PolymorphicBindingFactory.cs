using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NStalling.Avro.Configuration;
using NStalling.Avro.Resolution;

namespace NStalling.Avro.Polymorphism
{
    /// <summary>
    /// Compiles per-member polymorphism bindings from <see cref="AvroPolymorphicAttribute"/>, marker
    /// attributes, and fluent configuration, applying configuration precedence and failing fast on
    /// deterministic defects.
    /// </summary>
    internal static class PolymorphicBindingFactory
    {
        public static IReadOnlyDictionary<Type, IReadOnlyList<PolymorphicMemberBinding>> BuildAll(
            AvroOptions options,
            AvroTypeResolver resolver)
        {
            var result = new Dictionary<Type, IReadOnlyList<PolymorphicMemberBinding>>();

            var types = new HashSet<Type>(options.PolymorphicMembers.Keys);

            foreach (var type in types)
            {
                options.PolymorphicMembers.TryGetValue(
                    type,
                    out var fluent);

                var bindings = BuildForType(
                    type,
                    fluent ?? new Dictionary<PropertyInfo, PolymorphicMemberOptions>(),
                    options.PolymorphismDefaults,
                    options.ResolutionOptions,
                    resolver);

                if (bindings.Count > 0)
                {
                    result[type] = bindings;
                }
            }

            return result;
        }

        private static IReadOnlyList<PolymorphicMemberBinding> BuildForType(
            Type type,
            Dictionary<PropertyInfo, PolymorphicMemberOptions> fluent,
            AvroPolymorphismDefaults defaults,
            AvroResolutionOptions resolution,
            AvroTypeResolver resolver)
        {
            var candidates = new HashSet<PropertyInfo>(fluent.Keys);
            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<AvroPolymorphicAttribute>(inherit: true) is not null)
                {
                    candidates.Add(property);
                }
            }

            var bindings = new List<PolymorphicMemberBinding>();
            foreach (var member in candidates)
            {
                fluent.TryGetValue(member, out var options);
                var attribute = member.GetCustomAttribute<AvroPolymorphicAttribute>(inherit: true);

                var typePath = options?.TypeDiscriminatorPath
                               ?? attribute?.TypeDiscriminator
                               ?? DiscoverMarker<AvroTypeDiscriminatorAttribute>(type);

                var versionPath = options?.VersionDiscriminatorPath
                                  ?? attribute?.VersionDiscriminator
                                  ?? DiscoverMarker<AvroVersionDiscriminatorAttribute>(type);

                var handling = ResolveHandling(options, attribute, defaults);
                var fallback = options?.FallbackType ?? attribute?.FallbackType;
                var inheritVersion = (options?.InheritVersion ?? false) || resolution.InheritSchemaVersionEnabled;

                var typeLocator = typePath is null ? null : DiscriminatorLocator.Create(type, typePath);
                var versionLocator = versionPath is null ? null : DiscriminatorLocator.Create(type, versionPath);

                ValidateHandling(type, member, handling, fallback, resolver);

                bindings.Add(new PolymorphicMemberBinding(
                    member,
                    typeLocator,
                    versionLocator,
                    options?.PayloadSchemaSource,
                    handling,
                    fallback,
                    inheritVersion));
            }

            return bindings;
        }

        private static AvroUnrecognizedTypeDiscriminatorHandling ResolveHandling(
            PolymorphicMemberOptions? options,
            AvroPolymorphicAttribute? attribute,
            AvroPolymorphismDefaults defaults)
        {
            if (options is not null && options.Handling != AvroUnrecognizedTypeDiscriminatorHandling.Unspecified)
            {
                return options.Handling;
            }

            if (attribute is not null && attribute.UnrecognizedTypeDiscriminatorHandling
                != AvroUnrecognizedTypeDiscriminatorHandling.Unspecified)
            {
                return attribute.UnrecognizedTypeDiscriminatorHandling;
            }

            return defaults.DefaultHandling == AvroUnrecognizedTypeDiscriminatorHandling.Unspecified
                ? AvroUnrecognizedTypeDiscriminatorHandling.Fail
                : defaults.DefaultHandling;
        }

        private static void ValidateHandling(
            Type type,
            PropertyInfo member,
            AvroUnrecognizedTypeDiscriminatorHandling handling,
            Type? fallback,
            AvroTypeResolver resolver)
        {
            switch (handling)
            {
                case AvroUnrecognizedTypeDiscriminatorHandling.PreservePayload:
                    if (!member.PropertyType.IsAssignableFrom(typeof(byte[])))
                    {
                        throw new AvroConfigurationException(
                            $"PreservePayload is invalid for '{type.Name}.{member.Name}': a raw byte[] payload is " +
                            $"not assignable to the declared member type '{member.PropertyType.FullName}'.");
                    }

                    break;

                case AvroUnrecognizedTypeDiscriminatorHandling.UseFallbackType:
                    if (fallback is null)
                    {
                        throw new AvroConfigurationException(
                            $"UseFallbackType is configured for '{type.Name}.{member.Name}' but no fallback type was provided.");
                    }

                    if (!resolver.Allowlist.Contains(fallback))
                    {
                        throw new AvroConfigurationException(
                            $"Fallback type '{fallback.FullName}' for '{type.Name}.{member.Name}' is not part of the " +
                            "configured/discovered allowlist.");
                    }

                    if (!member.PropertyType.IsAssignableFrom(fallback))
                    {
                        throw new AvroConfigurationException(
                            $"Fallback type '{fallback.FullName}' is not assignable to the declared member type " +
                            $"'{member.PropertyType.FullName}' for '{type.Name}.{member.Name}'.");
                    }

                    break;
            }
        }

        private static string? DiscoverMarker<TAttribute>(Type type)
            where TAttribute : Attribute
        {
            string? found = null;
            foreach (var property in type.GetProperties())
            {
                if (property.GetCustomAttribute<TAttribute>(inherit: true) is not null)
                {
                    if (found is not null)
                    {
                        throw new AvroConfigurationException(
                            $"Type '{type.FullName}' declares multiple [{typeof(TAttribute).Name}] members; the " +
                            "discriminator is ambiguous.");
                    }

                    found = property.Name;
                }
            }

            return found;
        }
    }
}
