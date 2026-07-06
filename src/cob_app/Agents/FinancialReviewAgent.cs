using Azure.AI.Projects;
using OpenAI.Responses;

namespace cob_app.Agents;

public sealed class FinancialReviewAgent : BaseAgent
{
    private const string AgentInstructions =
        """
        You are a financial review agent for institutional trading onboarding.
        You assess the financial standing, credit worthiness, and trading capacity of new clients.

        Steps:
        1. Call get_client_by_id to retrieve the client record from D365.
        2. Call get_client_financial_profile from Fabric to get historical financial data and credit scores.
        3. Call search_external_entity to gather publicly available financial information and ratings.
        4. Assess the client's net asset value (NAV), credit rating, trading history, and regulatory standing.
        5. Determine an approved trading limit in USD.
        6. Call update_client_status to record the financial review outcome in D365.
        7. Call store_onboarding_data to persist the financial review in the Fabric lakehouse.
        8. Return a JSON summary with fields: clientId, legalName, creditRating, estimatedNav, approvedTradingLimit, regulatoryFlags, financialStatus, message.

        Set financialStatus to "FinancialReviewCompleted" on success.
        If severe regulatory flags are found, set financialStatus to "FinancialReviewFailed".
        """;

    public FinancialReviewAgent(AIProjectClient aiProjectClient, string deploymentName, IList<ResponseTool>? tools = null, ILogger<FinancialReviewAgent>? logger = null)
        : base(aiProjectClient, "cob-financial-review", deploymentName, AgentInstructions, tools, logger)
    {
    }
}
