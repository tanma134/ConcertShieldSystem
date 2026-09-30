import axiosClient from "./axiosClient";

const ticketApi = {
  createHoldSession: (dto) => axiosClient.post("/Holds/session", dto),
  getHoldDetails: (holdId) => axiosClient.get(`/Holds/session/${holdId}`),
  createOrder: (dto) => axiosClient.post("/Orders/create", dto),
  getBookedSeatIds: (eventId) =>
    axiosClient.get(`/Orders/events/${eventId}/booked-seats`),
  getHeldSeatIds: (eventId, seatIds) =>
    axiosClient.post(`/Holds/events/${eventId}/held-seats`, seatIds),
  saveHoldAttendees: (holdId, attendees) =>
    axiosClient.put(`/Holds/session/${holdId}/attendees`, { attendees }),
  getOrderById: (orderId) =>
    axiosClient.get(`/Orders/${encodeURIComponent(orderId)}`),
};

export default ticketApi;
