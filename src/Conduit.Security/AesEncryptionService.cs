using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Security
{
    /// <summary>
    /// AES-based encryption service supporting multiple AES modes.
    /// Implements symmetric encryption using AES with GCM and CBC modes.
    /// </summary>
    public class AesEncryptionService : IEncryptionService
    {
        private readonly EncryptionOptions _options;
        private readonly ILogger? _logger;
        private readonly ConcurrentDictionary<string, KeyInfo> _keys;
        private readonly SemaphoreSlim _keyGenerationLock;
        private const int NonceSize = 12; // 96 bits for GCM
        private const int TagSize = 16; // 128 bits for GCM
        private const int IvSize = 16; // 128 bits for CBC

        /// <inheritdoc/>
        public string ServiceName => "AES";

        /// <inheritdoc/>
        public IEnumerable<EncryptionAlgorithm> SupportedAlgorithms => new[]
        {
            EncryptionAlgorithm.Aes128Gcm,
            EncryptionAlgorithm.Aes256Gcm,
            EncryptionAlgorithm.Aes128Cbc,
            EncryptionAlgorithm.Aes256Cbc
        };

        /// <summary>
        /// Initializes a new instance of the AesEncryptionService class.
        /// </summary>
        public AesEncryptionService(
            EncryptionOptions options,
            ILogger<AesEncryptionService>? logger = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
            _keys = new ConcurrentDictionary<string, KeyInfo>();
            _keyGenerationLock = new SemaphoreSlim(1, 1);

            // Generate default key if not exists
            EnsureDefaultKeyExists();
        }

        /// <inheritdoc/>
        public Task<byte[]> EncryptAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            return EncryptAsync(data, "default", cancellationToken);
        }

        /// <inheritdoc/>
        public Task<byte[]> EncryptAsync(
            byte[] data,
            string keyId,
            CancellationToken cancellationToken = default)
        {
            return EncryptAsync(data, keyId, _options.DefaultAlgorithm, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<byte[]> EncryptAsync(
            byte[] data,
            string keyId,
            EncryptionAlgorithm algorithm,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(data, nameof(data));
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (!SupportedAlgorithms.Contains(algorithm))
            {
                throw new NotSupportedException($"Algorithm {algorithm} is not supported by AES encryption service");
            }

            var keyInfo = GetOrCreateKey(keyId, algorithm);

            if (!keyInfo.Metadata.IsValid())
            {
                throw new InvalidOperationException($"Key {keyId} is not valid (disabled or expired)");
            }

            try
            {
                var encryptedData = algorithm switch
                {
                    EncryptionAlgorithm.Aes128Gcm or EncryptionAlgorithm.Aes256Gcm => EncryptGcm(data, keyInfo.Key),
                    EncryptionAlgorithm.Aes128Cbc or EncryptionAlgorithm.Aes256Cbc => EncryptCbc(data, keyInfo.Key),
                    _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported")
                };

                _logger?.LogDebug("Encrypted {Size} bytes using {Algorithm} with key {KeyId}",
                    data.Length, algorithm, keyId);

                return Task.FromResult(encryptedData);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Encryption failed for key {KeyId}", keyId);
                throw new CryptographicException("Encryption failed", ex);
            }
        }

        /// <inheritdoc/>
        public Task<byte[]> DecryptAsync(byte[] encryptedData, CancellationToken cancellationToken = default)
        {
            return DecryptAsync(encryptedData, "default", cancellationToken);
        }

        /// <inheritdoc/>
        public Task<byte[]> DecryptAsync(
            byte[] encryptedData,
            string keyId,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(encryptedData, nameof(encryptedData));
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (!_keys.TryGetValue(keyId, out var keyInfo))
            {
                throw new KeyNotFoundException($"Key {keyId} not found");
            }

            try
            {
                var algorithm = keyInfo.Metadata.Algorithm;
                var decryptedData = algorithm switch
                {
                    EncryptionAlgorithm.Aes128Gcm or EncryptionAlgorithm.Aes256Gcm => DecryptGcm(encryptedData, keyInfo.Key),
                    EncryptionAlgorithm.Aes128Cbc or EncryptionAlgorithm.Aes256Cbc => DecryptCbc(encryptedData, keyInfo.Key),
                    _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported")
                };

                _logger?.LogDebug("Decrypted {Size} bytes using {Algorithm} with key {KeyId}",
                    encryptedData.Length, algorithm, keyId);

                return Task.FromResult(decryptedData);
            }
            catch (CryptographicException ex)
            {
                _logger?.LogError(ex, "Decryption failed for key {KeyId}", keyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            string? keyId = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(inputStream, nameof(inputStream));
            Guard.AgainstNull(outputStream, nameof(outputStream));

            keyId ??= "default";
            var keyInfo = GetOrCreateKey(keyId, _options.DefaultAlgorithm);

            // Read all data from input stream
            using var memoryStream = new MemoryStream();
            await inputStream.CopyToAsync(memoryStream, cancellationToken);
            var plainData = memoryStream.ToArray();

            // Encrypt
            var encryptedData = await EncryptAsync(plainData, keyId, cancellationToken);

            // Write to output stream
            await outputStream.WriteAsync(encryptedData, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            string? keyId = null,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(inputStream, nameof(inputStream));
            Guard.AgainstNull(outputStream, nameof(outputStream));

            keyId ??= "default";

            // Read all data from input stream
            using var memoryStream = new MemoryStream();
            await inputStream.CopyToAsync(memoryStream, cancellationToken);
            var encryptedData = memoryStream.ToArray();

            // Decrypt
            var plainData = await DecryptAsync(encryptedData, keyId, cancellationToken);

            // Write to output stream
            await outputStream.WriteAsync(plainData, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<KeyMetadata> GenerateKeyAsync(
            string keyId,
            EncryptionAlgorithm algorithm,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (!SupportedAlgorithms.Contains(algorithm))
            {
                throw new NotSupportedException($"Algorithm {algorithm} is not supported");
            }

            await _keyGenerationLock.WaitAsync(cancellationToken);
            try
            {
                if (_keys.ContainsKey(keyId))
                {
                    throw new InvalidOperationException($"Key {keyId} already exists");
                }

                var keySize = algorithm.GetKeySize();
                var key = GenerateRandomKey(keySize / 8);

                var metadata = new KeyMetadata
                {
                    KeyId = keyId,
                    Algorithm = algorithm,
                    KeySize = keySize,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = _options.KeyExpiration.HasValue
                        ? DateTime.UtcNow.Add(_options.KeyExpiration.Value)
                        : null,
                    Version = 1,
                    IsEnabled = true
                };

                var keyInfo = new KeyInfo
                {
                    Key = key,
                    Metadata = metadata
                };

                _keys.TryAdd(keyId, keyInfo);

                _logger?.LogInformation("Generated new key {KeyId} with algorithm {Algorithm}",
                    keyId, algorithm);

                return metadata;
            }
            finally
            {
                _keyGenerationLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<KeyMetadata> RotateKeyAsync(
            string keyId,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (!_keys.TryGetValue(keyId, out var existingKeyInfo))
            {
                throw new KeyNotFoundException($"Key {keyId} not found");
            }

            await _keyGenerationLock.WaitAsync(cancellationToken);
            try
            {
                var algorithm = existingKeyInfo.Metadata.Algorithm;
                var keySize = algorithm.GetKeySize();
                var newKey = GenerateRandomKey(keySize / 8);

                var newMetadata = new KeyMetadata
                {
                    KeyId = keyId,
                    Algorithm = algorithm,
                    KeySize = keySize,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = _options.KeyExpiration.HasValue
                        ? DateTime.UtcNow.Add(_options.KeyExpiration.Value)
                        : null,
                    Version = existingKeyInfo.Metadata.Version + 1,
                    IsEnabled = true
                };

                var newKeyInfo = new KeyInfo
                {
                    Key = newKey,
                    Metadata = newMetadata,
                    PreviousVersion = existingKeyInfo
                };

                _keys[keyId] = newKeyInfo;

                _logger?.LogInformation("Rotated key {KeyId} to version {Version}",
                    keyId, newMetadata.Version);

                return newMetadata;
            }
            finally
            {
                _keyGenerationLock.Release();
            }
        }

        /// <inheritdoc/>
        public Task<KeyMetadata> GetKeyMetadataAsync(
            string keyId,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (!_keys.TryGetValue(keyId, out var keyInfo))
            {
                throw new KeyNotFoundException($"Key {keyId} not found");
            }

            return Task.FromResult(keyInfo.Metadata);
        }

        /// <inheritdoc/>
        public Task DeleteKeyAsync(string keyId, CancellationToken cancellationToken = default)
        {
            Guard.AgainstNullOrEmpty(keyId, nameof(keyId));

            if (keyId == "default")
            {
                throw new InvalidOperationException("Cannot delete default key");
            }

            if (_keys.TryRemove(keyId, out _))
            {
                _logger?.LogInformation("Deleted key {KeyId}", keyId);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IEnumerable<string>> ListKeysAsync(CancellationToken cancellationToken = default)
        {
            var keyIds = _keys.Keys.ToList();
            return Task.FromResult<IEnumerable<string>>(keyIds);
        }

        private byte[] EncryptGcm(byte[] plainData, byte[] key)
        {
            using var aes = new AesGcm(key, TagSize);
            var nonce = GenerateRandomNonce(NonceSize);
            var ciphertext = new byte[plainData.Length];
            var tag = new byte[TagSize];

            aes.Encrypt(nonce, plainData, ciphertext, tag);

            // Combine: nonce + ciphertext + tag
            var result = new byte[NonceSize + plainData.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

            return result;
        }

        private byte[] DecryptGcm(byte[] encryptedData, byte[] key)
        {
            if (encryptedData.Length < NonceSize + TagSize)
            {
                throw new CryptographicException("Invalid encrypted data format");
            }

            // Extract: nonce + ciphertext + tag
            var nonce = new byte[NonceSize];
            var tag = new byte[TagSize];
            var ciphertext = new byte[encryptedData.Length - NonceSize - TagSize];

            Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
            Buffer.BlockCopy(encryptedData, NonceSize, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(encryptedData, NonceSize + ciphertext.Length, tag, 0, TagSize);

            using var aes = new AesGcm(key, TagSize);
            var plaintext = new byte[ciphertext.Length];

            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }

        private byte[] EncryptCbc(byte[] plainData, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var ciphertext = encryptor.TransformFinalBlock(plainData, 0, plainData.Length);

            // Combine: IV + ciphertext
            var result = new byte[IvSize + ciphertext.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, IvSize);
            Buffer.BlockCopy(ciphertext, 0, result, IvSize, ciphertext.Length);

            return result;
        }

        private byte[] DecryptCbc(byte[] encryptedData, byte[] key)
        {
            if (encryptedData.Length < IvSize)
            {
                throw new CryptographicException("Invalid encrypted data format");
            }

            // Extract: IV + ciphertext
            var iv = new byte[IvSize];
            var ciphertext = new byte[encryptedData.Length - IvSize];

            Buffer.BlockCopy(encryptedData, 0, iv, 0, IvSize);
            Buffer.BlockCopy(encryptedData, IvSize, ciphertext, 0, ciphertext.Length);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);

            return plaintext;
        }

        private KeyInfo GetOrCreateKey(string keyId, EncryptionAlgorithm algorithm)
        {
            if (_keys.TryGetValue(keyId, out var keyInfo))
            {
                return keyInfo;
            }

            // Generate key if it doesn't exist
            var metadata = GenerateKeyAsync(keyId, algorithm).GetAwaiter().GetResult();
            return _keys[keyId];
        }

        private void EnsureDefaultKeyExists()
        {
            if (!_keys.ContainsKey("default"))
            {
                GenerateKeyAsync("default", _options.DefaultAlgorithm).GetAwaiter().GetResult();
            }
        }

        private static byte[] GenerateRandomKey(int size)
        {
            var key = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
        }

        private static byte[] GenerateRandomNonce(int size)
        {
            var nonce = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(nonce);
            return nonce;
        }

        private class KeyInfo
        {
            public byte[] Key { get; set; } = Array.Empty<byte>();
            public KeyMetadata Metadata { get; set; } = new();
            public KeyInfo? PreviousVersion { get; set; }
        }
    }
}
