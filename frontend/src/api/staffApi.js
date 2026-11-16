import axiosClient from "./axiosClient";

// staff assigned to one event.
const staffApi = {
  // staff currently assigned to the event.
  list: (eventId) => axiosClient.get(`/events/${eventId}/staff`),

  // Staff accounts the organizer can pick from; q filters by name or email.
  candidates: (eventId, q) =>
    axiosClient.get(`/events/${eventId}/staff/candidates`, { params: q ? { q } : {} }),

  // assign a Staff account to the event.
  assign: (eventId, staffUserId, gateName, canReviewReturns = false) =>
    axiosClient.post(`/events/${eventId}/staff`, { staffUserId, gateName, canReviewReturns }),

  // Let (or stop) an assigned staff member reviewing ticket return requests of the event.
  setReturnReview: (eventId, staffUserId, canReviewReturns) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/return-review`, { canReviewReturns }),

  // remove a staff member from the event.
  unassign: (eventId, staffUserId) => axiosClient.delete(`/events/${eventId}/staff/${staffUserId}`),

  // Events & gates assigned to this staff member
  getMyAssignments: (staffUserId) => axiosClient.get(`/events/staff/${staffUserId}`),
};

export default staffApi;
