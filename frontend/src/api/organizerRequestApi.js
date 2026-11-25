import axiosClient from "./axiosClient";

const organizerRequestApi = {
  create: (formData) => axiosClient.post("/organizer-requests", formData),

  getMyRequests: () => axiosClient.get("/organizer-requests/me"),

  getById: (requestId) => axiosClient.get(`/organizer-requests/${requestId}`),

  getAll: (status) =>
    axiosClient.get("/organizer-requests", { params: status ? { status } : {} }),

  review: (requestId, data) =>
    axiosClient.put(`/organizer-requests/${requestId}/review`, data),
};

export default organizerRequestApi;
