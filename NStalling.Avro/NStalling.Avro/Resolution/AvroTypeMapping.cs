using System;

namespace NStalling.Avro.Resolution
{
    /// <summary>
    /// Source of a CLR mapping, ordered by resolution precedence. Lower numeric value wins.
    /// </summary>
    internal enum AvroMappingSource
    {
        /// <summary>Explicit programmatic registration (highest precedence).</summary>
        Explicit = 0,

        /// <summary><see cref="System.Runtime.Serialization.DataContractAttribute"/>-derived mapping.</summary>
        DataContract = 1,

        /// <summary>Exact CLR full-name convention (lowest precedence).</summary>
        ClrConvention = 2
    }

    /// <summary>
    /// A single candidate mapping produced by registration or discovery, before indexing.
    /// </summary>
    internal readonly record struct AvroTypeMapping(
        string FullName,
        string? SchemaVersion,
        Type Type,
        AvroMappingSource Source);
}
