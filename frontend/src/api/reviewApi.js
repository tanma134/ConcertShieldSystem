import axiosClient from "./axiosClient";

const reviewApi = {
  getByEvent: (eventId) => axiosClient.get(`/reviews/event/${eventId}`),
  create: (payload) => axiosClient.post("/reviews", payload),
  update: (reviewId, payload) => axiosClient.put(`/reviews/${reviewId}`, payload),
  remove: (reviewId) => axiosClient.delete(`/reviews/${reviewId}`),
  getAdmin: (params = {}) => axiosClient.get("/reviews/admin", { params }),
  setVisibility: (reviewId, hidden) => axiosClient.put(`/reviews/admin/${reviewId}`, { hidden }),
  reply: (reviewId, comment) => axiosClient.post(`/reviews/${reviewId}/replies`, { comment }),
};

export default reviewApi;
