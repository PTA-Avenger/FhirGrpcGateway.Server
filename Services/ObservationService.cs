using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes;
using FhirGrpcGateway.Server; // Matches your .proto csharp_namespace

namespace FhirGrpcGateway.Server.Services;

using Task = System.Threading.Tasks.Task;

public class ObservationService : ObservationApi.ObservationApiBase
{
    private readonly ILogger<ObservationService> _logger;
    private readonly FhirClient _fhirClient;

    public ObservationService(ILogger<ObservationService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async Task<ObservationListResponse> GetPatientObservations(ObservationRequest request, ServerCallContext context)
    {
        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        if (request.DateFrom != null)
            searchParams.Add("date", $"ge{request.DateFrom.ToDateTime():yyyy-MM-dd}");

        if (request.DateTo != null)
            searchParams.Add("date", $"le{request.DateTo.ToDateTime():yyyy-MM-dd}");

        if (request.Limit > 0)
            searchParams.Count = request.Limit;

        try
        {
            var bundle = await _fhirClient.SearchAsync<Observation>(searchParams);
            var response = new ObservationListResponse();

            foreach (var entry in bundle.Entry.Where(e => e.Resource is Observation))
            {
                response.Observations.Add(MapObservation((Observation)entry.Resource));
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Observation search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Search Error"));
        }
    }

    private ObservationResponse MapObservation(Observation obs)
    {
        var result = new ObservationResponse
        {
            Id = obs.Id,
            Status = obs.Status?.ToString() ?? "unknown",
            Code = MapCoding(obs.Code?.Coding.FirstOrDefault())
        };

        if (obs.Effective is FhirDateTime fdt)
        {
            result.EffectiveDateTime = Timestamp.FromDateTime(fdt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // FIX: Explicitly check for HL7.Fhir.Model.Quantity to avoid pattern matching errors
        if (obs.Value is Hl7.Fhir.Model.Quantity q)
        {
            result.ValueQuantity = new FhirGrpcGateway.Server.Quantity
            {
                // FIX: Cast null-coalesced decimal to double (using 0.0m for decimal zero)
                Value = (double)(q.Value ?? 0.0m),
                Unit = q.Unit ?? "",
                System = q.System ?? "",
                Code = q.Code ?? ""
            };
        }
        else if (obs.Value is Hl7.Fhir.Model.CodeableConcept cc)
        {
            result.ValueConcept = MapCoding(cc.Coding.FirstOrDefault());
        }
        else if (obs.Value is FhirString fs)
        {
            result.ValueString = fs.Value;
        }

        if (obs.Component != null && obs.Component.Any())
        {
            foreach (var comp in obs.Component)
            {
                // FIX: Use the correct generated namespace for Component
                var protoComp = new FhirGrpcGateway.Server.Component
                {
                    Code = MapCoding(comp.Code?.Coding.FirstOrDefault())
                };

                if (comp.Value is Hl7.Fhir.Model.Quantity cq)
                {
                    protoComp.ValueQuantity = new FhirGrpcGateway.Server.Quantity
                    {
                        Value = (double)(cq.Value ?? 0.0m),
                        Unit = cq.Unit ?? ""
                    };
                }

                result.Component.Add(protoComp);
            }
        }

        return result;
    }

    // FIX: Ensure return type and input type are explicitly defined
    private FhirGrpcGateway.Server.Coding MapCoding(Hl7.Fhir.Model.Coding c)
    {
        if (c == null) return new FhirGrpcGateway.Server.Coding();
        return new FhirGrpcGateway.Server.Coding
        {
            System = c.System ?? "",
            Code = c.Code ?? "",
            Display = c.Display ?? ""
        };
    }
}