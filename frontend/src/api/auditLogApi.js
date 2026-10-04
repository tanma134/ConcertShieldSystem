import axiosClient from "./axiosClient";

// Admin audit log endpoints (AdminAPI: /api/admin/audit-logs/*)
const auditLogApi = {
  // 19.1 View Activity Log List
  search: (query = {}) =>
    axiosClient.get("/admin/audit-logs", {
      params: {
        userId: query.userId || undefined,
        actorType: query.actorType || undefined,
        action: query.action || undefined,
        entityType: query.entityType || undefined,
        entityId: query.entityId || undefined,
        from: query.from || undefined,
        to: query.to || undefined,
        page: query.page || 1,
        pageSize: query.pageSize || 20,
      },
    }),

  // 19.2 Export Audit Log. Needs a blob response so the browser can save the file
  // (from/to are required by the backend to keep exports bounded).
  export: (query = {}, format = "csv") =>
    axiosClient.get("/admin/audit-logs/export", {
      responseType: "blob",
      params: {
        userId: query.userId || undefined,
        actorType: query.actorType || undefined,
        action: query.action || undefined,
        entityType: query.entityType || undefined,
        entityId: query.entityId || undefined,
        from: query.from || undefined,
        to: query.to || undefined,
        format,
      },
    }),
};

export default auditLogApi;
