namespace NStalling.Avro.Resolution
{
    /// <summary>
    /// Internal composite key used to index CLR mappings. This is an implementation detail and is not
    /// part of the public Avro identity surface. Avro identity remains <c>namespace + name</c>;
    /// <see cref="SchemaVersion"/> is external resolution context.
    /// </summary>
    internal readonly record struct AvroTypeResolutionKey(string FullName, string? SchemaVersion);
}
