import axios from "axios";

// Multi-endpoint pool (direct HTTP 5288, direct HTTPS 7330, gateway 7164) to bypass an offline gateway
const http5288Client = axios.create({
  baseURL: "http://localhost:5288/api",
  headers: { "Content-Type": "application/json" },
  timeout: 4000,
});

const https7330Client = axios.create({
  baseURL: "https://localhost:7330/api",
  headers: { "Content-Type": "application/json" },
  timeout: 4000,
});

const gatewayClient = axios.create({
  baseURL: "https://localhost:7164/api",
  headers: { "Content-Type": "application/json" },
  timeout: 4000,
});

const addAuth = (config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers = config.headers || {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
};

const withFallback = async (path, method = "get", data = null, extraConfig = {}) => {
  const config = addAuth({ ...extraConfig });
  const clients = [http5288Client, https7330Client, gatewayClient];

  let lastErr = null;
  for (const client of clients) {
    try {
      const fn =
        method === "get"
          ? () => client.get(path, config)
          : method === "put"
          ? () => client.put(path, data, config)
          : method === "delete"
          ? () => client.delete(path, config)
          : () => client.post(path, data, config);
      return await fn();
    } catch (err) {
      lastErr = err;
      // If the backend answered with a status code (4xx, 5xx), the server IS REACHABLE!
      // Do not treat it as a connection drop; surface the server's real response immediately.
      if (err.response) {
        throw err;
      }
    }
  }
  throw lastErr;
};

const checkinApi = {
  getMonitorStatus: (eventId, params = {}) => {
    const queryParams = new URLSearchParams({ eventId, ...params }).toString();
    return withFallback(`/checkin/monitor?${queryParams}`, "get");
  },

  // UC_15.1 / UC-54: View Gate Assignment
  getGateAssignment: (eventId = 1, staffUserId = null) => {
    const params = new URLSearchParams({ eventId });
    if (staffUserId) params.append("staffUserId", staffUserId);
    return withFallback(`/checkin/gate-assignment?${params.toString()}`, "get");
  },

  scanQrTicket: (payload) => {
    return withFallback("/checkin/scan-qr", "post", payload);
  },

  manualCheckin: (payload) => {
    return withFallback("/checkin/manual-checkin", "post", payload);
  },

  lookupTickets: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/lookup-ticket?${queryParams}`, "get");
  },

  getScanHistory: (eventId = 1, params = {}) => {
    const queryParams = new URLSearchParams({ eventId, ...params }).toString();
    return withFallback(`/checkin/scan-history?${queryParams}`, "get");
  },

  bulkRevokeTickets: (payload) => {
    return withFallback("/checkin/governance/bulk-revoke", "post", payload);
  },

  getRevocationHistory: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/governance/revocations?${queryParams}`, "get");
  },

  getReissueHistory: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/governance/reissues?${queryParams}`, "get");
  },

  getReconciliationRecords: (eventId) => {
    const path = eventId ? `/checkin/governance/reconciliations?eventId=${eventId}` : "/checkin/governance/reconciliations";
    return withFallback(path, "get");
  },

  getGovernanceStats: () => {
    return withFallback("/checkin/governance/stats", "get");
  },

  getGovernanceEvents: () => {
    return withFallback("/checkin/governance/events", "get");
  },

  // ==========================================
  // NHÓM 2: Offline Check-in
  // ==========================================
  // Enable Offline Check-in Mode (Preload Ticket Package)
  getOfflinePackage: (eventId = 1, gateName = "") => {
    const params = new URLSearchParams({ eventId });
    if (gateName) params.append("gateName", gateName);
    return withFallback(`/checkin/offline-package?${params.toString()}`, "get");
  },

  syncOfflineScans: (payload) => {
    return withFallback("/checkin/offline-sync", "post", payload);
  },

  getOfflineConflicts: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/conflicts?${queryParams}`, "get");
  },

  resolveOfflineConflict: (conflictId, payload) => {
    return withFallback(`/checkin/conflicts/${conflictId}/resolve`, "post", payload);
  },
};

export default checkinApi;
