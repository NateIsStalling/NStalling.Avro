using System;
using System.Collections.Generic;
using Avro;

namespace NStalling.Avro.Reflection
{
    /// <summary>
    /// Internal walker that enumerates the distinct <see cref="RecordSchema"/> nodes reachable from a
    /// schema, deduplicating by record full name and terminating on recursive and diamond references.
    /// </summary>
    internal static class SchemaGraphWalker
    {
        public static IEnumerable<RecordSchema> EnumerateRecordSchemas(Schema schema)
        {
            if (schema is null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            var results = new List<RecordSchema>();
            Walk(schema, visited, results);
            return results;
        }

        private static void Walk(Schema schema, HashSet<string> visited, List<RecordSchema> results)
        {
            switch (schema)
            {
                case RecordSchema record:
                    if (!visited.Add(record.Fullname))
                    {
                        return;
                    }

                    results.Add(record);
                    foreach (var field in record.Fields)
                    {
                        Walk(field.Schema, visited, results);
                    }

                    break;

                case UnionSchema union:
                    foreach (var branch in union.Schemas)
                    {
                        Walk(branch, visited, results);
                    }

                    break;

                case ArraySchema array:
                    Walk(array.ItemSchema, visited, results);
                    break;

                case MapSchema map:
                    Walk(map.ValueSchema, visited, results);
                    break;
            }
        }
    }
}
