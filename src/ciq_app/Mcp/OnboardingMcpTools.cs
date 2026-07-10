using System.ComponentModel;
using System.Text.Json;
using cob_app.Services;
using ModelContextProtocol.Server;

namespace cob_app.Mcp;

[McpServerToolType]
public sealed class OnboardingMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly D365MockService _d365;
    private readonly FabricLakehouseService _fabricLakehouseService;
    private readonly BingSearchService _bingSearchService;

    public OnboardingMcpTools(
        D365MockService d365,
        FabricLakehouseService fabricLakehouseService,
        BingSearchService bingSearchService)
    {
        _d365 = d365;
        _fabricLakehouseService = fabricLakehouseService;
        _bingSearchService = bingSearchService;
    }

    // ── D365 Mock Tools ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_client_by_id"), Description("Retrieve a client record from D365 by client ID. Returns the full record or null if not found.")]
    public string GetClientById(
        [Description("The unique client identifier, e.g. CLT-0001")] string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return JsonSerializer.Serialize(new { error = "clientId is required." }, JsonOptions);

        var record = _d365.GetById(clientId);
        if (record is null)
            return JsonSerializer.Serialize(new { found = false, clientId }, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            found = true,
            clientId = record.ClientId,
            legalName = record.LegalName,
            entityType = record.EntityType,
            status = record.Status,
            countryOfIncorporation = record.CountryOfIncorporation,
            regulatoryId = record.RegulatoryId,
            riskRating = record.RiskRating,
            creditRating = record.CreditRating,
            approvedTradingLimit = record.ApprovedTradingLimit,
            createdAt = record.CreatedAt,
            updatedAt = record.UpdatedAt
        }, JsonOptions);
    }

    [McpServerTool(Name = "list_clients"), Description("List all client records in D365.")]
    public string ListClients()
    {
        var clients = _d365.ListAll().Select(r => new
        {
            clientId = r.ClientId,
            legalName = r.LegalName,
            entityType = r.EntityType,
            status = r.Status,
            riskRating = r.RiskRating,
            updatedAt = r.UpdatedAt
        });
        return JsonSerializer.Serialize(clients, JsonOptions);
    }

    [McpServerTool(Name = "create_client_record"), Description("Create a new client record in D365 CRM. Returns the created record.")]
    public string CreateClientRecord(
        [Description("Unique client ID, e.g. CLT-0002")] string clientId,
        [Description("Full legal name of the entity")] string legalName,
        [Description("Entity type: Corporation, LLC, Partnership, Fund, or Trust")] string entityType,
        [Description("Country of incorporation, e.g. United States")] string? countryOfIncorporation = null,
        [Description("Regulatory registration ID, e.g. SEC-12345")] string? regulatoryId = null)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(legalName) || string.IsNullOrWhiteSpace(entityType))
            return JsonSerializer.Serialize(new { error = "clientId, legalName, and entityType are required." }, JsonOptions);

        if (_d365.GetById(clientId) is not null)
            return JsonSerializer.Serialize(new { error = $"Client '{clientId}' already exists." }, JsonOptions);

        var record = _d365.Create(clientId, legalName, entityType, countryOfIncorporation, regulatoryId);
        return JsonSerializer.Serialize(new
        {
            created = true,
            clientId = record.ClientId,
            legalName = record.LegalName,
            entityType = record.EntityType,
            status = record.Status,
            createdAt = record.CreatedAt
        }, JsonOptions);
    }

    [McpServerTool(Name = "update_client_status"), Description("Update the onboarding status and optional compliance fields of a client in D365.")]
    public string UpdateClientStatus(
        [Description("The unique client identifier")] string clientId,
        [Description("New onboarding status, e.g. KycInProgress, KycCompleted, EntityResolved, FinancialReviewCompleted, ReadyToTrade, Rejected")] string status,
        [Description("Risk rating: Low, Medium, High, or Blocked")] string? riskRating = null,
        [Description("Credit rating, e.g. A, BBB, BB")] string? creditRating = null,
        [Description("Approved trading limit in USD")] long? approvedTradingLimit = null)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(status))
            return JsonSerializer.Serialize(new { error = "clientId and status are required." }, JsonOptions);

        var updated = _d365.UpdateStatus(clientId, status, riskRating, creditRating, approvedTradingLimit);
        if (updated is null)
            return JsonSerializer.Serialize(new { error = $"Client '{clientId}' not found." }, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            updated = true,
            clientId = updated.ClientId,
            status = updated.Status,
            riskRating = updated.RiskRating,
            creditRating = updated.CreditRating,
            approvedTradingLimit = updated.ApprovedTradingLimit,
            updatedAt = updated.UpdatedAt
        }, JsonOptions);
    }

    [McpServerTool(Name = "resolve_affiliates"), Description("Check D365 for any existing records related to a list of affiliate legal names.")]
    public string ResolveAffiliates(
        [Description("JSON array of legal entity names to look up in D365")] string affiliateNamesJson)
    {
        List<string>? names;
        try { names = JsonSerializer.Deserialize<List<string>>(affiliateNamesJson); }
        catch { return JsonSerializer.Serialize(new { error = "affiliateNamesJson must be a JSON array of strings." }, JsonOptions); }

        if (names is null || names.Count == 0)
            return JsonSerializer.Serialize(new { results = Array.Empty<object>() }, JsonOptions);

        var allClients = _d365.ListAll();
        var results = names.Select(name =>
        {
            var match = allClients.FirstOrDefault(c => c.LegalName.Contains(name, StringComparison.OrdinalIgnoreCase));
            return new
            {
                searchName = name,
                existingD365Id = match?.ClientId,
                existingStatus = match?.Status
            };
        });

        return JsonSerializer.Serialize(new { results }, JsonOptions);
    }

    // ── Fabric / Work IQ Tools ───────────────────────────────────────────────────

    [McpServerTool(Name = "get_client_financial_profile"), Description("Retrieve the financial profile of a client from the Fabric lakehouse (Work IQ). Returns credit history, NAV, and regulatory flags.")]
    public string GetClientFinancialProfile(
        [Description("The unique client identifier")] string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return JsonSerializer.Serialize(new { error = "clientId is required." }, JsonOptions);

        // Mock financial profile data
        var profiles = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["CLT-0001"] = new
            {
                clientId = "CLT-0001",
                estimatedNav = 2_500_000_000L,
                creditScore = 820,
                creditRating = "A",
                aum = 1_800_000_000L,
                inceptionYear = 2008,
                regulatoryFlags = Array.Empty<string>(),
                tradingHistory = new[] { "Equities", "Fixed Income", "FX" },
                dataSource = "fabric-onelake"
            },
            ["CLT-0002"] = new
            {
                clientId = "CLT-0002",
                estimatedNav = 450_000_000L,
                creditScore = 760,
                creditRating = "BBB",
                aum = 300_000_000L,
                inceptionYear = 2015,
                regulatoryFlags = new[] { "MiFID II reporting pending" },
                tradingHistory = new[] { "Equities", "Derivatives" },
                dataSource = "fabric-onelake"
            }
        };

        if (profiles.TryGetValue(clientId, out var profile))
            return JsonSerializer.Serialize(profile, JsonOptions);

        return JsonSerializer.Serialize(new
        {
            clientId,
            estimatedNav = (long?)null,
            creditScore = (int?)null,
            creditRating = (string?)null,
            regulatoryFlags = Array.Empty<string>(),
            tradingHistory = Array.Empty<string>(),
            dataSource = "fabric-onelake",
            note = "No historical financial data found; new client."
        }, JsonOptions);
    }

    [McpServerTool(Name = "store_onboarding_data"), Description("Persist onboarding stage data to the Fabric lakehouse. Used to record intake, KYC, entity resolution, and financial review results.")]
    public async Task<string> StoreOnboardingData(
        [Description("Client ID this data belongs to")] string clientId,
        [Description("Stage name: intake, kyc, entity-resolution, financial-review, final")] string stage,
        [Description("JSON payload with the stage results to persist")] string payload)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(payload))
            return JsonSerializer.Serialize(new { error = "clientId, stage, and payload are required." }, JsonOptions);

        var timestamp = DateTimeOffset.UtcNow;
        var filename = $"onboarding/{clientId}/{stage}/{timestamp:yyyyMMddHHmmssfff}.json";
        var written = await _fabricLakehouseService.WriteFileAsync(filename, payload);

        return JsonSerializer.Serialize(new
        {
            stored = true,
            fabricPath = filename,
            writtenToOneLake = written,
            timestamp
        }, JsonOptions);
    }

    // ── Web IQ Tools (Bing Search) ───────────────────────────────────────────────

    [McpServerTool(Name = "search_external_entity"), Description("Screen an entity against external web sources using Bing Search (Web IQ). Returns news, regulatory filings, sanctions mentions, and adverse media.")]
    public async Task<string> SearchExternalEntity(
        [Description("Full legal name of the entity to screen")] string legalName,
        [Description("Optional focus area: sanctions, adverse-media, regulatory, corporate-structure")] string? focus = null)
    {
        if (string.IsNullOrWhiteSpace(legalName))
            return JsonSerializer.Serialize(new { error = "legalName is required." }, JsonOptions);

        var focusClause = focus switch
        {
            "sanctions" => " sanctions OFAC SDN list",
            "adverse-media" => " fraud scandal enforcement action",
            "regulatory" => " SEC FINRA FCA regulatory filing",
            "corporate-structure" => " parent company subsidiaries ownership structure",
            _ => " institutional investor regulatory compliance"
        };

        var query = $"\"{legalName}\"{focusClause}";
        var results = await _bingSearchService.SearchAsync(query, count: 5);

        if (!_bingSearchService.IsConfigured || results.Count == 0)
        {
            // Return mock screening results when Bing is not configured
            return JsonSerializer.Serialize(new
            {
                legalName,
                focus,
                source = "mock-screening",
                sanctionsHit = false,
                pepHit = false,
                adverseMediaCount = 0,
                results = new[]
                {
                    new { title = $"{legalName} - Company Overview", snippet = "Institutional investment manager with strong regulatory standing.", url = "https://example.com/overview" },
                    new { title = $"{legalName} Annual Report", snippet = "Audited financials show consistent growth and regulatory compliance.", url = "https://example.com/annual-report" }
                },
                note = "Bing Search not configured; using mock screening data."
            }, JsonOptions);
        }

        var hasSanctionsHit = results.Any(r =>
            r.Title.Contains("sanctions", StringComparison.OrdinalIgnoreCase) ||
            r.Snippet.Contains("OFAC", StringComparison.OrdinalIgnoreCase) ||
            r.Snippet.Contains("SDN", StringComparison.OrdinalIgnoreCase));

        return JsonSerializer.Serialize(new
        {
            legalName,
            focus,
            source = "bing-web-search",
            sanctionsHit = hasSanctionsHit,
            pepHit = false,
            adverseMediaCount = results.Count(r => r.Snippet.Contains("fraud", StringComparison.OrdinalIgnoreCase) || r.Snippet.Contains("scandal", StringComparison.OrdinalIgnoreCase)),
            results = results.Select(r => new { title = r.Title, snippet = r.Snippet, url = r.Url })
        }, JsonOptions);
    }
}
