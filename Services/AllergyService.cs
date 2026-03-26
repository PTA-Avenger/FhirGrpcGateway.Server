using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class AllergyService : AllergyApi.AllergyApiBase
{
    private readonly ILogger<AllergyService> _logger;
    private readonly FhirClient _fhirClient;

    public AllergyService(ILogger<AllergyService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<AllergyListResponse> GetPatientAllergies(AllergyListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching allergies for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"patient=Patient/{request.PatientId}");

        if (!string.IsNullOrEmpty(request.ClinicalStatus))
            searchParams.Add("clinical-status", request.ClinicalStatus);

        try
        {
            var bundle = await _fhirClient.SearchAsync<AllergyIntolerance>(searchParams);
            var response = new AllergyListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is AllergyIntolerance))
            {
                response.Allergies.Add(MapToAllergyResponse((AllergyIntolerance)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Allergy search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Allergy Search Failed"));
        }
    }

    private AllergyIntoleranceResponse MapToAllergyResponse(AllergyIntolerance a)
    {
        var resp = new AllergyIntoleranceResponse
        {
            Id = a.Id,
            Code = MapToProtoConcept(a.Code),
            Type = a.Type?.ToString() ?? "unknown",
            Category = a.Category?.FirstOrDefault()?.ToString() ?? "unknown",
            Criticality = a.Criticality?.ToString() ?? "unable-to-assess",
            ClinicalStatus = a.ClinicalStatus?.Coding?.FirstOrDefault()?.Code ?? "unknown"
        };

        if (a.RecordedDateElement != null)
        {
            resp.RecordedDate = Timestamp.FromDateTime(a.RecordedDateElement.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // Map Reactions
        foreach (var r in a.Reaction)
        {
            var protoReaction = new Reaction
            {
                Substance = MapToProtoConcept(r.Substance),
                Severity = r.Severity?.ToString() ?? "unknown"
            };

            protoReaction.Manifestation.AddRange(r.Manifestation.Select(m => MapToProtoConcept(m)));
            resp.Reaction.Add(protoReaction);
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