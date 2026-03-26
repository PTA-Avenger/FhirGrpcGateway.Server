using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class EncounterService : EncounterApi.EncounterApiBase
{
    private readonly ILogger<EncounterService> _logger;
    private readonly FhirClient _fhirClient;

    public EncounterService(ILogger<EncounterService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<EncounterListResponse> GetPatientEncounters(EncounterListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching encounters for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (request.DateFrom != null)
            searchParams.Add("date", $"ge{request.DateFrom.ToDateTime():yyyy-MM-dd}");

        try
        {
            var bundle = await _fhirClient.SearchAsync<Encounter>(searchParams);
            var response = new EncounterListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is Encounter))
            {
                response.Encounters.Add(MapToEncounterResponse((Encounter)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Encounter search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Encounter Search Failed"));
        }
    }

    private EncounterResponse MapToEncounterResponse(Encounter e)
    {
        var resp = new EncounterResponse
        {
            Id = e.Id,
            Status = e.Status?.ToString() ?? "unknown",
            Class = MapToProtoCoding(e.Class),
            SubjectId = e.Subject?.Reference ?? "",
            LocationDisplay = e.Location?.FirstOrDefault()?.Location?.Display ?? "Unknown Location"
        };

        // Map Timestamps from FHIR Period
        if (e.Period != null)
        {
            if (e.Period.StartElement != null)
                resp.Start = Timestamp.FromDateTime(e.Period.StartElement.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);

            if (e.Period.EndElement != null)
                resp.End = Timestamp.FromDateTime(e.Period.EndElement.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // Map Repeated Concepts (Type and Reason)
        resp.Type.AddRange(e.Type.Select(MapToProtoConcept));
        resp.ReasonCode.AddRange(e.ReasonCode.Select(r => MapToProtoConcept(r.CodeableConcept)));

        return resp;
    }

    private Protos.CodeableConcept MapToProtoConcept(Hl7.Fhir.Model.CodeableConcept fhirConcept)
    {
        if (fhirConcept == null) return new Protos.CodeableConcept();
        var protoConcept = new Protos.CodeableConcept { Text = fhirConcept.Text ?? "" };
        protoConcept.Coding.AddRange(fhirConcept.Coding.Select(MapToProtoCoding));
        return protoConcept;
    }

    private Protos.Coding MapToProtoCoding(Hl7.Fhir.Model.Coding c) => c == null ? new Protos.Coding() :
        new Protos.Coding { System = c.System ?? "", Code = c.Code ?? "", Display = c.Display ?? "" };
}