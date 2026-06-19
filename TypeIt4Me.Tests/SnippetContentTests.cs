using System.Text.Json;
using TypeIt4Me.Models;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class SnippetContentTests
{
    private static string PlainJson(params (string name, string content)[] items)
    {
        var list = new List<Snippet>();
        foreach (var (name, content) in items)
            list.Add(new Snippet { Name = name, Content = content, Category = "Test" });
        return JsonSerializer.Serialize(list);
    }

    [Fact]
    public void TryDeserialize_PlainJson_ReturnsSnippets()
    {
        string json = PlainJson(("Greeting", "Hello"));

        var result = SnippetManager.TryDeserializeSnippets(json, null);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("Greeting", result![0].Name);
        Assert.Equal("Hello", result[0].Content);
    }

    [Fact]
    public void TryDeserialize_EncryptedWithCorrectPin_ReturnsSnippets()
    {
        string encrypted = CryptoService.Encrypt(PlainJson(("Secret", "Top secret")), "1234");
        Assert.StartsWith("V3|", encrypted); // confirm the fixture is actually encrypted

        var result = SnippetManager.TryDeserializeSnippets(encrypted, "1234");

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("Secret", result![0].Name);
        Assert.Equal("Top secret", result[0].Content);
    }

    [Fact]
    public void TryDeserialize_EncryptedWithWrongPin_ReturnsNull()
    {
        string encrypted = CryptoService.Encrypt(PlainJson(("Secret", "x")), "1234");

        Assert.Null(SnippetManager.TryDeserializeSnippets(encrypted, "9999"));
    }

    [Fact]
    public void TryDeserialize_EncryptedWithNoPin_ReturnsNull()
    {
        string encrypted = CryptoService.Encrypt(PlainJson(("Secret", "x")), "1234");

        Assert.Null(SnippetManager.TryDeserializeSnippets(encrypted, null));
    }

    [Fact]
    public void TryDeserialize_GarbageContent_ReturnsNull()
    {
        Assert.Null(SnippetManager.TryDeserializeSnippets("not json at all", null));
    }
}
