"""Client intelligence agent - runs against the same Microsoft Foundry
project as src/ciq_app, grounded in Foundry IQ, Work IQ, Fabric IQ and Web IQ.

Foundry IQ, Fabric IQ and Web IQ are wired here via the Azure AI Agents SDK.
Work IQ (Microsoft 365) has no SDK support and must be added to the agent
from the Foundry portal - see scripts/setup_foundry_agent.py for details.
"""

import os
from pathlib import Path

from azure.ai.agents.models import BingGroundingTool, FabricTool, FileSearchTool
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential

from .instructions import INSTRUCTIONS

try:
    from dotenv import load_dotenv

    load_dotenv(dotenv_path=Path(__file__).resolve().parent.parent / ".env")
except ImportError:
    pass

AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "ciq-client-intelligence-agent")
PROJECT_ENDPOINT = os.environ.get("AZURE_AI_PROJECT_ENDPOINT", "")
MODEL_DEPLOYMENT_NAME = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-5.4")
TENANT_ID = os.environ.get("AZURE_TENANT_ID", "")
VECTOR_STORE_ID = os.environ.get("FOUNDRY_KNOWLEDGE_VECTOR_STORE_ID", "")
FABRIC_CONNECTION_ID = os.environ.get("FABRIC_CONNECTION_ID", "")
BING_CONNECTION_NAME = os.environ.get("BING_CONNECTION_NAME", "")


def get_project_client() -> AIProjectClient:
    if not PROJECT_ENDPOINT:
        raise EnvironmentError(
            "AZURE_AI_PROJECT_ENDPOINT is not set. Copy .env.template to .env "
            "and point it at the same Foundry project used by src/ciq_app."
        )
    credential = DefaultAzureCredential(
        **({"tenant_id": TENANT_ID} if TENANT_ID else {})
    )
    return AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=credential)


def build_tools(client: AIProjectClient) -> tuple[list, dict]:
    """Assemble the Foundry IQ, Fabric IQ and Web IQ tool definitions."""
    tools = []

    if VECTOR_STORE_ID:
        tools.append(FileSearchTool(vector_store_ids=[VECTOR_STORE_ID]))

    if FABRIC_CONNECTION_ID:
        tools.append(FabricTool(connection_id=FABRIC_CONNECTION_ID))

    if BING_CONNECTION_NAME:
        connection = client.connections.get(connection_name=BING_CONNECTION_NAME)
        tools.append(BingGroundingTool(connection_id=connection.id))

    from azure.ai.agents.models import get_tool_definitions, get_tool_resources

    return (
        get_tool_definitions(tools) if tools else [],
        get_tool_resources(tools) if tools else {},
    )


def get_or_create_agent(client: AIProjectClient):
    for agent in client.agents.list_agents():
        if agent.name == AGENT_NAME:
            return agent

    tool_definitions, tool_resources = build_tools(client)
    create_kwargs = dict(
        model=MODEL_DEPLOYMENT_NAME,
        name=AGENT_NAME,
        instructions=INSTRUCTIONS,
        tools=tool_definitions,
    )
    if tool_resources:
        create_kwargs["tool_resources"] = tool_resources
    return client.agents.create_agent(**create_kwargs)


def ask(message: str) -> str:
    """Send a single message to the client intelligence agent and return the reply."""
    client = get_project_client()
    agent = get_or_create_agent(client)

    thread = client.agents.threads.create()
    client.agents.messages.create(thread_id=thread.id, role="user", content=message)
    run = client.agents.runs.create_and_process(thread_id=thread.id, agent_id=agent.id)

    if run.status == "failed":
        raise RuntimeError(f"Agent run failed: {run.last_error}")

    # messages.list returns newest first, so the first assistant message is the reply.
    for msg in client.agents.messages.list(thread_id=thread.id):
        if msg.role == "assistant" and msg.text_messages:
            return msg.text_messages[-1].text.value
    return ""


if __name__ == "__main__":
    import sys

    question = " ".join(sys.argv[1:]) or "Summarize what you know about client CID-1001."
    print(ask(question))
