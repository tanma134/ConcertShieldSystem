import axios from "axios";

// NotificationAPI is called directly (bypasses Gateway) because the
// GET /notifications route has no JWT requirement — the controller
// reads the optional user id from the Bearer token itself.
// Direct URL: https://localhost:7197  (NotificationAPI)
// Fallback: https://localhost:7164   (via Gateway)

const NOTIF_DIRECT_URL = "https://localhost:7197/api";
const GATEWAY_NOTIF_URL = "https://localhost:7164/api";

const httpsAgent = (() => {
  // Node-only — browser ignores this, CORS is handled by server
  return undefined;
})();

const makeNotifAxios = (baseURL) =>
  axios.create({
    baseURL,
    headers: { "Content-Type": "application/json" },
    timeout: 5000,
  });

const directClient = makeNotifAxios(NOTIF_DIRECT_URL);
const gatewayClient = makeNotifAxios(GATEWAY_NOTIF_URL);

// Attach JWT from localStorage when available
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

  try {
    // Try NotificationAPI directly first (always running independently)
    const fn = method === "get"
      ? () => directClient.get(path, config)
      : method === "put"
        ? () => directClient.put(path, data, config)
        : method === "delete"
          ? () => directClient.delete(path, config)
          : () => directClient.post(path, data, config);
    return await fn();
  } catch (directErr) {
    // Fallback to Gateway if direct call fails
    try {
      const fn = method === "get"
        ? () => gatewayClient.get(path, config)
        : method === "put"
          ? () => gatewayClient.put(path, data, config)
          : method === "delete"
            ? () => gatewayClient.delete(path, config)
              : () => gatewayClient.post(path, data, config);
      return await fn();
    } catch (gatewayErr) {
      throw gatewayErr;
    }
  }
};

const notificationApi = {
  getNotifications: () => withFallback("/notifications", "get"),
  markAsRead: (id) => withFallback(`/notifications/${id}/read`, "put"),
  markAllAsRead: () => withFallback("/notifications/read-all", "put"),
  deleteNotification: (id) => withFallback(`/notifications/${id}`, "delete"),
};

export default notificationApi;
