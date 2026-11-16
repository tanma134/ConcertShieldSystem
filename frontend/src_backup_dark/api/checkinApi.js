import axios from "axios";

// Direct CheckinAPI URL (bypasses gateway if gateway is offline)
// Direct HTTP: http://localhost:5288 (CheckinAPI)
// Direct HTTPS: https://localhost:7330
// Gateway: https://localhost:7164/api

// Multi-endpoint client pool: supports HTTP directly (5288), HTTPS directly (7330), and Gateway (7164)
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
  // UC_29: Monitor Real-time Check-in Status
  getMonitorStatus: (eventId, params = {}) => {
    const queryParams = new URLSearchParams({ eventId, ...params }).toString();
    return withFallback(`/checkin/monitor?${queryParams}`, "get");
  },

  // UC_15.1 / UC-54: View Gate Assignment
  getGateAssignment: (eventId = 1) => {
    return withFallback(`/checkin/gate-assignment?eventId=${eventId}`, "get");
  },

  // UC_15.2 / UC-55: Scan Dynamic QR Ticket
  scanQrTicket: (payload) => {
    return withFallback("/checkin/scan-qr", "post", payload);
  },

  // UC_15.3 / UC-56: Manual Check-in
  manualCheckin: (payload) => {
    return withFallback("/checkin/manual-checkin", "post", payload);
  },

  // UC_15.4 / UC-56.1: Lookup Ticket
  lookupTickets: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/lookup-ticket?${queryParams}`, "get");
  },

  // UC_15.5 / UC-57: View Check-in Scan History
  getScanHistory: (eventId = 1, params = {}) => {
    const queryParams = new URLSearchParams({ eventId, ...params }).toString();
    return withFallback(`/checkin/scan-history?${queryParams}`, "get");
  },

  // ==========================================
  // NHÓM 1: Ticket Governance (UC_8.7, UC_8.8, UC_8.10)
  // ==========================================
  // UC_8.7: Bulk Revoke Tickets (event change)
  bulkRevokeTickets: (payload) => {
    return withFallback("/checkin/governance/bulk-revoke", "post", payload);
  },

  // UC_8.8: View Ticket Revocation History
  getRevocationHistory: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/governance/revocations?${queryParams}`, "get");
  },

  // UC_8.10: View Reissue History (1-1 Traceability)
  getReissueHistory: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/governance/reissues?${queryParams}`, "get");
  },

  // Reconciliation Records
  getReconciliationRecords: (eventId) => {
    const path = eventId ? `/checkin/governance/reconciliations?eventId=${eventId}` : "/checkin/governance/reconciliations";
    return withFallback(path, "get");
  },

  // Governance Stats
  getGovernanceStats: () => {
    return withFallback("/checkin/governance/stats", "get");
  },

  // Governance Events list
  getGovernanceEvents: () => {
    return withFallback("/checkin/governance/events", "get");
  },

  // ==========================================
  // NHÓM 2: Offline Check-in (UC_14.5, UC_14.6)
  // ==========================================
  // UC_14.5: Enable Offline Check-in Mode (Preload Ticket Package)
  getOfflinePackage: (eventId = 1) => {
    return withFallback(`/checkin/offline-package?eventId=${eventId}`, "get");
  },

  // UC_14.6: Sync Offline Check-in Records
  syncOfflineScans: (payload) => {
    return withFallback("/checkin/offline-sync", "post", payload);
  },

  // UC_14.7: Resolve Offline Sync Conflicts
  getOfflineConflicts: (params = {}) => {
    const queryParams = new URLSearchParams(params).toString();
    return withFallback(`/checkin/conflicts?${queryParams}`, "get");
  },

  resolveOfflineConflict: (conflictId, payload) => {
    return withFallback(`/checkin/conflicts/${conflictId}/resolve`, "post", payload);
  },
};

export default checkinApi;
