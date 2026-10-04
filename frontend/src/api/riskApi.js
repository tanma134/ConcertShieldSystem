import axiosClient from "./axiosClient";

// Risk-Based Anti-Fraud (AdminAPI: /api/admin/fraud-alerts/*, /api/admin/risk/blocks/*)
// <input type="date"> gives "YYYY-MM-DD"; as "to" that means 00:00 and would hide the whole last day.
const endOfDay = (d) => (d && /^\d{4}-\d{2}-\d{2}$/.test(d) ? `${d}T23:59:59.999` : d || undefined);

const riskApi = {
  // UC_18.1 View Fraud Alert Dashboard
  getSummary: (query = {}) =>
    axiosClient.get("/admin/fraud-alerts/summary", {
      params: {
        from: query.from || undefined,
        to: endOfDay(query.to),
      },
    }),

  // UC_18.1 list + filters
  search: (query = {}) =>
    axiosClient.get("/admin/fraud-alerts", {
      params: {
        riskLevel: query.riskLevel || undefined,
        alertType: query.alertType || undefined,
        status: query.status || undefined,
        userId: query.userId || undefined,
        from: query.from || undefined,
        to: endOfDay(query.to),
        page: query.page || 1,
        pageSize: query.pageSize || 20,
      },
    }),

  // UC_18.2 View Fraud Alert Details
  getDetail: (id) => axiosClient.get(`/admin/fraud-alerts/${id}`),

  // Tạo / làm mới bản tóm tắt AI cho một alert
  generateAiSummary: (id) => axiosClient.post(`/admin/fraud-alerts/${id}/ai-summary`),

  // Danh sách block (để tìm riskDecisionId cần Restore)
  searchBlocks: (query = {}) =>
    axiosClient.get("/admin/risk/blocks", {
      params: {
        status: query.status || undefined,
        scope: query.scope || undefined,
        userId: query.userId || undefined,
        fraudAlertId: query.fraudAlertId || undefined,
        page: query.page || 1,
        pageSize: query.pageSize || 20,
      },
    }),

  // UC_18.3 Block Account/Ticket
  createBlock: (payload) => axiosClient.post("/admin/risk/blocks", payload),

  // UC_18.4 Restore Account/Ticket
  restoreBlock: (id, reason) =>
    axiosClient.post(`/admin/risk/blocks/${id}/restore`, { reason }),
};

export default riskApi;
