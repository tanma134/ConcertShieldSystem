import axiosClient from "./axiosClient";

// UC_14.3 / UC_14.4: staff assigned to one event & Organizer Staff CRUD
const staffApi = {
  // Organizer Staff CRUD
  getOrganizerStaff: (q) =>
    axiosClient.get("/organizer/staff", { params: q ? { q } : {} }),
  createOrganizerStaff: (data) =>
    axiosClient.post("/organizer/staff", data),
  updateOrganizerStaff: (id, data) =>
    axiosClient.put(`/organizer/staff/${id}`, data),
  deleteOrganizerStaff: (id) =>
    axiosClient.delete(`/organizer/staff/${id}`),

  // UC_14.3: staff currently assigned to the event.
  list: (eventId) => axiosClient.get(`/events/${eventId}/staff`),

  // UC_14.4: Staff accounts the organizer can pick from; q filters by name or email.
  candidates: (eventId, q) =>
    axiosClient.get(`/events/${eventId}/staff/candidates`, { params: q ? { q } : {} }),

  // UC_14.4: assign a Staff account to the event with permissions and shift.
  assign: (eventId, staffUserId, gateName, canReviewReturns = false, shiftStartTime = null, shiftEndTime = null, shiftDate = null, canManageIncidents = false, canManageSupportCases = false, canCheckin = true) =>
    axiosClient.post(`/events/${eventId}/staff`, {
      staffUserId,
      gateName,
      canReviewReturns,
      shiftStartTime,
      shiftEndTime,
      shiftDate,
      canManageIncidents,
      canManageSupportCases,
      canCheckin,
    }),

  // Update gate and shift hours of an assigned staff member
  updateShift: (eventId, staffUserId, { gateName, shiftStartTime, shiftEndTime, shiftDate }) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/shift`, { gateName, shiftStartTime, shiftEndTime, shiftDate }),

  // Let (or stop) an assigned staff member reviewing ticket return requests of the event.
  setReturnReview: (eventId, staffUserId, canReviewReturns) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/return-review`, { canReviewReturns }),

  // Set individual duty (returns, incidents, support)
  setDuty: (eventId, staffUserId, dutyType, enabled = true) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/duty/${dutyType}?enabled=${enabled}`),

  // Update all duties at once
  updateDuties: (eventId, staffUserId, duties) =>
    axiosClient.put(`/events/${eventId}/staff/${staffUserId}/duties`, duties),

  // UC_14.4: remove a staff member from the event.
  unassign: (eventId, staffUserId) => axiosClient.delete(`/events/${eventId}/staff/${staffUserId}`),

  // Events & gates assigned to this staff member
  getMyAssignments: (staffUserId) => axiosClient.get(`/events/staff/${staffUserId}`),
};

export default staffApi;
