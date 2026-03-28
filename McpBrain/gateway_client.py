import grpc
import fhir_patient_pb2_grpc as patient_grpc
import fhir_observation_pb2_grpc as obs_grpc
import fhir_condition_pb2_grpc as cond_grpc
import fhir_medication_pb2_grpc as med_grpc
import fhir_patient_pb2 as patient_msgs
import fhir_observation_pb2 as obs_msgs
import fhir_condition_pb2 as cond_msgs
import fhir_medication_pb2 as med_msgs

class FhirGatewayClient:
    def __init__(self, host='localhost', port=5001):
        self.channel = grpc.insecure_channel(f'{host}:{port}')
        self.patient_stub = patient_grpc.PatientApiStub(self.channel)
        self.obs_stub = obs_grpc.ObservationApiStub(self.channel)
        self.cond_stub = cond_grpc.ConditionApiStub(self.channel)
        self.med_stub = med_grpc.MedicationApiStub(self.channel)

    def get_patient_summary(self, patient_id):
        resp = self.patient_stub.GetPatient(patient_msgs.PatientRequest(id=patient_id))
        return f"Name: {resp.name}, Gender: {resp.gender}, DOB: {resp.birth_date}"

    def get_vitals(self, patient_id):
        resp = self.obs_stub.GetPatientObservations(obs_msgs.ObservationRequest(patient_id=patient_id, limit=5))
        return [f"Obs: {o.code.display or 'Unknown'}, Value: {o.value_quantity.value} {o.value_quantity.unit}" for o in resp.observations]

    def get_conditions(self, patient_id):
        resp = self.cond_stub.GetPatientConditions(cond_msgs.ConditionListRequest(patient_id=patient_id))
        return [f"Diagnosis: {c.code.text}, Status: {c.clinical_status.text}" for c in resp.conditions]

    def get_medications(self, patient_id):
        resp = self.med_stub.GetPatientMedications(med_msgs.MedicationListRequest(patient_id=patient_id))
        return [f"Med: {m.medication.text}, Dosage: {m.dosage_instruction_text}" for m in resp.medications]