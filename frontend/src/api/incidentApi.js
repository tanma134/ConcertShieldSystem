import axiosClient from "./axiosClient";

const incidentApi = {
  getIncidents: (params) => {
    return axiosClient.get("/incidents", { params });
  },

  getIncidentById: (id) => {
    return axiosClient.get(`/incidents/${id}`);
  },

  reportIncident: (data) => {
    return axiosClient.post("/incidents", data);
  },

  updateIncident: (id, data) => {
    return axiosClient.put(`/incidents/${id}`, data);
  },

  updateStatus: (id, data) => {
    return axiosClient.patch(`/incidents/${id}/status`, data);
  },

  escalateIncident: (id, data) => {
    return axiosClient.post(`/incidents/${id}/escalate`, data);
  },

  resolveIncident: (id, data) => {
    return axiosClient.post(`/incidents/${id}/resolve`, data);
  },

  getResolution: (id) => {
    return axiosClient.get(`/incidents/${id}/resolution`);
  },

  getHistory: (id) => {
    return axiosClient.get(`/incidents/${id}/history`);
  },

  getEvidences: (incidentId) => {
    return axiosClient.get(`/incidents/${incidentId}/evidences`);
  },

  uploadEvidence: (incidentId, file, caption = "") => {
    const formData = new FormData();
    formData.append("file", file);
    if (caption) {
      formData.append("caption", caption);
    }
    return axiosClient.post(`/incidents/${incidentId}/evidences`, formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    });
  },

  deleteEvidence: (incidentId, evidenceId) => {
    return axiosClient.delete(`/incidents/${incidentId}/evidences/${evidenceId}`);
  },
};

export default incidentApi;
