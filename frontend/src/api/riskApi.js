import axiosClient from "./axiosClient";

// <input type="date"> gives "YYYY-MM-DD"; as "to" that means 00:00 and would hide the whole last day.
const endOfDay = (d) => (d && /^\d{4}-\d{2}-\d{2}$/.test(d) ? `${d}T23:59:59.999` : d || undefined);

const riskApi = {
  getSummary: (query = {}) =>
    axiosClient.get("/admin/fraud-alerts/summary", {
      params: {
        from: query.from || undefined,
        to: endOfDay(query.to),
      },
    }),

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

  getDetail: (id) => axiosClient.get(`/admin/fraud-alerts/${id}`),

  generateAiSummary: (id) => axiosClient.post(`/admin/fraud-alerts/${id}/ai-summary`),

  // Used to look up riskDecisionId before restoring a block
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

  createBlock: (payload) => axiosClient.post("/admin/risk/blocks", payload),

  restoreBlock: (id, reason) =>
    axiosClient.post(`/admin/risk/blocks/${id}/restore`, { reason }),

  searchCases: (q = {}) =>
    axiosClient.get("/admin/fraud-cases", {
      params: {
        status: q.status || undefined,
        riskLevel: q.riskLevel || undefined,
        alertType: q.alertType || undefined,
        assignedTo: q.assignedTo || undefined,
        unassigned: q.unassigned ? true : undefined,
        overdueOnly: q.overdueOnly ? true : undefined,
        keyword: q.keyword?.trim() || undefined,
        from: q.from || undefined,
        to: endOfDay(q.to),
        sortBy: q.sortBy || undefined,
        desc: q.desc ?? true,
        page: q.page || 1,
        pageSize: q.pageSize || 20,
      },
    }),

  assignCase: (id, analystId, note) =>
    axiosClient.post(`/admin/fraud-cases/${id}/assign`, { analystId, note: note || undefined }),

  // Assigned staff member proposes an outcome
  recommendCase: (id, outcome, note) =>
    axiosClient.post(`/admin/fraud-cases/${id}/recommend`, { outcome, note }),

  // Admin only; outcome is Confirmed, FalsePositive, or Dismissed
  resolveCase: (id, outcome, note) =>
    axiosClient.post(`/admin/fraud-cases/${id}/resolve`, { outcome, note }),

  getPolicy: (category) =>
    axiosClient.get("/admin/risk/policy", { params: { category: category || undefined } }),
  updatePolicy: (items, reason) =>
    axiosClient.put("/admin/risk/policy", { items, reason }),
};

export default riskApi;
