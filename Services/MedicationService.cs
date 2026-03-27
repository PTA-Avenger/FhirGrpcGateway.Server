using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using FhirGrpcGateway.Server; // Ensure this matches your .proto csharp_namespace

namespace FhirGrpcGateway.Server.Services;

using Task = System.Threading.Tasks.Task;

public class MedicationService : MedicationApi.MedicationApiBase
{
    private readonly ILogger<MedicationService> _logger;
    private readonly FhirClient _fhirClient;

    public MedicationService(ILogger<MedicationService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async Task<MedicationListResponse> GetPatientMedications(MedicationListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching medications for Patient {ID}", request.PatientId);

        // R5 search parameter for patient is 'subject'
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

            // Flattening the dosage
            DosageInstructionText = string.Join("; ", mr.DosageInstruction.Select(d => d.Text)),

            // R5 FIX: mr.Medication is a CodeableReference. We extract the Concept.
            Medication = MapToProtoConcept(mr.Medication?.Concept)
        };

        if (mr.AuthoredOnElement != null)
        {
            resp.AuthoredOn = Timestamp.FromDateTime(mr.AuthoredOnElement.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // R5 FIX: 'ReasonReference' is now 'Reason' (List of CodeableReference)
        if (mr.Reason != null)
        {
            resp.ReasonReferenceIds.AddRange(
                mr.Reason
                  .Where(r => r.Reference != null)
                  .Select(r => r.Reference.Reference)
            );
        }

        return resp;
    }

    // Explicitly use the generated namespace to avoid HL7.Fhir conflicts
    private FhirGrpcGateway.Server.CodeableConcept MapToProtoConcept(Hl7.Fhir.Model.CodeableConcept fhirConcept)
    {
        if (fhirConcept == null) return new FhirGrpcGateway.Server.CodeableConcept();

        var protoConcept = new FhirGrpcGateway.Server.CodeableConcept { Text = fhirConcept.Text ?? "" };

        if (fhirConcept.Coding != null)
        {
            protoConcept.Coding.AddRange(fhirConcept.Coding.Select(c => new FhirGrpcGateway.Server.Coding
            {
                System = c.System ?? "",
                Code = c.Code ?? "",
                Display = c.Display ?? ""
            }));
        }

        return protoConcept;
    }
}