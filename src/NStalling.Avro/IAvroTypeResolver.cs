using System;
using System.Diagnostics.CodeAnalysis;
using Avro;

namespace NStalling.Avro
{
    /// <summary>
    /// Resolves the application CLR type that an Avro <see cref="RecordSchema"/> should materialize as,
    /// optionally qualified by an externally supplied schema version and validated against a declared
    /// CLR member type. The resolver speaks strictly in Avro schema terms; it is never a generic
    /// arbitrary-string-to-<see cref="Type"/> resolver.
    /// </summary>
    public interface IAvroTypeResolver
    {
        /// <summary>
        /// Resolves the CLR type for <paramref name="schema"/>. Throws
        /// <see cref="AvroTypeResolutionException"/> when there is no candidate, the candidate set is
        /// ambiguous, or the resolved type is incompatible with <paramref name="declaredType"/>.
        /// </summary>
        Type Resolve(RecordSchema schema, Type? declaredType = null, string? schemaVersion = null);

        /// <summary>
        /// Attempts to resolve the CLR type. Returns <see langword="false"/> for absence, ambiguity, or
        /// declared-type incompatibility. Deterministic configuration defects may still throw.
        /// </summary>
        bool TryResolve(
            RecordSchema schema,
            Type? declaredType,
            string? schemaVersion,
            [NotNullWhen(true)] out Type? type);

        /// <summary>
        /// Resolves the CLR type or returns <see langword="null"/> when there is no candidate. Ambiguity
        /// and declared-type incompatibility still throw (absence is optional; contradiction is not).
        /// </summary>
        Type? ResolveOrDefault(RecordSchema schema, Type? declaredType = null, string? schemaVersion = null);
    }
}
