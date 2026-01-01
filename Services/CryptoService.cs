using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TypeIt4Me.Services
{
    /// <summary>
    /// Provides cryptographic services including PIN hashing (PBKDF2) and AES-256 encryption.
    /// Supports V2 format with per-file salts and random IVs.
    /// </summary>
    public static class CryptoService
    {
        // 32 bytes = 256 bits for salt
        private const int SaltSize = 32;
        // PBKDF2 iterations (OWASP recommended 600,000 for HMAC-SHA256)
        private const int Iterations = 600000;
        
        /// <summary>
        /// Generates a cryptographically strong random salt.
        /// </summary>
        public static string GenerateSalt()
        {
            var bytes = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static string HashPin(string pin, string salt)
        {
            if (string.IsNullOrEmpty(pin)) return string.Empty;
            
            byte[] saltBytes = Convert.FromBase64String(salt);
            byte[]? hash = null;
            
            try 
            {
                using var pbkdf2 = new Rfc2898DeriveBytes(pin, saltBytes, Iterations, HashAlgorithmName.SHA256);
                hash = pbkdf2.GetBytes(32); // 256-bit hash
                return Convert.ToBase64String(hash);
            }
            finally
            {
                if (hash != null) Array.Clear(hash, 0, hash.Length);
                // Note: saltBytes is from base64 string, harder to clear effectively in managed code without pinning, 
                // but we clear the output hash at least.
            }
        }

        public static string Encrypt(string plainText, string pin)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            if (string.IsNullOrEmpty(pin)) return plainText;

            // V2 Encryption:
            // 1. Generate Random Salt (for Key Derivation)
            // 2. Derive Key & IV (Randomized by Salt)
            // 3. Encrypt
            // 4. Format: "V2|" + Base64(Salt + IV + CipherText)
            
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            byte[]? key = null;
            byte[]? iv = null;

            try
            {
                using var aes = Aes.Create();
                using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
                
                key = keyDerivation.GetBytes(32); // 256-bit Key
                iv = keyDerivation.GetBytes(16);  // 128-bit IV
                
                aes.Key = key;
                aes.IV = iv;

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                
                // Write Salt and IV to stream first (unencrypted)
                ms.Write(salt, 0, salt.Length);
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }
                
                return "V2|" + Convert.ToBase64String(ms.ToArray());
            }
            finally
            {
                if (key != null) Array.Clear(key, 0, key.Length);
                if (iv != null) Array.Clear(iv, 0, iv.Length);
            }
        }

        public static string? Decrypt(string cipherText, string pin)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            if (string.IsNullOrEmpty(pin)) return cipherText;

            byte[]? key = null;

            try 
            {
                if (cipherText.StartsWith("V2|"))
                {
                    // V2 Decryption
                    string base64Payload = cipherText.Substring(3); // Remove "V2|"
                    byte[] fullBytes = Convert.FromBase64String(base64Payload);
                    
                    using var ms = new MemoryStream(fullBytes);
                    
                    // Read Salt
                    byte[] salt = new byte[SaltSize];
                    if (ms.Read(salt, 0, salt.Length) != salt.Length) return null; // Invalid
                    
                    // Read IV
                    byte[] iv = new byte[16];
                    if (ms.Read(iv, 0, iv.Length) != iv.Length) return null; // Invalid

                    // Derive Key (using extracted Salt)
                    using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
                    key = keyDerivation.GetBytes(32);
                    
                    // Decrypt
                    using var aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    
                    using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);
                    
                    return sr.ReadToEnd();
                }
                else
                {
                    // Legacy V1 Decryption (Static Salt)
                    byte[] cipherBytes = Convert.FromBase64String(cipherText);
                    byte[] salt = Encoding.UTF8.GetBytes("TypeIt4Me_Secure_Storage_Salt_2025");
    
                    using var aes = Aes.Create();
                    using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
                    key = keyDerivation.GetBytes(32);
                    byte[] iv = keyDerivation.GetBytes(16);
    
                    using var decryptor = aes.CreateDecryptor(key, iv);
                    using var ms = new MemoryStream(cipherBytes);
                    using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                    using var sr = new StreamReader(cs);
                    
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (key != null) Array.Clear(key, 0, key.Length);
            }
        }
        // Legacy support
        public static string ComputeHashLegacy(string input)
        {
             using var sha = System.Security.Cryptography.SHA256.Create();
             var bytes = System.Text.Encoding.UTF8.GetBytes(input);
             var hash = sha.ComputeHash(bytes);
             return Convert.ToBase64String(hash);
        }
    }
}
