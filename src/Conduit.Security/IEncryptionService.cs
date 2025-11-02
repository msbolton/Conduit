using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Security
{
    /// <summary>
    /// Interface for encryption and decryption services.
    /// Provides symmetric and asymmetric encryption capabilities.
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// Gets the name of this encryption service.
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// Gets the supported encryption algorithms.
        /// </summary>
        IEnumerable<EncryptionAlgorithm> SupportedAlgorithms { get; }

        /// <summary>
        /// Encrypts data using the default encryption key.
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encrypted data</returns>
        Task<byte[]> EncryptAsync(
            byte[] data,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts data using a specific key.
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <param name="keyId">The key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encrypted data</returns>
        Task<byte[]> EncryptAsync(
            byte[] data,
            string keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts data with a specific algorithm and key.
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <param name="keyId">The key identifier</param>
        /// <param name="algorithm">The encryption algorithm to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Encrypted data</returns>
        Task<byte[]> EncryptAsync(
            byte[] data,
            string keyId,
            EncryptionAlgorithm algorithm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts data.
        /// </summary>
        /// <param name="encryptedData">The encrypted data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decrypted data</returns>
        Task<byte[]> DecryptAsync(
            byte[] encryptedData,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts data using a specific key.
        /// </summary>
        /// <param name="encryptedData">The encrypted data</param>
        /// <param name="keyId">The key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Decrypted data</returns>
        Task<byte[]> DecryptAsync(
            byte[] encryptedData,
            string keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Encrypts a stream.
        /// </summary>
        /// <param name="inputStream">The input stream to encrypt</param>
        /// <param name="outputStream">The output stream for encrypted data</param>
        /// <param name="keyId">Optional key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            string? keyId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts a stream.
        /// </summary>
        /// <param name="inputStream">The encrypted input stream</param>
        /// <param name="outputStream">The output stream for decrypted data</param>
        /// <param name="keyId">Optional key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            string? keyId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a new encryption key.
        /// </summary>
        /// <param name="keyId">The key identifier</param>
        /// <param name="algorithm">The encryption algorithm</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Key metadata</returns>
        Task<KeyMetadata> GenerateKeyAsync(
            string keyId,
            EncryptionAlgorithm algorithm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Rotates an encryption key (generates new version).
        /// </summary>
        /// <param name="keyId">The key identifier to rotate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>New key metadata</returns>
        Task<KeyMetadata> RotateKeyAsync(
            string keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metadata for a key.
        /// </summary>
        /// <param name="keyId">The key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Key metadata</returns>
        Task<KeyMetadata> GetKeyMetadataAsync(
            string keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a key.
        /// </summary>
        /// <param name="keyId">The key identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteKeyAsync(
            string keyId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lists all key identifiers.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of key identifiers</returns>
        Task<IEnumerable<string>> ListKeysAsync(
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Encryption algorithms.
    /// </summary>
    public enum EncryptionAlgorithm
    {
        /// <summary>AES-256 with Galois/Counter Mode</summary>
        Aes256Gcm,

        /// <summary>AES-128 with Galois/Counter Mode</summary>
        Aes128Gcm,

        /// <summary>AES-256 with Cipher Block Chaining</summary>
        Aes256Cbc,

        /// <summary>AES-128 with Cipher Block Chaining</summary>
        Aes128Cbc,

        /// <summary>RSA with 2048-bit key</summary>
        Rsa2048,

        /// <summary>RSA with 4096-bit key</summary>
        Rsa4096,

        /// <summary>Elliptic Curve with P-256 curve</summary>
        EcdsaP256,

        /// <summary>Elliptic Curve with P-384 curve</summary>
        EcdsaP384,

        /// <summary>ChaCha20-Poly1305</summary>
        ChaCha20Poly1305
    }

    /// <summary>
    /// Key metadata information.
    /// </summary>
    public class KeyMetadata
    {
        /// <summary>
        /// Gets or sets the key identifier.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encryption algorithm.
        /// </summary>
        public EncryptionAlgorithm Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the key size in bits.
        /// </summary>
        public int KeySize { get; set; }

        /// <summary>
        /// Gets or sets when the key was created (UTC).
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the key expires (UTC), null if no expiration.
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the key version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets whether the key is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets additional metadata.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Checks if the key is expired.
        /// </summary>
        public bool IsExpired()
        {
            return ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
        }

        /// <summary>
        /// Checks if the key is valid (enabled and not expired).
        /// </summary>
        public bool IsValid()
        {
            return IsEnabled && !IsExpired();
        }
    }

    /// <summary>
    /// Encryption options.
    /// </summary>
    public class EncryptionOptions
    {
        /// <summary>
        /// Gets or sets the default encryption algorithm.
        /// </summary>
        public EncryptionAlgorithm DefaultAlgorithm { get; set; } = EncryptionAlgorithm.Aes256Gcm;

        /// <summary>
        /// Gets or sets whether to enable automatic key rotation.
        /// </summary>
        public bool EnableAutoKeyRotation { get; set; } = false;

        /// <summary>
        /// Gets or sets the key rotation interval.
        /// </summary>
        public TimeSpan KeyRotationInterval { get; set; } = TimeSpan.FromDays(30);

        /// <summary>
        /// Gets or sets the key expiration duration.
        /// </summary>
        public TimeSpan? KeyExpiration { get; set; }

        /// <summary>
        /// Gets or sets the key store path.
        /// </summary>
        public string? KeyStorePath { get; set; }

        /// <summary>
        /// Gets or sets the key store password.
        /// </summary>
        public string? KeyStorePassword { get; set; }

        /// <summary>
        /// Gets or sets whether to use hardware security modules (HSM).
        /// </summary>
        public bool UseHardwareSecurityModule { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable field-level encryption.
        /// </summary>
        public bool EnableFieldLevelEncryption { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable envelope encryption.
        /// </summary>
        public bool EnableEnvelopeEncryption { get; set; } = false;
    }

    /// <summary>
    /// Encryption result with metadata.
    /// </summary>
    public class EncryptionResult
    {
        /// <summary>
        /// Gets or sets the encrypted data.
        /// </summary>
        public byte[] EncryptedData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the key identifier used for encryption.
        /// </summary>
        public string KeyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the algorithm used.
        /// </summary>
        public EncryptionAlgorithm Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the initialization vector (IV) if applicable.
        /// </summary>
        public byte[]? InitializationVector { get; set; }

        /// <summary>
        /// Gets or sets the authentication tag for AEAD algorithms.
        /// </summary>
        public byte[]? AuthenticationTag { get; set; }

        /// <summary>
        /// Gets or sets additional authenticated data (AAD).
        /// </summary>
        public byte[]? AdditionalData { get; set; }

        /// <summary>
        /// Gets or sets when the encryption was performed (UTC).
        /// </summary>
        public DateTime EncryptedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Extension methods for encryption algorithms.
    /// </summary>
    public static class EncryptionAlgorithmExtensions
    {
        /// <summary>
        /// Gets the key size in bits for the algorithm.
        /// </summary>
        public static int GetKeySize(this EncryptionAlgorithm algorithm)
        {
            return algorithm switch
            {
                EncryptionAlgorithm.Aes128Gcm => 128,
                EncryptionAlgorithm.Aes128Cbc => 128,
                EncryptionAlgorithm.Aes256Gcm => 256,
                EncryptionAlgorithm.Aes256Cbc => 256,
                EncryptionAlgorithm.Rsa2048 => 2048,
                EncryptionAlgorithm.Rsa4096 => 4096,
                EncryptionAlgorithm.EcdsaP256 => 256,
                EncryptionAlgorithm.EcdsaP384 => 384,
                EncryptionAlgorithm.ChaCha20Poly1305 => 256,
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };
        }

        /// <summary>
        /// Checks if the algorithm is symmetric.
        /// </summary>
        public static bool IsSymmetric(this EncryptionAlgorithm algorithm)
        {
            return algorithm switch
            {
                EncryptionAlgorithm.Aes128Gcm => true,
                EncryptionAlgorithm.Aes128Cbc => true,
                EncryptionAlgorithm.Aes256Gcm => true,
                EncryptionAlgorithm.Aes256Cbc => true,
                EncryptionAlgorithm.ChaCha20Poly1305 => true,
                EncryptionAlgorithm.Rsa2048 => false,
                EncryptionAlgorithm.Rsa4096 => false,
                EncryptionAlgorithm.EcdsaP256 => false,
                EncryptionAlgorithm.EcdsaP384 => false,
                _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
            };
        }

        /// <summary>
        /// Checks if the algorithm supports AEAD (Authenticated Encryption with Associated Data).
        /// </summary>
        public static bool IsAead(this EncryptionAlgorithm algorithm)
        {
            return algorithm switch
            {
                EncryptionAlgorithm.Aes128Gcm => true,
                EncryptionAlgorithm.Aes256Gcm => true,
                EncryptionAlgorithm.ChaCha20Poly1305 => true,
                _ => false
            };
        }

        /// <summary>
        /// Gets the algorithm name as a string.
        /// </summary>
        public static string GetAlgorithmName(this EncryptionAlgorithm algorithm)
        {
            return algorithm switch
            {
                EncryptionAlgorithm.Aes128Gcm => "AES-128-GCM",
                EncryptionAlgorithm.Aes128Cbc => "AES-128-CBC",
                EncryptionAlgorithm.Aes256Gcm => "AES-256-GCM",
                EncryptionAlgorithm.Aes256Cbc => "AES-256-CBC",
                EncryptionAlgorithm.Rsa2048 => "RSA-2048",
                EncryptionAlgorithm.Rsa4096 => "RSA-4096",
                EncryptionAlgorithm.EcdsaP256 => "ECDSA-P256",
                EncryptionAlgorithm.EcdsaP384 => "ECDSA-P384",
                EncryptionAlgorithm.ChaCha20Poly1305 => "ChaCha20-Poly1305",
                _ => algorithm.ToString()
            };
        }
    }
}
