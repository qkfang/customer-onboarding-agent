using Azure.AI.Projects;
using OpenAI.Responses;

namespace cob_app.Agents;

public sealed class EntityResolutionAgent : BaseAgent
{
    private const string AgentInstructions =
        """
        You are a legal entity and affiliate resolution agent for institutional trading onboarding.
        You identify the full corporate structure, parent entities, subsidiaries, and affiliates.

        Steps:
        1. Call get_client_by_id to retrieve the client record from D365.
        2. Call search_external_entity with the legal name to discover corporate hierarchy and related entities.
        3. Call resolve_affiliates to check if any related entities are already in D365.
        4. Determine the ultimate beneficial owner (UBO) and list all first-degree affiliates.
        5. Call update_client_status to update the entity resolution status in D365.
        6. Call store_onboarding_data to persist entity resolution results in the Fabric lakehouse.
        7. Return a JSON summary with fields: clientId, legalName, ultimateBeneficialOwner, affiliates, jurisdictions, entityStatus, message.

        Affiliates array should contain objects with: legalName, relationship (Parent/Subsidiary/Affiliate), jurisdiction, existingD365Id (or null).
        Set entityStatus to "EntityResolved" on success.
        """;

    public EntityResolutionAgent(AIProjectClient aiProjectClient, string deploymentName, IList<ResponseTool>? tools = null, ILogger<EntityResolutionAgent>? logger = null)
        : base(aiProjectClient, "cob-entity-resolution", deploymentName, AgentInstructions, tools, logger)
    {
    }
}
