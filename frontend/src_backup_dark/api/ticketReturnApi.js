import axiosClient from "./axiosClient";

// UC_9: ticket return requests of the signed-in customer.
const ticketReturnApi = {
  // "My tickets" list, each ticket carries whether it can be returned right now.
  getMyTickets: () => axiosClient.get("/tickets/my"),

  // UC_9.1: submit a return request for one ticket.
  submit: (ticketId, reason) => axiosClient.post("/ticket-returns", { ticketId, reason }),

  // UC_9.2: all my return requests, optionally filtered by status.
  list: (status) => axiosClient.get("/ticket-returns", { params: status ? { status } : {} }),

  // UC_9.2: one return request.
  getById: (id) => axiosClient.get(`/ticket-returns/${id}`),

  // UC_9.3: withdraw a request that is still pending.
  cancel: (id) => axiosClient.post(`/ticket-returns/${id}/cancel`),
  reviewQueue: (status = "Pending") => axiosClient.get("/ticket-returns/review", { params: status ? { status } : {} }),
  retryRefund: (id) => axiosClient.post(`/ticket-returns/${id}/retry-refund`),
  review: (id, approve, reviewNote = "") => axiosClient.post(`/ticket-returns/${id}/review`, { approve, reviewNote }),
};

export default ticketReturnApi;
