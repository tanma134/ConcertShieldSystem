import axiosClient from "./axiosClient";

// Quan ly hang ve va han muc ve danh cho Guest va Organizer
const ticketTypeApi = {
  // Lay danh sach hang ve cua concert danh cho Guest va Organizer
  getByEvent: (eventId) => axiosClient.get(`/tickettypes/event/${eventId}`),

  // Tao moi hang ve cho concert danh cho Organizer
  create: (eventId, dto) =>
    axiosClient.post(`/tickettypes/event/${eventId}`, dto),

  // Cap nhat thong tin gia, so luong va han muc dat ve danh cho Organizer
  update: (ticketTypeId, dto) =>
    axiosClient.put(`/tickettypes/${ticketTypeId}`, { ticketTypeId, ...dto }),

  // Xoa hang ve khi chua ban ve nao danh cho Organizer
  remove: (ticketTypeId) => axiosClient.delete(`/tickettypes/${ticketTypeId}`),
};

export default ticketTypeApi;