using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class MedicationService : MedicationApi.MedicationApiBase
{
    private readonly ILogger<MedicationService> _logger;
    private readonly FhirClient _fhirClient;

    public MedicationService(ILogger<MedicationService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<MedicationListResponse> GetPatientMedications(MedicationListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching medications for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (!string.IsNullOrEmpty(request.Status))
            searchParams.Add("status", request.Status);

        try
        {
            var bundle = await _fhirClient.SearchAsync<MedicationRequest>(searchParams);
            var response = new MedicationListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is MedicationRequest))
            {
                response.Medications.Add(MapToMedicationResponse((MedicationRequest)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Medication search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Medication Search Failed"));
        }
    }

    private MedicationResponse MapToMedicationResponse(MedicationRequest mr)
    {
        var resp = new MedicationResponse
        {
            Id = mr.Id,
            Status = mr.Status?.ToString() ?? "unknown",
            Intent = mr.Intent?.ToString() ?? "unknown",
            SubjectId = mr.Subject?.Reference ?? "",
            RequesterDisplay = mr.Requester?.Display ?? "Unknown Provider",

            // Flattening the dosage for easier AI consumption
            DosageInstructionText = string.Join("; ", mr.DosageInstruction.Select(d => d.Text)),

            // Mapping the Medication CodeableConcept (RxNorm)
            Medication = MapToProtoConcept(mr.Medication as CodeableConcept)
        };

        if (mr.AuthoredOnElement != null)
        {
            resp.AuthoredOn = Timestamp.FromDateTime(mr.AuthoredOnElement.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // Map Reason References (Linking back to Conditions/Observations)
        resp.ReasonReferenceIds.AddRange(mr.ReasonReference.Select(r => r.Reference));

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