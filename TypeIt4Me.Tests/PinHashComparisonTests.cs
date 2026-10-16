using System;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class PinHashComparisonTests
{
    [Fact]
    public void Equal_NullFirst_ReturnsFalse()
    {
        Assert.False(PinHashComparison.Equal(null, "somehash"));
    }

    [Fact]
    public void Equal_NullSecond_ReturnsFalse()
    {
        Assert.False(PinHashComparison.Equal("somehash", null));
    }

    [Fact]
    public void Equal_EmptyStrings_ReturnsFalse()
    {
        Assert.False(PinHashComparison.Equal(string.Empty, string.Empty));
    }

    [Fact]
    public void Equal_InvalidBase64_ReturnsFalse()
    {
        Assert.False(PinHashComparison.Equal("!@#$", "somehash"));
    }

    [Fact]
    public void Equal_DifferentLengthBase64_ReturnsFalse()
    {
        var hash1 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var hash2 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        Assert.False(PinHashComparison.Equal(hash1, hash2));
    }

    [Fact]
    public void Equal_SameHashes_ReturnsTrue()
    {
        var hash = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        Assert.True(PinHashComparison.Equal(hash, hash));
    }

    [Fact]
    public void Equal_DifferentHashes_ReturnsFalse()
    {
        var hash1 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });
        var hash2 = Convert.ToBase64String(new byte[] { 5, 4, 3, 2, 1 });
        Assert.False(PinHashComparison.Equal(hash1, hash2));
    }
}
