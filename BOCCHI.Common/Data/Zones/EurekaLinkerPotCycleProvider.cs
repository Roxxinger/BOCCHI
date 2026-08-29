using BOCCHI.Common.Data.Fates;
using BOCCHI.Common.Data.Zones;
using BOCCHI.Common.Services;
using Ocelot.Lifecycle;
using Ocelot.Services.Logger;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BOCCHI.Common.Data.Zones;
/// <summary>
/// External provider for pot cycle data (Eureka Linker API, XIVAPI, etc.).
/// Used as a fallback when local/remote anchors are not available.
/// </summary>
public interface IPotCycleExternalProvider
{
    /// <summary>
    /// Try to get pot cycle data for a territory from external source.
    /// Returns null if not available or on error.
    /// </summary>
    Task<PotCycleSnapshot?> TryGetCycleAsync(
        ushort territoryTypeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Eureka Linker API provider for pot cycles.
/// Assumes an API endpoint like: https://api.eurekalinker.com/v1/pot-cycles?territory={id}&dc={dc}
/// Response format:
/// {
///   "territoryId": 1252,
///   "currentPotFateId": 1234,
///   "currentSpawnUnix": 1234567890,
///   "nextPotFateId": 5678,
///   "nextSpawnUnix": 1234567890,
///   "dataCenter": "Primal"
/// }
/// </summary>
public sealed class EurekaLinkerPotCycleProvider : IPotCycleExternalProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ILogger<EurekaLinkerPotCycleProvider> logger;
    private readonly string baseUrl;
    private readonly string? apiKey;

    public EurekaLinkerPotCycleProvider(
        ILogger<EurekaLinkerPotCycleProvider> logger,
        string baseUrl = "https://api.eurekalinker.com/v1",
        string? apiKey = null)
    {
        this.logger = logger;
        this.baseUrl = baseUrl.TrimEnd('/');
        this.apiKey = apiKey;
    }

    public async Task<PotCycleSnapshot?> TryGetCycleAsync(
        ushort territoryTypeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string url = $"{baseUrl}/pot-cycles?territory={territoryTypeId}";
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("Authorization", $"Bearer {apiKey}");
            }

            using HttpResponseMessage response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.Debug($"[EurekaLinker] HTTP {response.StatusCode} for territory {territoryTypeId}");
                return null;
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            EurekaLinkerResponse? data = JsonSerializer.Deserialize<EurekaLinkerResponse>(body, JsonOptions);
            if (data == null || data.CurrentPotFateId == 0 || data.CurrentSpawnUnix <= 0)
            {
                logger.Debug($"[EurekaLinker] No valid cycle data for territory {territoryTypeId}");
                return null;
            }

            DateTimeOffset currentSpawn = DateTimeOffset.FromUnixTimeSeconds(data.CurrentSpawnUnix);
            DateTimeOffset nextSpawn = data.NextSpawnUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(data.NextSpawnUnix)
                : currentSpawn.Add(TimeSpan.FromMinutes(30));

            return new PotCycleSnapshot
            {
                TerritoryTypeId = territoryTypeId,
                HasKnownAnchor = true,
                AnchorPotFateId = data.CurrentPotFateId,
                AnchorSpawnAt = currentSpawn,
                IsRemoteAnchor = true, // external source = treat as remote
                CurrentActivePotFateId = data.CurrentPotFateId,
                PredictedNextPotFateId = data.NextPotFateId,
                PredictedNextSpawnAt = nextSpawn,
            };
        }
        catch (Exception ex)
        {
            logger.Debug($"[EurekaLinker] Error fetching cycle for territory {territoryTypeId}: {ex.Message}");
            return null;
        }
    }

    private sealed class EurekaLinkerResponse
    {
        [JsonPropertyName("territoryId")]
        public int TerritoryId { get; set; }

        [JsonPropertyName("currentPotFateId")]
        public int CurrentPotFateId { get; set; }

        [JsonPropertyName("currentSpawnUnix")]
        public long CurrentSpawnUnix { get; set; }

        [JsonPropertyName("nextPotFateId")]
        public int NextPotFateId { get; set; }

        [JsonPropertyName("nextSpawnUnix")]
        public long NextSpawnUnix { get; set; }

        [JsonPropertyName("dataCenter")]
        public string? DataCenter { get; set; }
    }
}