using System;
using System.Collections.Generic;
using Avro;

namespace NStalling.Avro;

/// <summary>
/// Traverses an Avro schema graph and enumerates all named schemas.
/// 
/// This is useful for discovering named schemas that may require CLR type mappings
/// before Apache performs reflection-based deserialization.
/// </summary>
public static class SchemaGraphWalker
{
    /// <summary>
    /// Enumerates all distinct named schemas reachable from the given root schema.
    /// 
    /// Traverses:
    /// - RecordSchema fields
    /// - UnionSchema branches
    /// - ArraySchema element type
    /// - MapSchema value type
    /// 
    /// Avoids infinite recursion by tracking visited fullnames.
    /// </summary>
    public static IEnumerable<NamedSchema> EnumerateNamedSchemas(Schema schema)
    {
        if (schema is null) throw new ArgumentNullException(nameof(schema));
        return EnumerateNamedSchemasImpl(schema, new HashSet<string>());
    }

    private static IEnumerable<NamedSchema> EnumerateNamedSchemasImpl(Schema schema, HashSet<string> visited)
    {
        if (schema is NamedSchema namedSchema)
        {
            // Yield this named schema if not already visited
            if (visited.Add(namedSchema.Fullname))
            {
                yield return namedSchema;

                // Continue traversing into the named schema's contents
                schema = namedSchema;
            }
            else
            {
                // Already visited; return to avoid infinite recursion
                yield break;
            }
        }

        // Traverse into schema-specific sub-schemas
        switch (schema)
        {
            case RecordSchema recordSchema:
                foreach (var field in recordSchema.Fields)
                {
                    foreach (var nested in EnumerateNamedSchemasImpl(field.Schema, visited))
                    {
                        yield return nested;
                    }
                }
                break;

            case UnionSchema unionSchema:
                foreach (var branch in unionSchema.Schemas)
                {
                    foreach (var nested in EnumerateNamedSchemasImpl(branch, visited))
                    {
                        yield return nested;
                    }
                }
                break;

            case ArraySchema arraySchema:
                foreach (var nested in EnumerateNamedSchemasImpl(arraySchema.ItemSchema, visited))
                {
                    yield return nested;
                }
                break;

            case MapSchema mapSchema:
                foreach (var nested in EnumerateNamedSchemasImpl(mapSchema.ValueSchema, visited))
                {
                    yield return nested;
                }
                break;
        }
    }
}

