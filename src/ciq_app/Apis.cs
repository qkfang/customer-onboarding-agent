using System.Diagnostics;
using cob_app.Agents;
using cob_app.Services;

namespace cob_app;

public static class Apis
{
    private static string SanitizeLog(string? value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    public static void MapAllEndpoints(
        this WebApplication app,
        ClientIntakeAgent clientIntakeAgent,
        KycAmlAgent kycAmlAgent,
        EntityResolutionAgent entityResolutionAgent,
        FinancialReviewAgent financialReviewAgent,
        OnboardingOrchestratorAgent orchestratorAgent,
        D365MockService d365Service,
        ILogger logger)
    {
        // ── D365 Mock Endpoints ──────────────────────────────────────────────────

        app.MapGet("/api/d365/clients", () =>
        {
            var clients = d365Service.ListAll();
            return Results.Json(new { success = true, count = clients.Count, clients });
        });

        app.MapGet("/api/d365/clients/{clientId}", (string clientId) =>
        {
            var record = d365Service.GetById(clientId);
            if (record is null)
                return Results.NotFound(new { success = false, error = $"Client '{clientId}' not found." });
            return Results.Json(new { success = true, client = record });
        });

        // ── Onboarding Agent Endpoints ────────────────────────────────────────────

        app.MapPost("/api/onboarding/intake", async (IntakeRequest request) =>
        {
            logger.LogInformation("/api/onboarding/intake called for clientId={ClientId}", SanitizeLog(request.ClientId));
            if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.LegalName) || string.IsNullOrWhiteSpace(request.EntityType))
                return Results.BadRequest(new { error = "clientId, legalName, and entityType are required." });

            var sw = Stopwatch.StartNew();
            var message = $"Process intake for clientId='{request.ClientId}', legalName='{request.LegalName}', entityType='{request.EntityType}'" +
                          (request.CountryOfIncorporation is not null ? $", countryOfIncorporation='{request.CountryOfIncorporation}'" : "") +
                          (request.RegulatoryId is not null ? $", regulatoryId='{request.RegulatoryId}'" : "");

            var result = await clientIntakeAgent.RunAsync(message);
            sw.Stop();
            logger.LogInformation("/api/onboarding/intake completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Json(new { success = true, agentResult = result });
        });

        app.MapPost("/api/onboarding/kyc", async (ClientIdRequest request) =>
        {
            logger.LogInformation("/api/onboarding/kyc called for clientId={ClientId}", SanitizeLog(request.ClientId));
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return Results.BadRequest(new { error = "clientId is required." });

            var sw = Stopwatch.StartNew();
            var result = await kycAmlAgent.RunAsync($"Run KYC/AML checks for clientId='{request.ClientId}'");
            sw.Stop();
            logger.LogInformation("/api/onboarding/kyc completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Json(new { success = true, agentResult = result });
        });

        app.MapPost("/api/onboarding/entity-resolution", async (ClientIdRequest request) =>
        {
            logger.LogInformation("/api/onboarding/entity-resolution called for clientId={ClientId}", SanitizeLog(request.ClientId));
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return Results.BadRequest(new { error = "clientId is required." });

            var sw = Stopwatch.StartNew();
            var result = await entityResolutionAgent.RunAsync($"Resolve legal entity and affiliates for clientId='{request.ClientId}'");
            sw.Stop();
            logger.LogInformation("/api/onboarding/entity-resolution completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Json(new { success = true, agentResult = result });
        });

        app.MapPost("/api/onboarding/financial-review", async (ClientIdRequest request) =>
        {
            logger.LogInformation("/api/onboarding/financial-review called for clientId={ClientId}", SanitizeLog(request.ClientId));
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return Results.BadRequest(new { error = "clientId is required." });

            var sw = Stopwatch.StartNew();
            var result = await financialReviewAgent.RunAsync($"Perform financial review for clientId='{request.ClientId}'");
            sw.Stop();
            logger.LogInformation("/api/onboarding/financial-review completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Json(new { success = true, agentResult = result });
        });

        app.MapPost("/api/onboarding/orchestrate", async (ClientIdRequest request) =>
        {
            logger.LogInformation("/api/onboarding/orchestrate called for clientId={ClientId}", SanitizeLog(request.ClientId));
            if (string.IsNullOrWhiteSpace(request.ClientId))
                return Results.BadRequest(new { error = "clientId is required." });

            var sw = Stopwatch.StartNew();
            var result = await orchestratorAgent.RunAsync($"Orchestrate the full onboarding workflow for clientId='{request.ClientId}'");
            sw.Stop();
            logger.LogInformation("/api/onboarding/orchestrate completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Results.Json(new { success = true, agentResult = result });
        });
    }
}

public record IntakeRequest(
    string ClientId,
    string LegalName,
    string EntityType,
    string? CountryOfIncorporation = null,
    string? RegulatoryId = null);

public record ClientIdRequest(string ClientId);
