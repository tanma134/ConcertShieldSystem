import axiosClient from "./axiosClient";

const queueAdminApi = {
  configure: (eventId, data) => axiosClient.post(`/queue/${eventId}/prequeue/configure`, data),
  getSessions: (eventId) => axiosClient.get("/queue/admin/sessions", { params: eventId ? { eventId } : {} }),
  revoke: (token, reason) => axiosClient.post(`/queue/admin/tokens/${token}/revoke`, { reason }),
  revokeSession: (sessionId, reason) => axiosClient.post(`/queue/admin/sessions/${sessionId}/revoke`, { reason }),
  unrevokeSession: (sessionId) => axiosClient.post(`/queue/admin/sessions/${sessionId}/unrevoke`),
  getAlerts: (params = {}) => axiosClient.get("/queue/admin/abuse-alerts", { params }),
  getAlert: (id) => axiosClient.get(`/queue/admin/abuse-alerts/${id}`),
  flag: (sessionId, reason) => axiosClient.post(`/queue/admin/sessions/${sessionId}/flag`, { reason }),
  block: (sessionId, reason) => axiosClient.post(`/queue/admin/sessions/${sessionId}/block`, { reason }),
  unblock: (sessionId) => axiosClient.post(`/queue/admin/sessions/${sessionId}/unblock`),
};
export default queueAdminApi;
