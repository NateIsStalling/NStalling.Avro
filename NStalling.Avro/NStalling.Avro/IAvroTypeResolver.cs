using System;
using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro;

/// <summary>
/// Resolves Apache Avro named schemas to CLR types.
/// 
/// The resolver accepts an optional declared type (e.g., from a property's compile-time type annotation)
/// and an optional schemaVersion qualifier for version-aware resolution.
/// 
/// schemaVersion is an application-defined resolution qualifier independent from Avro identity.
/// It does not imply any registry-specific versioning model and is not part of the Avro fullname.
/// </summary>
public interface IAvroTypeResolver
{
    /// <summary>
    /// Resolves a NamedSchema to a CLR type.
    /// </summary>
    /// <param name="schema">The Apache NamedSchema to resolve. Cannot be null.</param>
    /// <param name="declaredType">
    /// Optional compile-time type annotation (e.g., a property's declared type).
    /// If provided, the resolved type must be assignable to declaredType.
    /// Treat object as unrestricted.
    /// </param>
    /// <param name="schemaVersion">
    /// Optional application-defined resolution qualifier.
    /// Enables version-aware resolution when multiple CLR types map to the same Avro fullname.
    /// </param>
    /// <returns>The resolved CLR type.</returns>
    /// <exception cref="AvroTypeResolutionException">
    /// Thrown when:
    /// - No candidate type is found
    /// - Multiple equally valid candidates exist
    /// - The resolved type is incompatible with declaredType
    /// - Invalid configuration is detected
    /// </exception>
    Type Resolve(
        NamedSchema schema,
        Type? declaredType = null,
        string? schemaVersion = null);

    /// <summary>
    /// Attempts to resolve a NamedSchema to a CLR type.
    /// Returns false for not-found, ambiguous, or incompatible resolution (no exception).
    /// Returns false for invalid configuration only if it would hide a configuration defect.
    /// </summary>
    bool TryResolve(
        NamedSchema schema,
        Type? declaredType,
        string? schemaVersion,
        [NotNullWhen(true)] out Type? type);

    /// <summary>
    /// Resolves a NamedSchema to a CLR type or null.
    /// 
    /// Returns null only when the schema is not found.
    /// Throws for ambiguous, incompatible, or invalid configuration scenarios.
    /// </summary>
    /// <remarks>
    /// Principle: Absence is optional; contradiction is not.
    /// </remarks>
    Type? ResolveOrDefault(
        NamedSchema schema,
        Type? declaredType = null,
        string? schemaVersion = null);
}

