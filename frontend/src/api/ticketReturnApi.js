import axiosClient from "./axiosClient";

// ticket return requests of the signed-in customer.
const ticketReturnApi = {
  // "My tickets" list, each ticket carries whether it can be returned right now.
  getMyTickets: () => axiosClient.get("/tickets/my"),

  // submit a return request for one ticket.
  submit: (ticketId, reason, proofUrl = null) =>
    axiosClient.post("/ticket-returns", { ticketId, reason, proofUrl }),

  // Hoàn tiền theo lô cho nhiều vé trong cùng một đơn hàng
  submitBatch: (ticketIds, reason, proofUrl = null) =>
    axiosClient.post("/ticket-returns/batch", { ticketIds, reason, proofUrl }),

  // all my return requests, optionally filtered by status.
  list: (status) => axiosClient.get("/ticket-returns", { params: status ? { status } : {} }),

  // one return request.
  getById: (id) => axiosClient.get(`/ticket-returns/${id}`),

  // withdraw a request that is still pending.
  cancel: (id) => axiosClient.post(`/ticket-returns/${id}/cancel`),
  reviewQueue: (status = "Pending") => axiosClient.get("/ticket-returns/review", { params: status ? { status } : {} }),
  retryRefund: (id) => axiosClient.post(`/ticket-returns/${id}/retry-refund`),
  review: (id, approve, reviewNote = "") => axiosClient.post(`/ticket-returns/${id}/review`, { approve, reviewNote }),
};

export default ticketReturnApi;
