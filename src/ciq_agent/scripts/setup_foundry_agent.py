#!/usr/bin/env python3
"""Create/manage the client intelligence agent in the Microsoft Foundry project.

Automates setup of the SDK-supported IQ tools:
  - Foundry IQ (FileSearchTool) - upload KYC/AML policy files for RAG
  - Fabric IQ  (FabricTool)     - connect to a Fabric Data Agent
  - Web IQ     (BingGroundingTool) - Bing web grounding

Work IQ (Microsoft 365) has no SDK support and must be added in the Foundry
portal after the agent is created - see _print_work_iq_instructions below.

Usage:
    python scripts/setup_foundry_agent.py --connections
    python scripts/setup_foundry_agent.py --knowledge-files docs/kyc-policy.pdf
    python scripts/setup_foundry_agent.py --list
    python scripts/setup_foundry_agent.py --delete
"""

import argparse
import os
import sys
from pathlib import Path

from dotenv import load_dotenv

_SAMPLE_DIR = Path(__file__).resolve().parent.parent
_ENV_PATH = _SAMPLE_DIR / ".env"

load_dotenv(_ENV_PATH if _ENV_PATH.exists() else _SAMPLE_DIR / ".env.template")

sys.path.insert(0, str(_SAMPLE_DIR))
from agent.instructions import INSTRUCTIONS  # noqa: E402

AGENT_NAME = os.environ.get("FOUNDRY_AGENT_NAME", "ciq-client-intelligence-agent")
MODEL_DEPLOYMENT_NAME = os.environ.get("AZURE_AI_MODEL_DEPLOYMENT_NAME", "gpt-5.4")
PROJECT_ENDPOINT = os.environ.get("AZURE_AI_PROJECT_ENDPOINT", "").strip()
FABRIC_CONNECTION_ID = os.environ.get("FABRIC_CONNECTION_ID", "").strip()
BING_CONNECTION_NAME = os.environ.get("BING_CONNECTION_NAME", "").strip()


def _get_client():
    from azure.ai.projects import AIProjectClient
    from azure.identity import AzureCliCredential

    if not PROJECT_ENDPOINT:
        sys.exit(
            "ERROR: AZURE_AI_PROJECT_ENDPOINT is not set.\n"
            "Set it in .env to the same Foundry project used by src/ciq_app."
        )
    return AIProjectClient(endpoint=PROJECT_ENDPOINT, credential=AzureCliCredential())


def list_connections():
    client = _get_client()
    print("Connections in project:\n")
    for conn in client.connections.list():
        d = conn.as_dict() if hasattr(conn, "as_dict") else vars(conn)
        print(f"  Name: {d.get('name', 'N/A')}")
        print(f"    ID:   {d.get('id', 'N/A')}")
        print(f"    Type: {d.get('connection_type', d.get('type', 'N/A'))}\n")


def list_agents():
    client = _get_client()
    found = False
    for agent in client.agents.list_agents():
        found = True
        print(f"  Name: {agent.name}\n    ID: {agent.id}\n    Model: {agent.model}\n")
    if not found:
        print("  (no agents found)")


def delete_agent():
    client = _get_client()
    deleted = 0
    for agent in client.agents.list_agents():
        if agent.name == AGENT_NAME:
            client.agents.delete_agent(agent.id)
            deleted += 1
    print(f"Deleted {deleted} agent(s)." if deleted else f"No agent named '{AGENT_NAME}' found.")


def create_agent(knowledge_files: list[str] | None = None):
    from azure.ai.agents.models import (
        BingGroundingTool,
        FabricTool,
        FilePurpose,
        FileSearchTool,
        get_tool_definitions,
        get_tool_resources,
    )

    client = _get_client()

    for agent in client.agents.list_agents():
        if agent.name == AGENT_NAME:
            print(f"Agent already exists: {agent.id}. Use --delete first to recreate.")
            return

    tools = []

    # 1) Foundry IQ - knowledge files for RAG
    if knowledge_files:
        print("Foundry IQ: uploading knowledge files...")
        file_ids = []
        for path in knowledge_files:
            if not os.path.exists(path):
                print(f"  WARNING: file not found, skipping: {path}")
                continue
            uploaded = client.agents.files.upload_and_poll(file_path=path, purpose=FilePurpose.AGENTS)
            file_ids.append(uploaded.id)
        if file_ids:
            vector_store = client.agents.vector_stores.create_and_poll(
                file_ids=file_ids, name=f"{AGENT_NAME}-knowledge"
            )
            tools.append(FileSearchTool(vector_store_ids=[vector_store.id]))
            print(f"  Vector store: {vector_store.id}")
    else:
        print("Foundry IQ: no --knowledge-files provided, skipping.")

    # 2) Fabric IQ - Fabric Data Agent connection
    if FABRIC_CONNECTION_ID:
        tools.append(FabricTool(connection_id=FABRIC_CONNECTION_ID))
        print("Fabric IQ: connected.")
    else:
        print("Fabric IQ: FABRIC_CONNECTION_ID not set, skipping. Run --connections to find it.")

    # 3) Web IQ - Bing grounding connection
    if BING_CONNECTION_NAME:
        connection = client.connections.get(connection_name=BING_CONNECTION_NAME)
        tools.append(BingGroundingTool(connection_id=connection.id))
        print("Web IQ: connected.")
    else:
        print("Web IQ: BING_CONNECTION_NAME not set, skipping.")

    tool_definitions = get_tool_definitions(tools) if tools else []
    tool_resources = get_tool_resources(tools) if tools else {}

    create_kwargs = dict(
        model=MODEL_DEPLOYMENT_NAME,
        name=AGENT_NAME,
        instructions=INSTRUCTIONS,
        tools=tool_definitions,
    )
    if tool_resources:
        create_kwargs["tool_resources"] = tool_resources

    agent = client.agents.create_agent(**create_kwargs)
    print(f"\nAgent created: {agent.id} ({agent.name}), model {agent.model}")
    _print_work_iq_instructions()


def _print_work_iq_instructions():
    print(
        """
Next step: configure Work IQ (Microsoft 365) in the Foundry portal:
1. Open Microsoft Foundry -> your project -> Agents.
2. Select the agent created above.
3. Under "Knowledge and tools" -> "+ Add" -> select "Microsoft 365".
4. Grant the required permissions and save.
"""
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group()
    group.add_argument("--delete", action="store_true", help="Delete the existing agent")
    group.add_argument("--list", action="store_true", help="List agents in the project")
    group.add_argument("--connections", action="store_true", help="List project connections")
    parser.add_argument(
        "--knowledge-files",
        nargs="+",
        metavar="FILE",
        help="KYC/AML policy files to upload as Foundry IQ knowledge",
    )
    args = parser.parse_args()

    if args.connections:
        list_connections()
    elif args.list:
        list_agents()
    elif args.delete:
        delete_agent()
    else:
        create_agent(knowledge_files=args.knowledge_files)


if __name__ == "__main__":
    main()
