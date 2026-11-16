import axiosClient from "./axiosClient";

const seatingApi = {
  getByEvent: (eventId) => axiosClient.get(`/seating/event/${eventId}`),
  getPreview: (eventId) => axiosClient.get(`/seating/event/${eventId}/preview`),
  getZoneSeats: (seatZoneId, availableOnly = false) =>
    axiosClient.get(`/seating/zones/${seatZoneId}/seats`, {
      params: { availableOnly },
    }),

  // Creates or replaces the whole layout.
  build: (eventId, dto) => axiosClient.post(`/seating/event/${eventId}`, dto),

  // Updates layout metadata (stage shape, FOH technical booth, dimensions).
  updateLayout: (eventId, layoutJson) =>
    axiosClient.put(`/seating/event/${eventId}/layout`, {
      layoutJson:
        typeof layoutJson === "string" ? layoutJson : JSON.stringify(layoutJson),
    }),

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
