// <copyright file="DeviceAuthenticator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain;

/// <summary>
/// T17 — Implementación del autenticador de dispositivo.
/// Usa Supabase Auth con sesiones anónimas y persiste tokens con ISecretStore.
/// </summary>
public sealed class DeviceAuthenticator : IDeviceAuthenticator, IDisposable
{
    private readonly ISecretStore secretStore;
    private readonly ITimeProvider timeProvider;
    private readonly HttpClient httpClient;
    private readonly string supabaseUrl;
    private readonly string supabaseKey;
    private readonly string sessionSecretName;

    // Estado en memoria
    private string? currentAccessToken;
    private string? currentRefreshToken;
    private string? currentDeviceId;
    private DateTimeOffset? tokenExpiresAt;
    private DeviceAuthState currentState = DeviceAuthState.Unauthenticated;

    // Lock para thread-safety
    private readonly SemaphoreSlim stateLock = new(1, 1);

    /// <summary>
    /// Nombre del secreto para el token de acceso.
    /// </summary>
    private const string AccessTokenKey = "access_token";

    /// <summary>
    /// Nombre del secreto para el refresh token.
    /// </summary>
    private const string RefreshTokenKey = "refresh_token";

    /// <summary>
    /// Nombre del secreto para el device_id.
    /// </summary>
    private const string DeviceIdKey = "device_id";

    /// <summary>
    /// Nombre del secreto para la fecha de expiración.
    /// </summary>
    private const string ExpiresAtKey = "expires_at";

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthenticator"/> class.
    /// </summary>
    /// <param name="secretStore">Almacén de secretos para persistencia cifrada.</param>
    /// <param name="timeProvider">Proveedor de tiempo.</param>
    /// <param name="httpClient">HTTP client para llamadas a Supabase.</param>
    /// <param name="supabaseUrl">URL de Supabase.</param>
    /// <param name="supabaseKey">Clave API de Supabase (publishable key).</param>
    public DeviceAuthenticator(
        ISecretStore secretStore,
        ITimeProvider timeProvider,
        HttpClient httpClient,
        string supabaseUrl,
        string supabaseKey)
    {
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.supabaseUrl = !string.IsNullOrWhiteSpace(supabaseUrl)
            ? supabaseUrl.TrimEnd('/')
            : throw new ArgumentNullException(nameof(supabaseUrl));
        this.supabaseKey = supabaseKey ?? throw new ArgumentNullException(nameof(supabaseKey));

        // Nombre único para esta instalación
        this.sessionSecretName = $"supabase-session";
    }

