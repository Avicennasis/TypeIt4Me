using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TypeIt4Me.Services
{
    /// <summary>
    /// Provides cryptographic services including PIN hashing (PBKDF2) and AES-256 encryption.
    /// Supports V2 format with per-file salts and random IVs.
    /// V3 adds HMAC-SHA256 for authenticated encryption.
    /// </summary>
    public static class CryptoService
    {
        // Cryptographic constants
        private const int SaltSize = 32;           // 32 bytes = 256 bits for salt
        private const int IVSize = 16;             // 16 bytes = 128 bits for AES IV
        private const int HMACSize = 32;           // 32 bytes = 256 bits for HMAC-SHA256
        private const int Iterations = 600000;     // OWASP recommended iterations for PBKDF2-SHA256
        private const int MaxPlainTextSize = 10 * 1024 * 1024; // 10 MB limit to prevent DoS
        
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

            // Input validation to prevent DoS
            if (plainText.Length > MaxPlainTextSize)
            {
                throw new ArgumentException($"Plaintext exceeds maximum size of {MaxPlainTextSize} bytes");
            }

            // V3 Encryption with HMAC Authentication:
            // 1. Generate Random Salt (for Key Derivation)
            // 2. Derive Encryption Key + HMAC Key
            // 3. Encrypt plaintext
            // 4. Compute HMAC over (Salt + IV + CipherText)
            // 5. Format: "V3|" + Base64(Salt + IV + CipherText + HMAC)

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            byte[]? encryptionKey = null;
            byte[]? hmacKey = null;
            byte[]? iv = null;

            try
            {
                using var aes = Aes.Create();
                using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);

                // Derive separate keys for encryption and authentication
                encryptionKey = keyDerivation.GetBytes(32); // 256-bit AES Key
                iv = keyDerivation.GetBytes(16);            // 128-bit IV
                hmacKey = keyDerivation.GetBytes(32);       // 256-bit HMAC Key

                aes.Key = encryptionKey;
                aes.IV = iv;

                using var ms = new MemoryStream();

                // Write Salt and IV (unencrypted, needed for decryption)
                ms.Write(salt, 0, salt.Length);
                ms.Write(iv, 0, iv.Length);

                // Encrypt the plaintext
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(plainText);
                }

                // Get the encrypted payload (Salt + IV + CipherText)
                byte[] encryptedPayload = ms.ToArray();

                // Compute HMAC over entire payload for authentication
                byte[] hmac;
                using (var hmacAlg = new HMACSHA256(hmacKey))
                {
                    hmac = hmacAlg.ComputeHash(encryptedPayload);
                }

                // Append HMAC to the end
                byte[] finalPayload = new byte[encryptedPayload.Length + hmac.Length];
                Buffer.BlockCopy(encryptedPayload, 0, finalPayload, 0, encryptedPayload.Length);
                Buffer.BlockCopy(hmac, 0, finalPayload, encryptedPayload.Length, hmac.Length);

                return "V3|" + Convert.ToBase64String(finalPayload);
            }
            finally
            {
                // Securely clear sensitive key material
                if (encryptionKey != null) Array.Clear(encryptionKey, 0, encryptionKey.Length);
                if (hmacKey != null) Array.Clear(hmacKey, 0, hmacKey.Length);
                if (iv != null) Array.Clear(iv, 0, iv.Length);
            }
        }

        public static string? Decrypt(string cipherText, string pin)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            if (string.IsNullOrEmpty(pin)) return cipherText;

            byte[]? encryptionKey = null;
            byte[]? hmacKey = null;

            try
            {
                // Only support V3 format with HMAC authentication
                if (!cipherText.StartsWith("V3|"))
                {
                    throw new CryptographicException("Invalid or unsupported encryption format. Only V3 format is supported.");
                }

                // Remove "V3|" prefix
                string base64Payload = cipherText.Substring(3);
                byte[] fullBytes = Convert.FromBase64String(base64Payload);

                // Minimum size validation: Salt + IV + (at least 1 block of ciphertext) + HMAC
                int minimumSize = SaltSize + IVSize + 16 + HMACSize; // 16 = minimum AES block size
                if (fullBytes.Length < minimumSize)
                {
                    throw new CryptographicException("Encrypted data is too short to be valid.");
                }

                // Extract HMAC (last 32 bytes)
                byte[] storedHmac = new byte[HMACSize];
                byte[] encryptedPayload = new byte[fullBytes.Length - HMACSize];

                Buffer.BlockCopy(fullBytes, 0, encryptedPayload, 0, encryptedPayload.Length);
                Buffer.BlockCopy(fullBytes, encryptedPayload.Length, storedHmac, 0, HMACSize);

                // Read from encryptedPayload (Salt + IV + CipherText)
                using var ms = new MemoryStream(encryptedPayload);

                // Read Salt
                byte[] salt = new byte[SaltSize];
                if (ms.Read(salt, 0, salt.Length) != salt.Length)
                {
                    throw new CryptographicException("Failed to read salt from encrypted data.");
                }

                // Read IV
                byte[] iv = new byte[IVSize];
                if (ms.Read(iv, 0, iv.Length) != iv.Length)
                {
                    throw new CryptographicException("Failed to read IV from encrypted data.");
                }

                // Derive keys from PIN
                using var keyDerivation = new Rfc2898DeriveBytes(pin, salt, Iterations, HashAlgorithmName.SHA256);
                encryptionKey = keyDerivation.GetBytes(32); // 256-bit AES Key
                keyDerivation.GetBytes(16); // Skip the IV bytes we derived during encryption
                hmacKey = keyDerivation.GetBytes(32);       // 256-bit HMAC Key

                // Verify HMAC before attempting decryption (Authenticate-then-Decrypt)
                byte[] computedHmac;
                using (var hmacAlg = new HMACSHA256(hmacKey))
                {
                    computedHmac = hmacAlg.ComputeHash(encryptedPayload);
                }

                // Constant-time comparison to prevent timing attacks
                bool hmacValid = true;
                for (int i = 0; i < HMACSize; i++)
                {
                    hmacValid &= (storedHmac[i] == computedHmac[i]);
                }

                if (!hmacValid)
                {
                    throw new CryptographicException("HMAC validation failed. Data may be corrupted or tampered with, or PIN is incorrect.");
                }

                // HMAC is valid, proceed with decryption
                using var aes = Aes.Create();
                aes.Key = encryptionKey;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (CryptographicException)
            {
                // Re-throw cryptographic exceptions with their specific messages
                throw;
            }
            catch (Exception ex)
            {
                // Wrap other exceptions to avoid information leakage
                throw new CryptographicException("Decryption failed. The data may be corrupted or the PIN may be incorrect.", ex);
            }
            finally
            {
                // Securely clear sensitive key material
                if (encryptionKey != null) Array.Clear(encryptionKey, 0, encryptionKey.Length);
                if (hmacKey != null) Array.Clear(hmacKey, 0, hmacKey.Length);
            }
        }
    }
}
