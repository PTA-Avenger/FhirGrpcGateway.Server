🚀 PTA-Avenger: FHIR gRPC & AI Gateway
A High-Performance Clinical Data Facade & MCP Intelligence Layer
Built with .NET 10 (C# 14) & Python (MCP)

📌 Overview
PTA-Avenger is a dual-layer architectural solution designed to solve the "latency and reasoning" gap in modern digital health. It acts as a high-speed binary intermediary between HL7 FHIR R5 data stores and Autonomous AI Agents.

By converting traditional, heavy JSON/REST payloads into Protobuf over HTTP/2, this gateway reduces data overhead by up to 70-80%, making it ideal for mobile clinical apps and Large Language Model (LLM) context injection.

🏗️ Architecture: The Two-Tier Approach
1. The Data Tier (.NET 10 gRPC Gateway)
A Service-Oriented Architecture (SOA) that enforces strict type-safety for clinical domains:

PatientApi: Identity management and demographics.

ObservationApi: Real-time vitals, labs, and longitudinal data.

ConditionApi: Active problem lists and SNOMED-CT diagnoses.

MedicationApi: Prescription tracking and R5 CodeableReference mapping.

Procedure & EncounterApi: Clinical context and temporal grouping.

2. The Intelligence Tier (Python MCP Server)
A Model Context Protocol (MCP) implementation that exposes the gRPC services as "Tools" for AI models (like Claude 3.5/4 or Gemini 1.5/2). This allows an AI to:

Autonomously "browse" a patient's history.

Cross-reference medications with active conditions.

Summarize complex FHIR bundles into plain-language clinical notes.

🛠️ Tech Stack
Backend: C# 14 / .NET 10.0 (High-performance Kestrel server)

AI Orchestration: Python 3.12+ / MCP SDK

Communication: gRPC (Binary Protobuf), HTTP/2

Healthcare Standards: HL7 FHIR R5 (HAPI FHIR)

Core Libraries: Hl7.Fhir.R5, Grpc.AspNetCore, Google.Protobuf, mcp

🌟 Pro Features
R5 Polymorphic Mapping: Expertly handles FHIR oneof types (e.g., effectiveDateTime vs effectivePeriod) and the new R5 CodeableReference structures.

Streaming $everything: Implements gRPC server-side streaming to deliver entire medical records without memory spikes.

AI-Tool Discovery: The MCP layer allows LLMs to "discover" clinical endpoints as functions, preventing hallucinated data structures.

Type-Safe Healthcare: Replaces "stringly-typed" JSON with a strictly enforced Protobuf contract, ensuring data integrity across the wire.

🚦 Getting Started
1. Run the gRPC Gateway (.NET)
Bash
cd FhirGrpcGateway.Server
dotnet run
# Server starts on http://localhost:5000 and https://localhost:5001
2. Run the MCP AI Server (Python)
Bash
cd McpBrain
pip install -r requirements.txt
python mcp_server.py
3. Connect to AI (Claude Desktop example)
Add this to your claude_desktop_config.json:

JSON
"fhir-gateway": {
  "command": "python",
  "args": ["/path/to/mcp_server.py"]
}
🎓 About the Developer
Developed by Thamy Mabena, a Computer Science student and Student Assistant at North-West University (NWU).

This project represents a deep dive into Distributed Systems, Health Informatics, and AI Integration, aimed at modernizing how clinical data serves both human providers and artificial intelligence.

Connect with me:
LinkedIn | GitHub | Portfolio

🚀 Ready to Push?
Save this as README.md in your root folder.

Make sure your .gitignore is set (no bin/, obj/, or __pycache__).

Run these commands:

Bash
git add .
git commit -m "feat: complete fhir r5 grpc gateway and mcp ai layer"
git push origin main