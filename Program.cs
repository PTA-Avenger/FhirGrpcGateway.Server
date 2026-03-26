using FhirGrpcGateway.Server.Services;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Kestrel for HTTP/2 (Required for gRPC)
builder.WebHost.ConfigureKestrel(options =>
{
    // High-performance 2026 settings
    options.ListenLocalhost(5000, o => o.Protocols = HttpProtocols.Http2);
    options.ListenLocalhost(5001, o => o.Protocols = HttpProtocols.Http2);
});

// 2. Register the FHIR Client
// Point this to your target FHIR server (HAPI, Azure, or GCP)
builder.Services.AddScoped<FhirClient>(sp =>
{
    var settings = new FhirClientSettings
    {
        PreferredFormat = ResourceFormat.Json,
        Timeout = 30000 // 30 second timeout for large bundles
    };
    return new FhirClient("https://hapi.fhir.org/baseR5", settings);
});

// 3. Add gRPC Services to the Container
builder.Services.AddGrpc(options =>
{
    // Allow large message sizes for $everything operations
    options.MaxReceiveMessageSize = 5 * 1024 * 1024; // 5MB
    options.MaxSendMessageSize = 5 * 1024 * 1024;    // 5MB
});

// 4. (Optional) Add Health Checks
builder.Services.AddGrpcHealthChecks()
                .AddCheck("FhirStore", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

var app = builder.Build();

// 5. Map the gRPC Services (The Routing Table)
app.MapGrpcService<PatientService>();
app.MapGrpcService<ObservationService>();
app.MapGrpcService<ConditionService>();
app.MapGrpcService<AllergyService>();
app.MapGrpcService<DiagnosticReportService>();
app.MapGrpcService<EncounterService>();
app.MapGrpcService<MedicationService>();
app.MapGrpcService<PractitionerService>();
app.MapGrpcService<ProcedureService>();

// 6. Map Health Checks and Default Root
app.MapGrpcHealthChecksService();
app.MapGet("/", () => "FHIR gRPC Gateway is Online. Use a gRPC client (like Postman or BloomRPC) to connect.");

app.Run();