import axiosClient from "./axiosClient";

const seatingApi = {
  getByEvent: (eventId) => axiosClient.get(`/seating/event/${eventId}`),
  getPreview: (eventId) => axiosClient.get(`/seating/event/${eventId}/preview`),
  // Returns physical seat definitions only; booking availability belongs to Booking/Queue.
  getZoneSeats: (seatZoneId) => axiosClient.get(`/seating/zones/${seatZoneId}/seats`),

  // Creates or replaces the whole layout.
  build: (eventId, dto) => axiosClient.post(`/seating/event/${eventId}`, dto),

  addZone: (eventId, dto) =>
    axiosClient.post(`/seating/event/${eventId}/zones`, dto),
  updateZone: (seatZoneId, dto) =>
    axiosClient.put(`/seating/zones/${seatZoneId}`, dto),
  removeZone: (seatZoneId) =>
    axiosClient.delete(`/seating/zones/${seatZoneId}`),

  // Deletes the whole layout - the concert goes back to general admission.
  remove: (eventId) => axiosClient.delete(`/seating/event/${eventId}`),
};

export default seatingApi;
