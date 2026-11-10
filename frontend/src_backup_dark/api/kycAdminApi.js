import axiosClient from "./axiosClient";

// Admin eKYC endpoints (AuthenticationAPI: /api/admin/kyc/*)
const kycAdminApi = {
  // 3.4 Consent versions
  listConsentVersions: () => axiosClient.get("/admin/kyc/consent-versions"),
  getConsentVersion: (id) => axiosClient.get(`/admin/kyc/consent-versions/${id}`),
  createConsentVersion: (payload) => axiosClient.post("/admin/kyc/consent-versions", payload),
  activateConsentVersion: (id) => axiosClient.post(`/admin/kyc/consent-versions/${id}/activate`),
  deleteConsentVersion: (id) => axiosClient.delete(`/admin/kyc/consent-versions/${id}`),

  // 3.6 Deletion requests
  listDeletionRequests: ({ status, page = 1, pageSize = 20 } = {}) =>
    axiosClient.get("/admin/kyc/deletion-requests", {
      params: { status: status || undefined, page, pageSize },
    }),
  approveDeletion: (id) => axiosClient.post(`/admin/kyc/deletion-requests/${id}/approve`),
  rejectDeletion: (id, note) => axiosClient.post(`/admin/kyc/deletion-requests/${id}/reject`, { note }),

  // 3.5 Data retention policy
  getSettings: () => axiosClient.get("/admin/kyc/settings"),
  updateSettings: (payload) => axiosClient.put("/admin/kyc/settings", payload),

  // 3.7 Access log (read-only)
  searchAccessLogs: (query) =>
    axiosClient.get("/admin/kyc/access-logs", {
      params: {
        subjectUserId: query.subjectUserId || undefined,
        actorUserId: query.actorUserId || undefined,
        actorType: query.actorType || undefined,
        action: query.action || undefined,
        from: query.from || undefined,
        to: query.to || undefined,
        page: query.page || 1,
        pageSize: query.pageSize || 20,
      },
    }),

  // Original KYC images. Needs the Bearer token, so it must be fetched as a blob
  // (an <img src> can't send the Authorization header). Every call is logged server-side.
  getImage: (ekycId, kind) =>
    axiosClient.get(`/admin/kyc/${ekycId}/images/${kind}`, { responseType: "blob" }),
};

export default kycAdminApi;