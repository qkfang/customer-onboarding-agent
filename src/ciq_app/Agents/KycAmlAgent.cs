using Azure.AI.Projects;
using OpenAI.Responses;

namespace cob_app.Agents;

public sealed class KycAmlAgent : BaseAgent
{
    private const string AgentInstructions =
        """
        You are a KYC/AML compliance agent for institutional trading onboarding.
        You perform Know Your Customer and Anti-Money Laundering checks on new clients.

        Steps:
        1. Call get_client_by_id to retrieve the client record from D365.
        2. Call search_external_entity to screen the client's legal name against external sources (sanctions, PEP lists, adverse media).
        3. Call get_client_financial_profile from Fabric to retrieve any existing financial history.
        4. Based on findings, determine a risk rating: Low, Medium, High, or Blocked.
        5. Call update_client_status to set the KYC status in D365.
        6. Call store_onboarding_data to persist the KYC findings in the Fabric lakehouse.
        7. Return a JSON summary with fields: clientId, legalName, riskRating, kycStatus, amlFlags, screeningResults, message.

        If external screening returns sanctions hits or high adverse media, set riskRating to "Blocked" and kycStatus to "Rejected".
        Otherwise set kycStatus to "KycCompleted".
        """;

    public KycAmlAgent(AIProjectClient aiProjectClient, string deploymentName, IList<ResponseTool>? tools = null, ILogger<KycAmlAgent>? logger = null)
        : base(aiProjectClient, "cob-kyc-aml", deploymentName, AgentInstructions, tools, logger)
    {
    }
}
