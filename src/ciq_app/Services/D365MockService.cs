using System.Text.Json;

namespace cob_app.Services;

public record ClientRecord(
    string ClientId,
    string LegalName,
    string EntityType,
    string Status,
    string? CountryOfIncorporation,
    string? RegulatoryId,
    string? RiskRating,
    string? CreditRating,
    long? ApprovedTradingLimit,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class D365MockService
{
    private readonly Dictionary<string, ClientRecord> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<D365MockService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public D365MockService(ILogger<D365MockService> logger)
    {
        _logger = logger;
        SeedData();
    }

    private void SeedData()
    {
        var now = DateTimeOffset.UtcNow;
        var seed = new ClientRecord(
            "CLT-0001",
            "Apex Capital Management Ltd",
            "Fund",
            "ReadyToTrade",
            "United Kingdom",
            "FCA-998877",
            "Low",
            "A",
            50_000_000L,
            now.AddDays(-30),
            now.AddDays(-5));
        _store[seed.ClientId] = seed;
    }

    public ClientRecord? GetById(string clientId)
    {
        _store.TryGetValue(clientId, out var record);
        _logger.LogDebug("D365 GetById {ClientId}: {Found}", clientId, record is not null ? "found" : "not found");
        return record;
    }

    public IReadOnlyList<ClientRecord> ListAll() =>
        _store.Values.OrderBy(c => c.CreatedAt).ToList();

    public ClientRecord Create(
        string clientId,
        string legalName,
        string entityType,
        string? countryOfIncorporation = null,
        string? regulatoryId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new ClientRecord(
            clientId,
            legalName,
            entityType,
            "IntakeReceived",
            countryOfIncorporation,
            regulatoryId,
            null,
            null,
            null,
            now,
            now);

        _store[clientId] = record;
        _logger.LogInformation("D365 Create client {ClientId} '{LegalName}'", Sanitize(clientId), Sanitize(legalName));
        return record;
    }

    public ClientRecord? UpdateStatus(
        string clientId,
        string status,
        string? riskRating = null,
        string? creditRating = null,
        long? approvedTradingLimit = null)
    {
        if (!_store.TryGetValue(clientId, out var existing))
        {
            _logger.LogWarning("D365 UpdateStatus: client {ClientId} not found", clientId);
            return null;
        }

        var updated = existing with
        {
            Status = status,
            RiskRating = riskRating ?? existing.RiskRating,
            CreditRating = creditRating ?? existing.CreditRating,
            ApprovedTradingLimit = approvedTradingLimit ?? existing.ApprovedTradingLimit,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _store[clientId] = updated;
        _logger.LogInformation("D365 UpdateStatus {ClientId} -> {Status}", clientId, status);
        return updated;
    }

    public string Serialize(object obj) => JsonSerializer.Serialize(obj, JsonOptions);

    private static string Sanitize(string? value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
}
