using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;

namespace FhirGrpcGateway.Server.Services;

public class PractitionerService : PractitionerApi.PractitionerApiBase
{
    private readonly ILogger<PractitionerService> _logger;
    private readonly FhirClient _fhirClient;

    public PractitionerService(ILogger<PractitionerService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<PractitionerResponse> GetPractitioner(PractitionerRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching Practitioner {ID} from FHIR Store", request.Id);

        try
        {
            var practitioner = await _fhirClient.ReadAsync<Practitioner>($"Practitioner/{request.Id}");

            var resp = new PractitionerResponse
            {
                Id = practitioner.Id,
                Active = practitioner.Active ?? true
            };

            // Map Identifiers (e.g., NPI numbers)
            resp.Identifier.AddRange(practitioner.Identifier.Select(i => new Protos.Identifier
            {
                System = i.System ?? "",
                Value = i.Value ?? ""
            }));

            // Map Names
            resp.Name.AddRange(practitioner.Name.Select(n => new Protos.HumanName
            {
                Family = n.Family ?? "",
                Given = { n.Given ?? new List<string>() },
                Prefix = { n.Prefix ?? new List<string>() }
            }));

            // Map Telecom
            resp.Telecom.AddRange(practitioner.Telecom.Select(t => new Protos.ContactPoint
            {
                System = t.System?.ToString() ?? "",
                Value = t.Value ?? "",
                Use = t.Use?.ToString() ?? ""
            }));

            // Map Qualifications (MD, RN, Specializations)
            resp.Qualification.AddRange(practitioner.Qualification.Select(q => new Protos.Qualification
            {
                Identifier = q.Identifier.Select(i => new Protos.Identifier { Value = i.Value }).FirstOrDefault() ?? new Protos.Identifier(),
                Code = MapToProtoConcept(q.Code)
            }));

            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Practitioner {ID} not found", request.Id);
            throw new RpcException(new Status(StatusCode.NotFound, $"Practitioner {request.Id} not found"));
        }
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