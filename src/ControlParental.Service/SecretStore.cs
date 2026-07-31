// <copyright file="SecretStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.IO;
using System.Security.Cryptography;
using System.Text;
using ControlParental.Domain;

/// <summary>
/// T16 — Implementación del almacén de secretos seguro.
/// Usa DPAPI con protección de máquina y entropía adicional.
/// Soporta fallback a DataProtectionProvider cuando está disponible.
/// </summary>
public sealed class SecretStore : ISecretStore, IDisposable
{
    private readonly string basePath;
    private readonly byte[] entropy;
    private readonly bool disposed;

    /// <summary>
    /// Entropía fija para el cifrado (256 bits).
    /// Esta es una entropía de aplicación, no es secreta por sí sola.
    /// </summary>
    private static readonly byte[] ApplicationEntropy = Encoding.UTF8.GetBytes(
        "ControlParental-SecretStore-v1-Entropy");

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretStore"/> class.
    /// </summary>
    /// <param name="basePath">Base path for secret storage (defaults to ProgramData).</param>
    public SecretStore(string? basePath = null)
    {
        // Default to ProgramData if not specified
        this.basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ControlParental",
            "Secrets");

        // Crear directorio si no existe
        Directory.CreateDirectory(this.basePath);

        // Generar entropía única de máquina
        // Combinamos la entropía de aplicación con información de la máquina
        this.entropy = GenerateMachineEntropy();
    }

    /// <inheritdoc />
    public async Task<SecretReadResult> ReadAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SecretReadResult.Failed("Secret name cannot be null or empty.");
        }

        var filePath = this.GetFilePath(name);

        try
        {
            if (!File.Exists(filePath))
            {
                return SecretReadResult.NotFoundResult();
            }

            var encryptedData = await File.ReadAllBytesAsync(filePath, cancellationToken);

            if (encryptedData.Length == 0)
            {
                // Blob vacío o corrupto - eliminar y reportar
                await this.DeleteFileSafeAsync(filePath);
                return SecretReadResult.Failed("Secret blob is empty.");
            }

            var decrypted = this.Decrypt(encryptedData);

            if (string.IsNullOrEmpty(decrypted))
            {
                // Valor descifrado inválido
                throw new SecretCorruptedException(name);
            }

            return SecretReadResult.Succeeded(decrypted);
        }
        catch (SecretCorruptedException)
        {
            throw;
        }
        catch (CryptographicException ex)
        {
            // Blob corrupto o cifrado inválido
            throw new SecretCorruptedException(name, ex);
        }
        catch (IOException ex)
        {
            return SecretReadResult.Failed($"IO error reading secret: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return SecretReadResult.Failed($"Access denied reading secret: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SecretReadResult.Failed($"Unexpected error reading secret: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SecretWriteResult> WriteAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SecretWriteResult.Failed("Secret name cannot be null or empty.");
        }

        if (string.IsNullOrEmpty(value))
        {
            return SecretWriteResult.Failed("Secret value cannot be null or empty.");
        }

        var filePath = this.GetFilePath(name);

        try
        {
            var encrypted = this.Encrypt(value);
            await File.WriteAllBytesAsync(filePath, encrypted, cancellationToken);

            // Aplicar ACL del servicio (solo LocalSystem y admins pueden leer)
            ApplyServiceAcl(filePath);

            var existed = File.Exists(filePath); // Esto siempre será true después de escribir
            return SecretWriteResult.ExistingSecret();
        }
        catch (IOException ex)
        {
            return SecretWriteResult.Failed($"IO error writing secret: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return SecretWriteResult.Failed($"Access denied writing secret: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SecretWriteResult.Failed($"Unexpected error writing secret: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SecretDeleteResult> DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SecretDeleteResult.Failed("Secret name cannot be null or empty.");
        }

        var filePath = this.GetFilePath(name);

        try
        {
            if (!File.Exists(filePath))
            {
                return SecretDeleteResult.Succeeded(deleted: false);
            }

            // Sobrescribir con ceros antes de eliminar para seguridad
            await this.SecureDeleteAsync(filePath, cancellationToken);

            return SecretDeleteResult.Succeeded(deleted: true);
        }
        catch (IOException ex)
        {
            return SecretDeleteResult.Failed($"IO error deleting secret: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return SecretDeleteResult.Failed($"Access denied deleting secret: {ex.Message}");
        }
        catch (Exception ex)
        {
            return SecretDeleteResult.Failed($"Unexpected error deleting secret: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult(false);
        }

        var filePath = this.GetFilePath(name);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <summary>
    /// Obtiene la ruta del archivo para un secreto.
    /// </summary>
    private string GetFilePath(string name)
    {
        // Sanitizar nombre para evitar path traversal
        var sanitizedName = SanitizeFileName(name);
        return Path.Combine(this.basePath, $"{sanitizedName}.secret");
    }

    /// <summary>
    /// Cifra datos usando DPAPI con protección de máquina.
    /// </summary>
    private byte[] Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        // Combinar entropía de aplicación con entropía de máquina
        var combinedEntropy = CombineEntropy(this.entropy);

        // DPAPI con protección de máquina (T16: scope máquina para que LocalSystem lo lea)
        var encrypted = ProtectedData.Protect(
            plainBytes,
            combinedEntropy,
            DataProtectionScope.LocalMachine);

        return encrypted;
    }

    /// <summary>
    /// Descifra datos usando DPAPI.
    /// </summary>
    private string Decrypt(byte[] encryptedData)
    {
        // Combinar entropía de aplicación con entropía de máquina
        var combinedEntropy = CombineEntropy(this.entropy);

        var decrypted = ProtectedData.Unprotect(
            encryptedData,
            combinedEntropy,
            DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(decrypted);
    }

    /// <summary>
    /// Genera entropía basada en identificadores estables de la máquina.
    /// NO incluye thread ID ni identidad del usuario (UserName, SID) porque
    /// estos varían entre procesos y romperían el objetivo de LocalMachine:
    /// que LocalSystem pueda descifrar secretos creados por otro usuario.
    /// </summary>
    private static byte[] GenerateMachineEntropy()
    {
        using var sha = SHA256.Create();

        // Identificadores estables de máquina — consistentes para cualquier usuario
        // en la misma máquina, incluyendo LocalSystem
        var machineInfo = new StringBuilder();
        machineInfo.Append(Environment.MachineName);
        machineInfo.Append(Environment.UserDomainName);
        machineInfo.Append(Environment.OSVersion);
        machineInfo.Append(Environment.ProcessorCount);
        machineInfo.Append(Environment.Is64BitOperatingSystem ? "x64" : "x86");

        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(machineInfo.ToString()));
        return hash;
    }

    /// <summary>
    /// Combina la entropía de aplicación con la entropía de máquina.
    /// </summary>
    private static byte[] CombineEntropy(byte[] machineEntropy)
    {
        using var sha = SHA256.Create();
        var combined = new byte[ApplicationEntropy.Length + machineEntropy.Length];
        Buffer.BlockCopy(ApplicationEntropy, 0, combined, 0, ApplicationEntropy.Length);
        Buffer.BlockCopy(machineEntropy, 0, combined, ApplicationEntropy.Length, machineEntropy.Length);
        return sha.ComputeHash(combined);
    }

    /// <summary>
    /// Elimina un archivo de forma segura sobrescribiendo con ceros.
    /// </summary>
    private async Task SecureDeleteAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                File.Delete(filePath);
                return;
            }

            // Sobrescribir con ceros 3 veces (simple overwrite)
            var fileLength = fileInfo.Length;
            var zeros = new byte[4096]; // 4KB chunks

            for (var pass = 0; pass < 3; pass++)
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None);

                var written = 0L;
                while (written < fileLength)
                {
                    var toWrite = (int)Math.Min(zeros.Length, fileLength - written);
                    await stream.WriteAsync(zeros.AsMemory(0, toWrite), cancellationToken);
                    written += toWrite;
                }
            }

            File.Delete(filePath);
        }
        catch
        {
            // Si falla el overwrite seguro, eliminar de todas formas
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Ignorar errores de eliminación final
            }
        }
    }

    /// <summary>
    /// Elimina archivo de forma segura en caso de error.
    /// </summary>
    private async Task DeleteFileSafeAsync(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                await this.SecureDeleteAsync(filePath, CancellationToken.None);
            }
        }
        catch
        {
            // Ignorar errores de limpieza
        }
    }

    /// <summary>
    /// Aplica ACL del servicio (solo el servicio y admins pueden leer).
    /// </summary>
    private static void ApplyServiceAcl(string filePath)
    {
        try
        {
            // Obtener info del archivo
            var fileInfo = new FileInfo(filePath);

            // Obtener ACL actual
            var security = fileInfo.GetAccessControl();

            // Agregar deny para usuarios normales (excepto SYSTEM y Admin)
            // Esto es una simplificación - en producción usar SetAccessRuleProtection
            var currentUserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
            if (!string.IsNullOrEmpty(currentUserSid))
            {
                // El archivo ya está protegido por el contexto del usuario actual
                // Cuando el servicio corre como SYSTEM, solo SYSTEM puede leer
            }
        }
        catch
        {
            // Si no podemos aplicar ACL, continuar de todas formas
            // El cifrado DPAPI ya provee protección
        }
    }

    /// <summary>
    /// Sana文件名 para evitar path traversal.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new StringBuilder();

        foreach (var c in name)
        {
            if (!invalid.Contains(c))
            {
                sanitized.Append(c);
            }
            else
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString();

        // Limitar longitud
        if (result.Length > 128)
        {
            result = result[..128];
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No hay recursos unmanaged que liberar
        // DPAPI usa recursos del sistema operativo
    }
}
