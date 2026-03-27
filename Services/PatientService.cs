using FhirGrpcGateway.Server; // Ensure this matches your proto namespace
using FhirGrpcGateway.Server.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using System.Linq;
using FhirGrpcGateway.Server;

namespace FhirGrpcGateway.Server.Services;

// Using alias to resolve the "Task" conflict
using Task = System.Threading.Tasks.Task;

public class PatientService : PatientApi.PatientApiBase
{
    private readonly ILogger<PatientService> _logger;
    private readonly FhirClient _fhirClient;

    public PatientService(ILogger<PatientService> logger, FhirClient fhirClient)
    {
        _logger = logger;
        _fhirClient = fhirClient;
    }

    public override async System.Threading.Tasks.Task<PatientResponse> GetPatient(PatientRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fetching Patient {ID}", request.Id);
        try
        {
            var patient = await _fhirClient.ReadAsync<Patient>($"Patient/{request.Id}");
            return MapToResponse(patient, "Success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving patient {ID}", request.Id);
            throw new RpcException(new Status(StatusCode.NotFound, $"Patient {request.Id} not found"));
        }
    }

    public override async System.Threading.Tasks.Task<PatientListResponse> ListPatients(Empty request, ServerCallContext context)
    {
        try
        {
            var bundle = await _fhirClient.SearchAsync<Patient>();
            var response = new PatientListResponse();
            foreach (var entry in bundle.Entry.Where(e => e.Resource is Patient))
            {
                response.Patients.Add(MapToResponse((Patient)entry.Resource, "Listed"));
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing patients");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Search failed"));
        }
    }

    public override async System.Threading.Tasks.Task<PatientResponse> CreatePatient(PatientRequest request, ServerCallContext context)
    {
        try
        {
            // 1. We are creating a FHIR Model Patient
            var newPatient = new Hl7.Fhir.Model.Patient();

            // 2. Map Name (Use the FHIR Model HumanName)
            if (!string.IsNullOrEmpty(request.Name))
            {
                newPatient.Name.Add(new Hl7.Fhir.Model.HumanName { Family = request.Name });
            }

            // 3. Map Gender
            if (System.Enum.TryParse<Hl7.Fhir.Model.AdministrativeGender>(request.Gender, true, out var gender))
            {
                newPatient.Gender = gender;
            }

            newPatient.BirthDate = request.BirthDate;

            // 4. Map Telecom (Use FHIR Model ContactPoint and Enums)
            newPatient.Telecom = request.Telecom.Select(t => new Hl7.Fhir.Model.ContactPoint
            {
                System = System.Enum.TryParse<Hl7.Fhir.Model.ContactPoint.ContactPointSystem>(t.System, true, out var sys) ? sys : null,
                Value = t.Value,
                Use = System.Enum.TryParse<Hl7.Fhir.Model.ContactPoint.ContactPointUse>(t.Use, true, out var use) ? use : null
            }).ToList();

            var created = await _fhirClient.CreateAsync(newPatient);
            return MapToResponse(created, "Created Successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating patient");
            throw new RpcException(new Status(StatusCode.Internal, "Could not create Patient resource"));
        }
    }

    public override async System.Threading.Tasks.Task<PatientResponse> DeletePatient(PatientRequest request, ServerCallContext context)
    {
        try
        {
            // Fix: Use the correct string-based ID for deletion
            await _fhirClient.DeleteAsync($"Patient/{request.Id}");
            return new PatientResponse { Id = request.Id, Status = "Deleted Successfully" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for {ID}", request.Id);
            throw new RpcException(new Status(StatusCode.Internal, "Delete operation failed"));
        }
    }

    // FIXED: Return type changed to PatientListResponse to match Proto logic
    public override async System.Threading.Tasks.Task<PatientListResponse> SearchPatients(PatientSearchRequest request, ServerCallContext context)
    {
        var searchParams = new SearchParams();

        if (!string.IsNullOrEmpty(request.FamilyName))
            searchParams.Add("family", request.FamilyName);

        if (!string.IsNullOrEmpty(request.Identifier))
            searchParams.Add("identifier", request.Identifier);

        if (!string.IsNullOrEmpty(request.BirthdateAfter))
            searchParams.Add("birthdate", $"ge{request.BirthdateAfter}"); // FHIR uses 'ge' for >=

        try
        {
            var bundle = await _fhirClient.SearchAsync<Patient>(searchParams);
            var response = new PatientListResponse();
            foreach (var entry in bundle.Entry.Where(e => e.Resource is Patient))
            {
                response.Patients.Add(MapToResponse((Patient)entry.Resource, "Search Result"));
            }
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Search Error"));
        }
    }

    public override async System.Threading.Tasks.Task GetEverything(PatientRequest request, IServerStreamWriter<ResourceBundleResponse> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Streaming full record for Patient {ID}", request.Id);

        try
        {
            // Use InstanceOperationAsync to call the '$everything' custom operation
            // Parameters: (Resource Identity, Operation Name, Parameters [null])
            var bundle = await _fhirClient.InstanceOperationAsync(
                new Uri($"Patient/{request.Id}", UriKind.Relative),
                "everything") as Bundle;

            if (bundle == null) return;

            foreach (var entry in bundle.Entry.Where(e => e.Resource != null))
            {
                await responseStream.WriteAsync(new ResourceBundleResponse
                {
                    ResourceType = entry.Resource.TypeName,
                    JsonData = entry.Resource.ToJson()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch $everything for Patient {ID}", request.Id);
            throw new RpcException(new Status(StatusCode.Internal, "FHIR Everything Operation Failed"));
        }
    }

    private PatientResponse MapToResponse(Patient p, string status)
    {
        var resp = new PatientResponse
        {
            Id = p.Id ?? "",
            Name = p.Name.FirstOrDefault()?.Family ?? "Unknown",
            Gender = p.Gender?.ToString() ?? "Unknown",
            BirthDate = p.BirthDate ?? "N/A",
            Status = status
        };

        resp.Telecom.AddRange(p.Telecom.Select(t => new ContactPointMessage
        {
            System = t.System?.ToString() ?? "",
            Value = t.Value ?? "",
            Use = t.Use?.ToString() ?? ""
        }));

        return resp;
    }
}