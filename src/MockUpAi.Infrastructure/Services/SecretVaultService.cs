using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MockUpAi.Core.Application.Abstractions;
using MockUpAi.Core.Common;

namespace MockUpAi.Infrastructure.Services;

internal sealed class SecretVaultService : ISecretVaultService
{
    private readonly string _vaultPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _lastSource = "unset";

    public SecretVaultService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockUpAi");
        Directory.CreateDirectory(folder);
        _vaultPath = Path.Combine(folder, "vault.json");
    }

    public async Task<string?> GetOpenAiApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            _lastSource = "environment";
            return envKey.Trim();
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_vaultPath))
            {
                _lastSource = "unset";
                return null;
            }

            var json = await File.ReadAllTextAsync(_vaultPath, cancellationToken);
            var envelope = JsonSerializer.Deserialize<VaultEnvelope>(json);
            if (envelope?.OpenAiApiKey is null)
            {
                _lastSource = "unset";
                return null;
            }

            var decrypted = Decrypt(envelope.OpenAiApiKey);
            _lastSource = "encrypted_file";
            return decrypted;
        }
        catch
        {
            _lastSource = "error";
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> SaveOpenAiApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length < 20)
        {
            return OperationResult.Failure("Invalid API key format.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            VaultEnvelope envelope;
            if (File.Exists(_vaultPath))
            {
                var existing = await File.ReadAllTextAsync(_vaultPath, cancellationToken);
                envelope = JsonSerializer.Deserialize<VaultEnvelope>(existing) ?? new VaultEnvelope();
            }
            else
            {
                envelope = new VaultEnvelope();
            }

            envelope.OpenAiApiKey = Encrypt(apiKey.Trim());
            envelope.UpdatedAtUtc = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_vaultPath, json, cancellationToken);
            _lastSource = "encrypted_file";

            return OperationResult.Success("API key saved securely.");
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Failed to save API key: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> HasOpenAiApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var key = await GetOpenAiApiKeyAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(key);
    }

    public string GetKeySource() => _lastSource;

    private static string Encrypt(string plainText)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var key = DeriveKey(salt);

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintextBytes, cipher, tag);

        var blob = new EncryptedValue
        {
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            CipherText = Convert.ToBase64String(cipher),
            Tag = Convert.ToBase64String(tag),
        };

        return JsonSerializer.Serialize(blob);
    }

    private static string Decrypt(string encrypted)
    {
        var blob = JsonSerializer.Deserialize<EncryptedValue>(encrypted)
            ?? throw new InvalidOperationException("Invalid vault payload.");

        var salt = Convert.FromBase64String(blob.Salt);
        var nonce = Convert.FromBase64String(blob.Nonce);
        var cipher = Convert.FromBase64String(blob.CipherText);
        var tag = Convert.FromBase64String(blob.Tag);
        var key = DeriveKey(salt);

        var plaintext = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] DeriveKey(byte[] salt)
    {
        var master = Environment.GetEnvironmentVariable("MOCKUPAI_MASTER_SECRET");
        var material = $"MockUpAi|{Environment.UserName}|{Environment.MachineName}|{master}";
        return Rfc2898DeriveBytes.Pbkdf2(material, salt, 100_000, HashAlgorithmName.SHA256, 32);
    }

    private sealed class VaultEnvelope
    {
        public string? OpenAiApiKey { get; set; }

        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class EncryptedValue
    {
        public string Salt { get; set; } = string.Empty;

        public string Nonce { get; set; } = string.Empty;

        public string CipherText { get; set; } = string.Empty;

        public string Tag { get; set; } = string.Empty;
    }
}
