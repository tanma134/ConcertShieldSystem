import axiosClient from "./axiosClient";
import axios from "axios";

const NOTIFICATION_API_URL =
  import.meta.env.VITE_NOTIFICATION_API_URL || "https://localhost:7197/api";
const GATEWAY_API_URL =
  import.meta.env.VITE_API_BASE_URL || "https://localhost:7164/api";

const makeNotifAxios = (baseURL) =>
  axios.create({
    baseURL,
    headers: { "Content-Type": "application/json" },
    timeout: 5000,
  });

const directClient = makeNotifAxios(NOTIFICATION_API_URL);
const gatewayClient = makeNotifAxios(GATEWAY_API_URL);

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
  } catch {
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
      if (path === "/notifications" && method === "get") {
        try {
          return await axiosClient.get("/organizer-requests/notifications");
        } catch {
          console.warn("Notification fallback request failed.");
        }
      }
      if (path === "/notifications/read-all" && method === "put") {
        try {
          return await axiosClient.put("/organizer-requests/notifications/read-all");
        } catch {
          console.warn("Mark-all-read fallback request failed.");
        }
      }
      if (path.includes("/notifications/") && path.endsWith("/read") && method === "put") {
        try {
          return await axiosClient.put(`/organizer-requests${path}`);
        } catch {
          console.warn("Mark-read fallback request failed.");
        }
      }
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
