using System.Security.Cryptography;
using TypeIt4Me.Services;
using Xunit;

namespace TypeIt4Me.Tests;

public class CryptoServiceTests
{
    // --- GenerateSalt ---

    [Fact]
    public void GenerateSalt_ReturnsBase64String()
    {
        var salt = CryptoService.GenerateSalt();
        var bytes = Convert.FromBase64String(salt);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void GenerateSalt_ProducesUniqueSalts()
    {
        var salt1 = CryptoService.GenerateSalt();
        var salt2 = CryptoService.GenerateSalt();
        Assert.NotEqual(salt1, salt2);
    }

    // --- HashPin ---

    [Fact]
    public void HashPin_SamePinAndSalt_ProducesSameHash()
    {
        var salt = CryptoService.GenerateSalt();
        var hash1 = CryptoService.HashPin("mypin", salt);
        var hash2 = CryptoService.HashPin("mypin", salt);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashPin_DifferentPins_ProduceDifferentHashes()
    {
        var salt = CryptoService.GenerateSalt();
        var hash1 = CryptoService.HashPin("pin1", salt);
        var hash2 = CryptoService.HashPin("pin2", salt);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashPin_DifferentSalts_ProduceDifferentHashes()
    {
        var salt1 = CryptoService.GenerateSalt();
        var salt2 = CryptoService.GenerateSalt();
        var hash1 = CryptoService.HashPin("mypin", salt1);
        var hash2 = CryptoService.HashPin("mypin", salt2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashPin_EmptyPin_ReturnsEmptyString()
    {
        var salt = CryptoService.GenerateSalt();
        Assert.Equal(string.Empty, CryptoService.HashPin("", salt));
    }

    [Fact]
    public void HashPin_Returns256BitHash()
    {
        var salt = CryptoService.GenerateSalt();
        var hash = CryptoService.HashPin("test", salt);
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(32, bytes.Length); // 256 bits = 32 bytes
    }

    // --- Encrypt / Decrypt round-trip ---

    [Fact]
    public void EncryptDecrypt_RoundTrip_PreservesPlaintext()
    {
        var plaintext = "Hello, World! This is a secret message.";
        var pin = "securepin123";
        var encrypted = CryptoService.Encrypt(plaintext, pin);
        var decrypted = CryptoService.Decrypt(encrypted, pin);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_UnicodeText_PreservesContent()
    {
        var plaintext = "日本語テスト 🔐 café résumé";
        var pin = "testpin";
        var encrypted = CryptoService.Encrypt(plaintext, pin);
        var decrypted = CryptoService.Decrypt(encrypted, pin);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_LargeText_PreservesContent()
    {
        var plaintext = new string('A', 100_000);
        var pin = "testpin";
        var encrypted = CryptoService.Encrypt(plaintext, pin);
        var decrypted = CryptoService.Decrypt(encrypted, pin);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesV3Format()
    {
        var encrypted = CryptoService.Encrypt("test", "pin");
        Assert.StartsWith("V3|", encrypted);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
    {
        var pin = "mypin";
        var enc1 = CryptoService.Encrypt("same text", pin);
        var enc2 = CryptoService.Encrypt("same text", pin);
        Assert.NotEqual(enc1, enc2); // Random salt + IV each time
    }

    // --- Decrypt error cases ---

    [Fact]
    public void Decrypt_WrongPin_ThrowsCryptographicException()
    {
        var encrypted = CryptoService.Encrypt("secret", "correctpin");
        Assert.Throws<CryptographicException>(() =>
            CryptoService.Decrypt(encrypted, "wrongpin"));
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsCryptographicException()
    {
        var encrypted = CryptoService.Encrypt("secret", "pin");
        // Tamper with the base64 payload (flip a character)
        var chars = encrypted.ToCharArray();
        var idx = encrypted.IndexOf('|') + 5; // Skip "V3|" and a few chars
        chars[idx] = chars[idx] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        Assert.Throws<CryptographicException>(() =>
            CryptoService.Decrypt(tampered, "pin"));
    }

    [Fact]
    public void Decrypt_InvalidFormat_ThrowsCryptographicException()
    {
        Assert.Throws<CryptographicException>(() =>
            CryptoService.Decrypt("V2|someolddata", "pin"));
    }

    [Fact]
    public void Decrypt_TruncatedData_ThrowsCryptographicException()
    {
        Assert.Throws<CryptographicException>(() =>
            CryptoService.Decrypt("V3|AAAA", "pin"));
    }

    // --- Edge cases ---

    [Fact]
    public void Encrypt_EmptyPlaintext_ReturnsEmpty()
    {
        Assert.Equal("", CryptoService.Encrypt("", "pin"));
    }

    [Fact]
    public void Encrypt_EmptyPin_ReturnsPlaintextUnchanged()
    {
        Assert.Equal("hello", CryptoService.Encrypt("hello", ""));
    }

    [Fact]
    public void Decrypt_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", CryptoService.Decrypt("", "pin"));
    }

    [Fact]
    public void Decrypt_EmptyPin_ReturnsCiphertextUnchanged()
    {
        Assert.Equal("V3|data", CryptoService.Decrypt("V3|data", ""));
    }
}
