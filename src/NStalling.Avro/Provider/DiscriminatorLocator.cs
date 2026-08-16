using System;
using System.Globalization;

namespace NStalling.Avro.Provider
{
    /// <summary>
    /// Reads a discriminator value from a fully decoded outer object using a simple member name or a
    /// nested dot path (e.g. <c>Metadata.EventType</c>). Structurally invalid paths fail at configuration
    /// time; a null intermediate at runtime yields an absent value rather than a failure.
    /// </summary>
    internal sealed class DiscriminatorLocator
    {
        private readonly string[] _segments;

        private DiscriminatorLocator(string path, string[] segments)
        {
            Path = path;
            _segments = segments;
        }

        public string Path { get; }

        /// <summary>
        /// Builds and structurally validates a locator against <paramref name="rootType"/>. Validation
        /// stops when it reaches a member whose type is not statically inspectable (object/interface),
        /// since concrete shape is only known at runtime.
        /// </summary>
        public static DiscriminatorLocator Create(Type rootType, string path)
        {
            if (rootType is null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new AvroConfigurationException("A discriminator path must be a non-empty member path.");
            }

            var segments = path.Split('.');
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new AvroConfigurationException(
                        $"Discriminator path '{path}' contains an empty segment.");
                }
            }

            var current = rootType;
            foreach (var segment in segments)
            {
                if (current is null || current == typeof(object) || current.IsInterface || current.IsAbstract)
                {
                    // Cannot statically validate deeper; the remaining path is checked at runtime.
                    break;
                }

                var property = current.GetProperty(segment);
                if (property is null)
                {
                    throw new AvroConfigurationException(
                        $"Discriminator path '{path}' is invalid: type '{current.FullName}' has no property '{segment}'.");
                }

                current = property.PropertyType;
            }

            return new DiscriminatorLocator(path, segments);
        }

        /// <summary>
        /// Reads the discriminator value from a decoded object. Returns <see langword="false"/> with a
        /// null value when any intermediate or the final value is absent/null.
        /// </summary>
        public bool TryRead(object? root, out string? value)
        {
            value = null;
            object? current = root;
            foreach (var segment in _segments)
            {
                if (current is null)
                {
                    return false;
                }

                var property = current.GetType().GetProperty(segment);
                if (property is null)
                {
                    // Path became invalid against the runtime type; treated as absent.
                    return false;
                }

                current = property.GetValue(current);
            }

            if (current is null)
            {
                return false;
            }

            value = current as string ?? Convert.ToString(current, CultureInfo.InvariantCulture);
            return value is not null;
        }
    }
}
