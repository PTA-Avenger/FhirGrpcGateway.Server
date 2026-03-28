import asyncio
from mcp.server.models import InitializationOptions
from mcp.server import NotificationOptions, Server
from mcp.server.stdio import stdio_server
import mcp.types as types
from gateway_client import FhirGatewayClient

# 1. Initialize the MCP Server
server = Server("fhir-grpc-gateway-ai")
gateway = FhirGatewayClient(host='localhost', port=5001)

# 2. Define the "List Tools" Capability
# ... (imports and server init from previous step)

@server.list_tools()
async def handle_list_tools() -> list[types.Tool]:
    return [
        types.Tool(
            name="get_patient_summary",
            description="Get basic identity and demographics.",
            inputSchema={"type": "object", "properties": {"patient_id": {"type": "string"}}, "required": ["patient_id"]}
        ),
        types.Tool(
            name="get_clinical_vitals",
            description="Get latest vitals like BP, Heart Rate, and Temp.",
            inputSchema={"type": "object", "properties": {"patient_id": {"type": "string"}}, "required": ["patient_id"]}
        ),
        types.Tool(
            name="get_active_conditions",
            description="List current medical diagnoses and problems.",
            inputSchema={"type": "object", "properties": {"patient_id": {"type": "string"}}, "required": ["patient_id"]}
        ),
        types.Tool(
            name="get_medication_list",
            description="List currently prescribed medications and dosages.",
            inputSchema={"type": "object", "properties": {"patient_id": {"type": "string"}}, "required": ["patient_id"]}
        )
    ]

@server.call_tool()
async def handle_call_tool(name: str, arguments: dict | None) -> list[types.TextContent]:
    pid = arguments.get("patient_id")
    
    try:
        if name == "get_patient_summary":
            return [types.TextContent(type="text", text=gateway.get_patient_summary(pid))]
        elif name == "get_clinical_vitals":
            return [types.TextContent(type="text", text="\n".join(gateway.get_vitals(pid)))]
        elif name == "get_active_conditions":
            return [types.TextContent(type="text", text="\n".join(gateway.get_conditions(pid)))]
        elif name == "get_medication_list":
            return [types.TextContent(type="text", text="\n".join(gateway.get_medications(pid)))]
    except Exception as e:
        return [types.TextContent(type="text", text=f"Error: {str(e)}")]