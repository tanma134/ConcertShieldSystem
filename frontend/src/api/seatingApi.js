import axiosClient from "./axiosClient";

const seatingApi = {
  getByEvent: (eventId) => axiosClient.get(`/seating/event/${eventId}`),

  getPreview: (eventId) => axiosClient.get(`/seating/event/${eventId}/preview`),

  getZoneSeats: (seatZoneId, availableOnly = false) =>
    axiosClient.get(`/seating/zones/${seatZoneId}/seats`, {
      params: { availableOnly },
    }),

  build: (eventId, dto) => axiosClient.post(`/seating/event/${eventId}`, dto),

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

  remove: (eventId) => axiosClient.delete(`/seating/event/${eventId}`),
};

export default seatingApi;