    /// <inheritdoc />
    public DeviceAuthState CurrentState
    {
        get
        {
            this.stateLock.Wait();
            try
            {
                return this.currentState;
            }
            finally
            {
                this.stateLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentDeviceId
    {
        get
        {
            this.stateLock.Wait();
            try
            {
                return this.currentDeviceId;
            }
            finally
            {
                this.stateLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentAccessToken
    {
        get
        {
            this.stateLock.Wait();
            try
            {
                return this.currentAccessToken;
            }
            finally
            {
                this.stateLock.Release();
            }
        }
    }

    /// <inheritdoc />
    public async Task<DeviceAuthResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        try
        {
            // Intentar restaurar sesión desde el secret store
            var accessResult = await this.secretStore.ReadAsync($"{this.sessionSecretName}-{AccessTokenKey}", cancellationToken);
            var refreshResult = await this.secretStore.ReadAsync($"{this.sessionSecretName}-{RefreshTokenKey}", cancellationToken);
            var deviceIdResult = await this.secretStore.ReadAsync($"{this.sessionSecretName}-{DeviceIdKey}", cancellationToken);
            var expiresResult = await this.secretStore.ReadAsync($"{this.sessionSecretName}-{ExpiresAtKey}", cancellationToken);

            if (accessResult.Success && !string.IsNullOrEmpty(accessResult.Value) &&
                refreshResult.Success && !string.IsNullOrEmpty(refreshResult.Value) &&
                deviceIdResult.Success && !string.IsNullOrEmpty(deviceIdResult.Value))
            {
                this.currentAccessToken = accessResult.Value;
                this.currentRefreshToken = refreshResult.Value;
                this.currentDeviceId = deviceIdResult.Value;

                if (expiresResult.Success && !string.IsNullOrEmpty(expiresResult.Value) &&
                    DateTimeOffset.TryParse(expiresResult.Value, out var expiresAt))
                {
                    this.tokenExpiresAt = expiresAt;

                    // Verificar si necesita refresh
                    if (expiresAt <= this.timeProvider.WallClockNow)
                    {
                        this.currentState = DeviceAuthState.NeedsRefresh;
                    }
                    else
                    {
                        this.currentState = DeviceAuthState.Authenticated;
                    }
                }
                else
                {
                    this.currentState = DeviceAuthState.NeedsRefresh;
                }

                return DeviceAuthResult.Succeeded(this.currentAccessToken, this.currentDeviceId);
            }

            this.currentState = DeviceAuthState.Unauthenticated;
            return DeviceAuthResult.Failed("No hay sesión previa guardada.", DeviceAuthState.Unauthenticated);
        }
        catch (Exception ex)
        {
            this.currentState = DeviceAuthState.RequiresRePairing;
            return DeviceAuthResult.Failed($"Error inicializando sesión: {ex.Message}");
        }
        finally
        {
            this.stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DeviceAuthResult> CreateAnonymousSessionAsync(CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        try
        {
            // Generar un device_id único
            var newDeviceId = GenerateDeviceId();

            // Llamar a Supabase Auth para crear sesión anónima
            var requestBody = new
            {
                data = new
                {
                    device_id = newDeviceId,
                },
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{this.supabaseUrl}/auth/v1/anonymous")
            {
                Content = JsonContent.Create(requestBody),
            };

            // Headers de Supabase
            request.Headers.Add("apikey", this.supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {this.supabaseKey}");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return DeviceAuthResult.Failed($"Error creando sesión anónima: {response.StatusCode} - {errorContent}");
            }

            var session = await response.Content.ReadFromJsonAsync<SupabaseSession>(cancellationToken: cancellationToken);

            if (session?.AccessToken == null || session?.RefreshToken == null)
            {
                return DeviceAuthResult.Failed("Respuesta inválida del servidor de autenticación.");
            }

            // Guardar en memoria
            this.currentAccessToken = session.AccessToken;
            this.currentRefreshToken = session.RefreshToken;
            this.currentDeviceId = newDeviceId;
            this.tokenExpiresAt = session.ExpiresAt;

            // Extraer device_id del JWT si está presente
            if (TryExtractDeviceIdFromToken(session.AccessToken, out var tokenDeviceId))
            {
                this.currentDeviceId = tokenDeviceId;
            }

            // Persistir en secret store
            await this.SaveSessionAsync(session, newDeviceId, cancellationToken);

            this.currentState = DeviceAuthState.Authenticated;

            return DeviceAuthResult.Succeeded(this.currentAccessToken, this.currentDeviceId);
        }
        catch (HttpRequestException ex)
        {
            this.currentState = DeviceAuthState.RequiresRePairing;
            return DeviceAuthResult.Failed($"Error de red creando sesión: {ex.Message}");
        }
        catch (Exception ex)
        {
            this.currentState = DeviceAuthState.RequiresRePairing;
            return DeviceAuthResult.Failed($"Error creando sesión: {ex.Message}");
        }
        finally
        {
            this.stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DeviceAuthResult> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        string? refreshToken;

        try
        {
            refreshToken = this.currentRefreshToken;

            if (string.IsNullOrEmpty(refreshToken))
            {
                return DeviceAuthResult.Failed("No hay refresh token disponible.");
            }
        }
        finally
        {
            this.stateLock.Release();
        }

        try
        {
            // Llamar a Supabase para refrescar el token
            var requestBody = new
            {
                refresh_token = refreshToken,
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{this.supabaseUrl}/auth/v1/token?grant_type=refresh_token")
            {
                Content = JsonContent.Create(requestBody),
            };

            request.Headers.Add("apikey", this.supabaseKey);
            request.Headers.Add("Authorization", $"Bearer {this.supabaseKey}");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                await this.stateLock.WaitAsync(cancellationToken);
                try
                {
                    // Refresh fallido, necesitamos re-autenticar
                    this.currentState = DeviceAuthState.RequiresRePairing;
                    this.currentAccessToken = null;
                    this.currentRefreshToken = null;
                }
                finally
                {
                    this.stateLock.Release();
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return DeviceAuthResult.Failed($"Error refrescando token: {response.StatusCode} - {errorContent}");
            }

            var session = await response.Content.ReadFromJsonAsync<SupabaseSession>(cancellationToken: cancellationToken);

            if (session?.AccessToken == null || session?.RefreshToken == null)
            {
                await this.stateLock.WaitAsync(cancellationToken);
                try
                {
                    this.currentState = DeviceAuthState.RequiresRePairing;
                }
                finally
                {
                    this.stateLock.Release();
                }

                return DeviceAuthResult.Failed("Respuesta inválida al refrescar token.");
            }

            await this.stateLock.WaitAsync(cancellationToken);
            try
            {
                this.currentAccessToken = session.AccessToken;
                this.currentRefreshToken = session.RefreshToken;
                this.tokenExpiresAt = session.ExpiresAt;
                this.currentState = DeviceAuthState.Authenticated;

                // Extraer device_id del JWT si está presente
                if (TryExtractDeviceIdFromToken(session.AccessToken, out var tokenDeviceId))
                {
                    this.currentDeviceId = tokenDeviceId;
                }

                // Persistir sesión actualizada
                await this.SaveSessionAsync(session, this.currentDeviceId, cancellationToken);
            }
            finally
            {
                this.stateLock.Release();
            }

            return DeviceAuthResult.Succeeded(this.currentAccessToken, this.currentDeviceId!);
        }
        catch (HttpRequestException ex)
        {
            await this.stateLock.WaitAsync(cancellationToken);
            try
            {
                this.currentState = DeviceAuthState.NeedsRefresh;
            }
            finally
            {
                this.stateLock.Release();
            }

            return DeviceAuthResult.Failed($"Error de red refrescando token: {ex.Message}", DeviceAuthState.NeedsRefresh);
        }
        catch (Exception ex)
        {
            await this.stateLock.WaitAsync(cancellationToken);
            try
            {
                this.currentState = DeviceAuthState.NeedsRefresh;
            }
            finally
            {
                this.stateLock.Release();
            }

            return DeviceAuthResult.Failed($"Error refrescando token: {ex.Message}", DeviceAuthState.NeedsRefresh);
        }
    }

    /// <inheritdoc />
    public async Task<DeviceAuthResult> RefreshIfNeededAsync(
        TimeSpan refreshThreshold,
        CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        var state = this.currentState;
        var expiresAt = this.tokenExpiresAt;

        try
        {
            if (state != DeviceAuthState.Authenticated && state != DeviceAuthState.NeedsRefresh)
            {
                return DeviceAuthResult.Failed("No hay sesión activa para refrescar.", state);
            }

            if (expiresAt.HasValue)
            {
                var timeUntilExpiry = expiresAt.Value - this.timeProvider.WallClockNow;
                if (timeUntilExpiry > refreshThreshold)
                {
                    // No necesita refresh aún
                    return DeviceAuthResult.Succeeded(this.currentAccessToken!, this.currentDeviceId!);
                }
            }
        }
        finally
        {
            this.stateLock.Release();
        }

        // Necesita refresh
        return await this.RefreshTokenAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateSessionAsync(string reason, CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        try
        {
            // Limpiar secretos
            await this.secretStore.DeleteAsync($"{this.sessionSecretName}-{AccessTokenKey}", cancellationToken);
            await this.secretStore.DeleteAsync($"{this.sessionSecretName}-{RefreshTokenKey}", cancellationToken);
            await this.secretStore.DeleteAsync($"{this.sessionSecretName}-{DeviceIdKey}", cancellationToken);
            await this.secretStore.DeleteAsync($"{this.sessionSecretName}-{ExpiresAtKey}", cancellationToken);

            // Limpiar memoria
            this.currentAccessToken = null;
            this.currentRefreshToken = null;
            this.currentDeviceId = null;
            this.tokenExpiresAt = null;
            this.currentState = DeviceAuthState.RequiresRePairing;
        }
        finally
        {
            this.stateLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateSessionAsync(CancellationToken cancellationToken = default)
    {
        await this.stateLock.WaitAsync(cancellationToken);
        var token = this.currentAccessToken;
        var state = this.currentState;

        try
        {
            if (string.IsNullOrEmpty(token) || state == DeviceAuthState.Unauthenticated || state == DeviceAuthState.RequiresRePairing)
            {
                return false;
            }

            // Verificar expiración
            if (this.tokenExpiresAt.HasValue && this.tokenExpiresAt.Value <= this.timeProvider.WallClockNow)
            {
                return false;
            }

            // Verificar que el JWT es válido (firma, estructura)
            if (!TryValidateJwtStructure(token))
            {
                return false;
            }
        }
        finally
        {
            this.stateLock.Release();
        }

        return true;
    }

    /// <inheritdoc />
    public async Task<DeviceAuthResult> RotateSessionAsync(CancellationToken cancellationToken = default)
    {
        // Invalidar sesión actual
        await this.InvalidateSessionAsync("Rotación de sesión solicitada.", cancellationToken);

        // Crear nueva sesión
        return await this.CreateAnonymousSessionAsync(cancellationToken);
    }

    /// <summary>
    /// Persiste la sesión en el secret store.
    /// </summary>
    private async Task SaveSessionAsync(SupabaseSession session, string deviceId, CancellationToken cancellationToken)
    {
        await this.secretStore.WriteAsync($"{this.sessionSecretName}-{AccessTokenKey}", session.AccessToken, cancellationToken);
        await this.secretStore.WriteAsync($"{this.sessionSecretName}-{RefreshTokenKey}", session.RefreshToken, cancellationToken);
        await this.secretStore.WriteAsync($"{this.sessionSecretName}-{DeviceIdKey}", deviceId, cancellationToken);

        if (session.ExpiresAt.HasValue)
        {
            await this.secretStore.WriteAsync(
                $"{this.sessionSecretName}-{ExpiresAtKey}",
                session.ExpiresAt.Value.ToString("O"),
                cancellationToken);
        }
    }

    /// <summary>
    /// Genera un device_id único.
    /// </summary>
    private static string GenerateDeviceId()
    {
        // Usar GUID + timestamp + hash de máquina
        var machineInfo = $"{Environment.MachineName}-{Environment.UserDomainName}-{Environment.UserName}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(machineInfo));
        var hashStr = Convert.ToHexString(hash)[..16];
        return $"device_{hashStr}_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Intenta extraer el device_id del JWT.
    /// </summary>
    private static bool TryExtractDeviceIdFromToken(string token, out string? deviceId)
    {
        deviceId = null;

        try
        {
            // Decodificar JWT sin verificar firma (solo para extraer claims)
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            var payload = parts[1];
            // Add padding if needed
            var remainder = payload.Length % 4;
            var padded = remainder switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload,
            };

            var jsonBytes = Convert.FromBase64String(padded);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Intentar diferentes claim names para device_id
            if (root.TryGetProperty("device_id", out var deviceIdElement))
            {
                deviceId = deviceIdElement.GetString();
                return !string.IsNullOrEmpty(deviceId);
            }

            if (root.TryGetProperty("sub", out var subElement))
            {
                deviceId = subElement.GetString();
                return !string.IsNullOrEmpty(deviceId);
            }
        }
        catch
        {
            // Ignorar errores de parsing
        }

        return false;
    }

    /// <summary>
    /// Valida la estructura del JWT.
    /// </summary>
    private static bool TryValidateJwtStructure(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            // Verificar que cada parte es base64 válida
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    return false;
                }

                // Check base64 charset (allow = for padding)
                var chars = part.ToCharArray();
                foreach (var c in chars)
                {
                    if (!char.IsLetterOrDigit(c) && c != '+' && c != '/' && c != '-' && c != '_' && c != '=')
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.stateLock.Dispose();
    }

    /// <summary>
    /// Respuesta de sesión de Supabase.
    /// </summary>
    private class SupabaseSession
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTimeOffset? ExpiresAt { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("user")]
        public SupabaseUser? User { get; set; }
    }

    /// <summary>
    /// Usuario de Supabase.
    /// </summary>
    private class SupabaseUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("app_metadata")]
        public SupabaseAppMetadata? AppMetadata { get; set; }
    }

    /// <summary>
    /// App metadata de Supabase.
    /// </summary>
    private class SupabaseAppMetadata
    {
        [JsonPropertyName("provider")]
        public string? Provider { get; set; }

        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }
    }
}
