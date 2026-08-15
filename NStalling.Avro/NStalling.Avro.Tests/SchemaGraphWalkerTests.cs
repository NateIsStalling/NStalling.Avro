using Avro;

namespace NStalling.Avro.Tests;

public class SchemaGraphWalkerTests
{
        [Fact]
        public void SchemaGraphWalker_EnumeratesNestedSchemas()
        {
            var rootSchema = Schema.Parse("""
            {
              "type": "record",
              "name": "Envelope",
              "fields": [
                {
                  "name": "payload",
                  "type": {
                    "type": "record",
                    "name": "Payload",
                    "fields": []
                  }
                }
              ]
            }
            """);

            var namedSchemas = SchemaGraphWalker.EnumerateNamedSchemas(rootSchema).ToList();

            // Should find both Envelope and Payload
            Assert.NotEmpty(namedSchemas);
            var fullnames = namedSchemas.Select(s => s.Fullname).ToList();
            Assert.Contains("Envelope", fullnames);
            Assert.Contains("Payload", fullnames);
        }

        [Fact]
        public void SchemaGraphWalker_HandlesUnionSchemas()
        {
            var rootSchema = Schema.Parse("""
            {
              "type": "record",
              "name": "UnionExample",
              "fields": [
                {
                  "name": "value",
                  "type": [
                    "null",
                    {
                      "type": "record",
                      "name": "Option1",
                      "fields": []
                    },
                    {
                      "type": "record",
                      "name": "Option2",
                      "fields": []
                    }
                  ]
                }
              ]
            }
            """);

            var namedSchemas = SchemaGraphWalker.EnumerateNamedSchemas(rootSchema).ToList();
            var fullnames = namedSchemas.Select(s => s.Fullname).ToList();

            Assert.Contains("UnionExample", fullnames);
            Assert.Contains("Option1", fullnames);
            Assert.Contains("Option2", fullnames);
        }

        [Fact]
        public void SchemaGraphWalker_HandlesArraySchemas()
        {
            var rootSchema = Schema.Parse("""
            {
              "type": "record",
              "name": "ArrayContainer",
              "fields": [
                {
                  "name": "items",
                  "type": {
                    "type": "array",
                    "items": {
                      "type": "record",
                      "name": "Item",
                      "fields": []
                    }
                  }
                }
              ]
            }
            """);

            var namedSchemas = SchemaGraphWalker.EnumerateNamedSchemas(rootSchema).ToList();
            var fullnames = namedSchemas.Select(s => s.Fullname).ToList();

            Assert.Contains("ArrayContainer", fullnames);
            Assert.Contains("Item", fullnames);
        }

        [Fact]
        public void SchemaGraphWalker_HandlesMapSchemas()
        {
            var rootSchema = Schema.Parse("""
            {
              "type": "record",
              "name": "MapContainer",
              "fields": [
                {
                  "name": "values",
                  "type": {
                    "type": "map",
                    "values": {
                      "type": "record",
                      "name": "MapValue",
                      "fields": []
                    }
                  }
                }
              ]
            }
            """);

            var namedSchemas = SchemaGraphWalker.EnumerateNamedSchemas(rootSchema).ToList();
            var fullnames = namedSchemas.Select(s => s.Fullname).ToList();

            Assert.Contains("MapContainer", fullnames);
            Assert.Contains("MapValue", fullnames);
        }

        [Fact]
        public void SchemaGraphWalker_AvoidsCycles()
        {
            var rootSchema = Schema.Parse("""
            {
              "type": "record",
              "name": "Node",
              "fields": [
                {"name": "value", "type": "int"},
                {"name": "parent", "type": ["null", "Node"]}
              ]
            }
            """);

            var namedSchemas = SchemaGraphWalker.EnumerateNamedSchemas(rootSchema).ToList();

            // Should enumerate Node exactly once despite the recursive reference
            var nodeCount = namedSchemas.Count(s => s.Fullname == "Node");
            Assert.Equal(1, nodeCount);
        }
}