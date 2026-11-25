import axiosClient from "./axiosClient";

const ticketTypeApi = {
  getByEvent: (eventId) => axiosClient.get(`/tickettypes/event/${eventId}`),

  create: (eventId, dto) =>
    axiosClient.post(`/tickettypes/event/${eventId}`, dto),

  update: (ticketTypeId, dto) =>
    axiosClient.put(`/tickettypes/${ticketTypeId}`, { ticketTypeId, ...dto }),

  remove: (ticketTypeId) => axiosClient.delete(`/tickettypes/${ticketTypeId}`),
};

export default ticketTypeApi;