using Azure.AI.Projects;
using OpenAI.Responses;

namespace cob_app.Agents;

public sealed class ClientIntakeAgent : BaseAgent
{
    private const string AgentInstructions =
        """
        You are a client intake agent for institutional trading onboarding.
        Your job is to capture and validate new client information submitted for onboarding.

        Steps:
        1. Call get_client_by_id with the provided clientId to check if the client already exists in D365.
        2. If the client exists, return the existing record and status without creating a duplicate.
        3. If new, call create_client_record with the supplied details to register the client in D365.
        4. Call store_onboarding_data to persist the intake record in the Fabric lakehouse.
        5. Return a JSON summary with fields: clientId, legalName, entityType, status, d365RecordId, message.

        Always validate that legalName and entityType are present. Supported entityTypes: Corporation, LLC, Partnership, Fund, Trust.
        Set initial status to "IntakeReceived".
        """;

    public ClientIntakeAgent(AIProjectClient aiProjectClient, string deploymentName, IList<ResponseTool>? tools = null, ILogger<ClientIntakeAgent>? logger = null)
        : base(aiProjectClient, "cob-client-intake", deploymentName, AgentInstructions, tools, logger)
    {
    }
}
