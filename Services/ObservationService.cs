using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Google.Protobuf.WellKnownTypes; // For Timestamp conversion
using Task = System.Threading.Tasks.Task;
 using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

public class ObservationService : ObservationApi.ObservationApiBase
{
    private readonly ILogger<ObservationService> _logger;
    private readonly FhirClient _fhirClient;

    public ObservationService(ILogger<ObservationService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<ObservationListResponse> GetPatientObservations(ObservationRequest request, ServerCallContext context)
    {
        var searchParams = new SearchParams().Where($"subject=Patient/{request.PatientId}");

        // 1. Handle Date Filtering (Converting Protobuf Timestamp to FHIR strings)
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
                var obs = (Observation)entry.Resource;
                response.Observations.Add(MapObservation(obs));
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

        // 2. Map Effective Date to Protobuf Timestamp
        if (obs.Effective is FhirDateTime fdt)
        {
            result.EffectiveDateTime = Timestamp.FromDateTime(fdt.ToDateTimeOffset(TimeSpan.Zero).UtcDateTime);
        }

        // 3. Handle 'oneof value' (The Core Logic)
        if (obs.Value is Quantity q)
        {
            result.ValueQuantity = new Protos.Quantity
            {
                Value = (double)(q.Value ?? 0),
                Unit = q.Unit ?? "",
                System = q.System ?? "",
                Code = q.Code ?? ""
            };
        }
        else if (obs.Value is CodeableConcept cc)
        {
            result.ValueConcept = MapCoding(cc.Coding.FirstOrDefault());
        }
        else if (obs.Value is FhirString fs)
        {
            result.ValueString = fs.Value;
        }

        // 4. Handle Components (e.g. Blood Pressure)
        if (obs.Component != null && obs.Component.Any())
        {
            foreach (var comp in obs.Component)
            {
                var protoComp = new Protos.Component { Code = MapCoding(comp.Code?.Coding.FirstOrDefault()) };
                // Apply the same 'oneof' logic for the component value...
                if (comp.Value is Quantity cq)
                    protoComp.ValueQuantity = new Protos.Quantity { Value = (double)(cq.Value ?? 0), Unit = cq.Unit ?? "" };

                result.Component.Add(protoComp);
            }
        }

        return result;
    }

    private Protos.Coding MapCoding(Hl7.Fhir.Model.Coding c) => c == null ? new Protos.Coding() :
        new Protos.Coding { System = c.System ?? "", Code = c.Code ?? "", Display = c.Display ?? "" };
}