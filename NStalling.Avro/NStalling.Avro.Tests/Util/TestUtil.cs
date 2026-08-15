namespace NStalling.Avro.Tests.Util;

public static class TestUtil
{
    public static string ReadFixture(string fixtureName)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName);
        return File.ReadAllText(fixturePath);
    }
}