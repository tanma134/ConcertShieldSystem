// Shared axiosClient (BASE_URL includes /api, attaches Bearer token & refresh)
import axiosClient from "./axiosClient";

const cleanParams = (params = {}) => {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== "" && v != null)
  );
};

const supportCaseApi = {
  // Create a new customer support case
  createCase: (data) => axiosClient.post("/support-cases", data),

  // List support cases (Staff sees assigned/unassigned; Admin sees all)
  getCases: (params) => axiosClient.get("/support-cases", { params: cleanParams(params) }),

  // View support case details with activity notes timeline
  getCaseDetail: (id) => axiosClient.get(`/support-cases/${id}`),

  // Retrieve active case for a specific order
  getCaseByOrder: (orderId) => axiosClient.get(`/support-cases/by-order/${orderId}`),

  // Assign support case to staff (Admin only)
  assignStaff: (id, data) => axiosClient.post(`/support-cases/${id}/assign`, data),

  // Resolve and close support case with mandatory resolution
  resolveCase: (id, data) => axiosClient.post(`/support-cases/${id}/resolve`, data),

  // Add internal investigation or communication note
  addNote: (id, data) => axiosClient.post(`/support-cases/${id}/notes`, data),
};

export default supportCaseApi;
