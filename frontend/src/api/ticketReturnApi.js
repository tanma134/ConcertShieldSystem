import axiosClient from "./axiosClient";

const ticketReturnApi = {
  getMyTickets: () => axiosClient.get("/tickets/my"),

  submit: (ticketId, reason, bankAccountName, bankAccountNumber, bankName, proofUrl = null) =>
    axiosClient.post("/ticket-returns", { ticketId, reason, bankAccountName, bankAccountNumber, bankName, proofUrl }),

  list: (status) => axiosClient.get("/ticket-returns", { params: status ? { status } : {} }),

  getById: (id) => axiosClient.get(`/ticket-returns/${id}`),

  cancel: (id) => axiosClient.post(`/ticket-returns/${id}/cancel`),

  reviewQueue: (status = "Pending") => axiosClient.get("/ticket-returns/review", { params: status ? { status } : {} }),

  confirmManualRefund: (id, payload) => axiosClient.post(`/ticket-returns/${id}/confirm-manual-refund`, payload),
  verifyRefundAccount: (id, payload) => axiosClient.post(`/ticket-returns/${id}/verify-refund-account`, payload),

  review: (id, approve, reviewNote = "") => axiosClient.post(`/ticket-returns/${id}/review`, { approve, reviewNote }),
};

export default ticketReturnApi;
