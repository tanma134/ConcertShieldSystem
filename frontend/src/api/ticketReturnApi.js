import axiosClient from "./axiosClient";

// Quan ly yeu cau hoan tra ve va xet duyet hoan tien danh cho Customer, Staff va Admin
const ticketReturnApi = {
  // Lay danh sach ve cua toi danh cho Customer
  getMyTickets: () => axiosClient.get("/tickets/my"),

  // Gui yeu cau hoan tra 1 ve danh cho Customer
  submit: (ticketId, reason, proofUrl = null) =>
    axiosClient.post("/ticket-returns", { ticketId, reason, proofUrl }),

  // Gui yeu cau hoan tra theo lo cho nhieu ve trong don hang danh cho Customer
  submitBatch: (ticketIds, reason, proofUrl = null) =>
    axiosClient.post("/ticket-returns/batch", { ticketIds, reason, proofUrl }),

  // Lay danh sach cac yeu cau hoan ve cua toi danh cho Customer
  list: (status) => axiosClient.get("/ticket-returns", { params: status ? { status } : {} }),

  // Xem chi tiet mot yeu cau hoan ve theo id danh cho Customer
  getById: (id) => axiosClient.get(`/ticket-returns/${id}`),

  // Huy yeu cau hoan ve dang cho duyet danh cho Customer
  cancel: (id) => axiosClient.post(`/ticket-returns/${id}/cancel`),

  // Lay danh sach hang cho duyet hoan ve danh cho Staff va Admin
  reviewQueue: (status = "Pending") => axiosClient.get("/ticket-returns/review", { params: status ? { status } : {} }),

  // Thu lai yeu cau hoan tien bi loi danh cho Admin
  retryRefund: (id) => axiosClient.post(`/ticket-returns/${id}/retry-refund`),

  // Phe duyet hoac tu choi yeu cau hoan ve danh cho Staff va Admin
  review: (id, approve, reviewNote = "") => axiosClient.post(`/ticket-returns/${id}/review`, { approve, reviewNote }),
};

export default ticketReturnApi;
