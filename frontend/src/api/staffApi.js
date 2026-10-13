import axiosClient from "./axiosClient";

// UC_14.3 / UC_14.4: staff assigned to one event.
const staffApi = {
  // UC_14.3: staff currently assigned to the event.
  list: (eventId) => axiosClient.get(`/events/${eventId}/staff`),

  // UC_14.4: Staff accounts the organizer can pick from; q filters by name or email.
  candidates: (eventId, q) =>
    axiosClient.get(`/events/${eventId}/staff/candidates`, { params: q ? { q } : {} }),

  // UC_14.4: assign a Staff account to the event.
  assign: (eventId, staffUserId, gateName, canReviewReturns = false) =>
    axiosClient.post(`/events/${eventId}/staff`, { staffUserId, gateName, canReviewReturns }),

  // Let (or stop) an assigned staff member reviewing ticket return requests of the event.
  setReturnReview: (eventId, staffUserId, canReviewReturns) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/return-review`, { canReviewReturns }),

  // UC_14.4: remove a staff member from the event.
  unassign: (eventId, staffUserId) => axiosClient.delete(`/events/${eventId}/staff/${staffUserId}`),
};

export default staffApi;
