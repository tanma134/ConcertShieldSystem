import axiosClient from "./axiosClient";

const staffApi = {
  list: (eventId) => axiosClient.get(`/events/${eventId}/staff`),

  candidates: (eventId, q) =>
    axiosClient.get(`/events/${eventId}/staff/candidates`, { params: q ? { q } : {} }),

  assign: (eventId, staffUserId, gateName, canReviewReturns = false) =>
    axiosClient.post(`/events/${eventId}/staff`, { staffUserId, gateName, canReviewReturns }),

  setReturnReview: (eventId, staffUserId, canReviewReturns) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/return-review`, { canReviewReturns }),

  unassign: (eventId, staffUserId) => axiosClient.delete(`/events/${eventId}/staff/${staffUserId}`),

  getMyAssignments: (staffUserId) => axiosClient.get(`/events/staff/${staffUserId}`),
};

export default staffApi;
