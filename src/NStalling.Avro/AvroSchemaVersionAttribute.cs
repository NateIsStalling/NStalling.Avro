using System;

namespace NStalling.Avro
{
    /// <summary>
    /// Candidate-side metadata describing which Avro schema version(s) a CLR type may represent.
    /// The Avro full name still comes from explicit registration, <see cref="System.Runtime.Serialization.DataContractAttribute"/>,
    /// or the exact CLR full-name convention; this attribute contributes only the version qualifier.
    /// It never manufactures incoming version context for a nested schema.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct,
        AllowMultiple = true,
        Inherited = false)]
    public sealed class AvroSchemaVersionAttribute : Attribute
    {
        public AvroSchemaVersionAttribute(string version)
        {
            if (version is null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            Version = version;
        }

        public string Version { get; }
    }
}
