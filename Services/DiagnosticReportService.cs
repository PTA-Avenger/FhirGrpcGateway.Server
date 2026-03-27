using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class DiagnosticReportService : DiagnosticReportApi.DiagnosticReportApiBase
{
    private readonly ILogger<DiagnosticReportService> _logger;
    private readonly FhirClient _fhirClient;

    public DiagnosticReportService(ILogger<DiagnosticReportService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<ReportListResponse> GetPatientReports(ReportListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching Diagnostic Reports for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (!string.IsNullOrEmpty(request.Category))
            searchParams.Add("category", request.Category);

        try
        {
            var bundle = await _fhirClient.SearchAsync<DiagnosticReport>(searchParams);
            var response = new ReportListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is DiagnosticReport))
            {
                response.Reports.Add(MapToReportResponse((DiagnosticReport)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Diagnostic Report search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Report Search Failed"));
        }
    }

    private DiagnosticReportResponse MapToReportResponse(DiagnosticReport dr)
    {
        var resp = new DiagnosticReportResponse
        {
            Id = dr.Id,
            Status = dr.Status?.ToString() ?? "unknown",
            Code = MapToProtoConcept(dr.Code),
            Subject = new Reference { Reference_ = dr.Subject?.Reference ?? "" },
            Conclusion = dr.Conclusion ?? ""
        };

        // Map Timestamps
        if (dr.Effective is FhirDateTime fdt)
            resp.EffectiveDateTime = Timestamp.FromDateTime(fdt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);

        if (dr.Issued.HasValue)
            resp.Issued = Timestamp.FromDateTime(dr.Issued.Value.UtcDateTime);

        // Map Result References (Linking to Observations)
        resp.Result.AddRange(dr.Result.Select(r => new Reference
        {
            Reference_ = r.Reference ?? "",
            Display = r.Display ?? ""
        }));

        return resp;
    }

    private CodeableConcept MapToProtoConcept(Hl7.Fhir.Model.CodeableConcept fhirConcept)
    {
        if (fhirConcept == null) return new CodeableConcept();
        var protoConcept = new CodeableConcept { Text = fhirConcept.Text ?? "" };
        protoConcept.Coding.AddRange(fhirConcept.Coding.Select(c => new Coding
        {
            System = c.System ?? "",
            Code = c.Code ?? "",
            Display = c.Display ?? ""
        }));
        return protoConcept;
    }
}