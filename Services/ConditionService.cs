using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using Task = System.Threading.Tasks.Task;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class ConditionService : ConditionApi.ConditionApiBase
{
    private readonly ILogger<ConditionService> _logger;
    private readonly FhirClient _fhirClient;

    public ConditionService(ILogger<ConditionService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    // 1. GET LIST OF CONDITIONS (Filter by Patient + Category)
    public override async System.Threading.Tasks.Task<ConditionListResponse> GetPatientConditions(ConditionListRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Listing conditions for Patient {ID}", request.PatientId);

        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (!string.IsNullOrEmpty(request.Category))
            searchParams.Add("category", request.Category);

        try
        {
            var bundle = await _fhirClient.SearchAsync<Condition>(searchParams);
            var response = new ConditionListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is Condition))
            {
                response.Conditions.Add(MapConditionToResponse((Condition)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Condition list search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Condition Search Failed"));
        }
    }

    // 2. GET SINGLE CONDITION
    public override async System.Threading.Tasks.Task<ConditionResponse> GetCondition(ConditionRequest request, ServerCallContext context)
    {
        try
        {
            var condition = await _fhirClient.ReadAsync<Condition>($"Condition/{request.Id}");
            return MapConditionToResponse(condition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Condition {ID} not found", request.Id);
            throw new RpcException(new Status(StatusCode.NotFound, $"Condition {request.Id} not found"));
        }
    }

    private ConditionResponse MapConditionToResponse(Condition c)
    {
        var resp = new ConditionResponse
        {
            Id = c.Id,
            ClinicalStatus = MapToProtoConcept(c.ClinicalStatus),
            VerificationStatus = MapToProtoConcept(c.VerificationStatus),
            Code = MapToProtoConcept(c.Code),
            Subject = c.Subject?.Reference ?? "",
            RecordedDate = c.RecordedDate ?? "",
            Asserter = c.Asserter?.Reference ?? "",
            Note = string.Join(" | ", c.Note.Select(n => n.Text))
        };

        // Map Category List
        resp.Category.AddRange(c.Category.Select(MapToProtoConcept));

        // Handle Onset (oneof)
        if (c.Onset is FhirDateTime fdt)
            resp.OnsetDateTime = Timestamp.FromDateTime(fdt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        else if (c.Onset is FhirString fs)
            resp.OnsetString = fs.Value;

        // Handle Abatement (oneof)
        if (c.Abatement is FhirDateTime adt)
            resp.AbatementDateTime = Timestamp.FromDateTime(adt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        else if (c.Abatement is FhirString @as)
            resp.AbatementString = @as.Value;
        else if (c.Abatement is FhirBoolean ab)
            resp.AbatementBoolean = ab.Value ?? false;

        return resp;
    }

    private Protos.CodeableConcept MapToProtoConcept(Hl7.Fhir.Model.CodeableConcept fhirConcept)
    {
        if (fhirConcept == null) return new Protos.CodeableConcept();

        var protoConcept = new Protos.CodeableConcept { Text = fhirConcept.Text ?? "" };
        protoConcept.Coding.AddRange(fhirConcept.Coding.Select(coding => new Protos.Coding
        {
            System = coding.System ?? "",
            Code = coding.Code ?? "",
            Display = coding.Display ?? ""
        }));

        return protoConcept;
    }
}