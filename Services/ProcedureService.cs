using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class ProcedureService : ProcedureApi.ProcedureApiBase
{
    private readonly ILogger<ProcedureService> _logger;
    private readonly FhirClient _fhirClient;

    public ProcedureService(ILogger<ProcedureService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<ProcedureListResponse> GetPatientProcedures(ProcedureListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching procedures for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (request.DateFrom != null)
            searchParams.Add("date", $"ge{request.DateFrom.ToDateTime():yyyy-MM-dd}");

        try
        {
            var bundle = await _fhirClient.SearchAsync<Procedure>(searchParams);
            var response = new ProcedureListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is Procedure))
            {
                response.Procedures.Add(MapToProcedureResponse((Procedure)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Procedure search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Procedure Search Failed"));
        }
    }

    private ProcedureResponse MapToProcedureResponse(Procedure p)
    {
        var resp = new ProcedureResponse
        {
            Id = p.Id,
            Status = p.Status?.ToString() ?? "unknown",
            Code = MapToProtoConcept(p.Code),
            Subject = new Protos.Reference { Reference_ = p.Subject?.Reference ?? "" },
            OutcomeText = p.Outcome?.Text ?? ""
        };

        // Handle 'oneof performed' (Polymorphic Mapping)
        if (p.Performed is FhirDateTime fdt)
        {
            resp.PerformedDateTime = Timestamp.FromDateTime(fdt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }
        else if (p.Performed is FhirString fs)
        {
            resp.PerformedString = fs.Value;
        }

        // Map Reason Codes
        resp.ReasonCode.AddRange(p.ReasonCode.Select(MapToProtoConcept));

        // Map Recorder References
        if (p.Recorder != null)
        {
            resp.Recorder.Add(new Protos.Reference { Reference_ = p.Recorder.Reference ?? "" });
        }

        return resp;
    }

    private Protos.CodeableConcept MapToProtoConcept(Hl7.Fhir.Model.CodeableConcept fhirConcept)
    {
        if (fhirConcept == null) return new Protos.CodeableConcept();
        var protoConcept = new Protos.CodeableConcept { Text = fhirConcept.Text ?? "" };
        protoConcept.Coding.AddRange(fhirConcept.Coding.Select(c => new Protos.Coding
        {
            System = c.System ?? "",
            Code = c.Code ?? "",
            Display = c.Display ?? ""
        }));
        return protoConcept;
    }
}