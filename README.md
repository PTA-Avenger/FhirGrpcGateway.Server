🚀 FHIR gRPC Gateway
A High-Performance Healthcare Data Facade built with .NET 10

📌 Overview
This project is a specialized gRPC Gateway that acts as a high-speed intermediary between clinical applications and HL7 FHIR R5 servers (like HAPI FHIR or Azure Health Data Services). By replacing traditional JSON/REST with binary Protobuf over HTTP/2, this gateway significantly reduces latency and payload size for mobile and internal microservice consumption.

🏗️ Architecture
The gateway is built using a Service-Oriented Architecture (SOA) where each clinical domain is isolated into its own gRPC service:

PatientApi: Identity management and CRUD operations.

ObservationApi: Vitals, laboratory results, and longitudinal data.

ConditionApi: Clinical problem lists and active diagnoses (SNOMED-CT).

MedicationApi: Prescription tracking and dosage instructions.

EncounterApi: Clinical visit context and temporal grouping.

DiagnosticReportApi: Grouped test results and professional conclusions.

PractitionerApi: Healthcare provider identity and qualifications.

🛠️ Tech Stack
Language: C# 14 / .NET 10.0

Framework: ASP.NET Core gRPC Service

Protocols: gRPC (HTTP/2), Protobuf v3

Healthcare Standard: HL7 FHIR R5 (HAPI FHIR)

Libraries: Hl7.Fhir.R5, Grpc.AspNetCore, Google.Protobuf

🌟 Key Features
Polymorphic Mapping: Seamlessly handles FHIR oneof types (e.g., effectiveDateTime vs effectivePeriod).

Advanced Search: Multi-parameter filtering using FHIR SearchParams.

Streaming $everything: Server-side streaming (gRPC stream) to deliver full patient medical records without memory overhead.

Type Safety: Eliminates "stringly-typed" healthcare data by enforcing a strict Protobuf contract.

Native AOT Ready: Optimized for sub-20ms startup times and minimal memory footprint in containerized environments.

🚦 Getting Started
Clone the Repo: git clone https://github.com/PTA-Avenger/FhirGrpcGateway.git

Restore Packages: dotnet restore

Run the Server: dotnet run --project FhirGrpcGateway.Server

Test: Use Postman (gRPC mode) or Visual Studio Endpoints Explorer to query localhost:5001.

Contact & Contributions
Developed by Thamy Mabena (@PTA-Avenger).
Feel free to reach out via LinkedIn for collaboration or graduate opportunities!