import axiosClient from "./axiosClient";

const ticketApi = {
  createHoldSession: (dto, queueToken) =>
    axiosClient.post("/Holds/session", dto, {
      headers: queueToken ? { "X-Queue-Token": queueToken } : {},
    }),
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
  getMyTickets: () => axiosClient.get("/Tickets/my-tickets"),
  rotateTicketQr: (ticketId) =>
    axiosClient.post(`/Orders/tickets/${ticketId}/qr/rotate`),
  getTicketDetails: (ticketId) =>
    axiosClient.get(`/Tickets/${ticketId}`),
  getMyOrders: () => axiosClient.get("/Orders/my-orders"),
  cancelHoldSession: (holdId) =>
    axiosClient.post(
      `/Holds/session/${encodeURIComponent(holdId)}/cancel`
    ),
  scanTicket: (dto) => axiosClient.post("/checkin/scan", dto),
  getTicketHistory: (ticketId) =>
  axiosClient.get(`/Tickets/${ticketId}/history`),
  
  changeHoldSeats: (holdId, seatIds) =>
    axiosClient.put(`/Holds/session/${encodeURIComponent(holdId)}/seats`, {
      seatIds,
    }),

  getAdminTickets: (params) =>
    axiosClient.get("/admin/tickets", { params }),

  getAdminStatusHistories: (params) =>
    axiosClient
      .get("/ticket-returns/status-histories", { params })
      .catch(() => axiosClient.get("/admin/status-histories", { params })),

  getAdminTicketDetails: (ticketId) =>
    axiosClient.get(
      `/admin/tickets/${encodeURIComponent(ticketId)}`
    ),

  revokeTicket: (ticketId, reason) =>
    axiosClient.post(
      `/admin/tickets/${encodeURIComponent(ticketId)}/revoke`,
      { reason }
    ),

  reissueTicket: (ticketId, reason) =>
    axiosClient.post(
      `/admin/tickets/${encodeURIComponent(ticketId)}/reissue`,
      { reason }
    ),

  // Ticket Governance (Bulk Revocation, Traceability & Audit)
  bulkRevokeTickets: (payload) =>
    axiosClient.post("/tickets/governance/bulk-revoke", payload),

  getRevocationHistory: (params = {}) =>
    axiosClient.get("/tickets/governance/revocations", { params }),

  getReissueHistory: (params = {}) =>
    axiosClient.get("/tickets/governance/reissues", { params }),

  getReconciliationRecords: (eventId) =>
    axiosClient.get(eventId ? `/tickets/governance/reconciliations?eventId=${eventId}` : "/tickets/governance/reconciliations"),

  getGovernanceStats: () =>
    axiosClient.get("/tickets/governance/stats"),

  getGovernanceEvents: () =>
    axiosClient.get("/tickets/governance/events"),
    getActiveHoldSession: () => axiosClient.get("/Holds/session/active"),
};

export default ticketApi;
