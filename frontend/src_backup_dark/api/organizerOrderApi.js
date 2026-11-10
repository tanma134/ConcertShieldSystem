import axiosClient from "./axiosClient";

const organizerOrderApi = {
  getByEvent: (eventId, params = {}) =>
    axiosClient.get(`/organizer/events/${eventId}/orders`, { params }),
};

export default organizerOrderApi;