import axiosClient from "./axiosClient";

const cleanParams = (params = {}) => {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== "" && v != null)
  );
};

const supportCaseApi = {
  createCase: (data) => axiosClient.post("/support-cases", data),

  getCases: (params) => axiosClient.get("/support-cases", { params: cleanParams(params) }),

  getCaseDetail: (id) => axiosClient.get(`/support-cases/${id}`),

  getCaseByOrder: (orderId) => axiosClient.get(`/support-cases/by-order/${orderId}`),

  assignStaff: (id, data) => axiosClient.post(`/support-cases/${id}/assign`, data),

  resolveCase: (id, data) => axiosClient.post(`/support-cases/${id}/resolve`, data),

  addNote: (id, data) => axiosClient.post(`/support-cases/${id}/notes`, data),
};

export default supportCaseApi;
