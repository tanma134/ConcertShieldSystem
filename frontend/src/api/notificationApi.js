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

// Helper to get current logged-in user ID from localStorage
const getCurrentUserId = () => {
  try {
    const rawUser = localStorage.getItem("user");
    if (rawUser) {
      const parsed = JSON.parse(rawUser);
      const id = parsed?.userId ?? parsed?.id ?? parsed?.UserId;
      if (id) return Number(id);
    }
  } catch (_) {}
  return 0;
};

// Local persistent read-state helpers (ensures read/mark-all-read persists across reloads)
const getReadIdsSet = (uid) => {
  try {
    const arr = JSON.parse(localStorage.getItem(`cs_read_notif_ids_${uid}`) || "[]");
    return new Set(Array.isArray(arr) ? arr.map(String) : []);
  } catch (_) {
    return new Set();
  }
};

const saveReadIdsSet = (uid, setObj) => {
  try {
    localStorage.setItem(`cs_read_notif_ids_${uid}`, JSON.stringify(Array.from(setObj)));
  } catch (_) {}
};

const getReadKeysSet = (uid) => {
  try {
    const arr = JSON.parse(localStorage.getItem(`cs_read_notif_keys_${uid}`) || "[]");
    return new Set(Array.isArray(arr) ? arr : []);
  } catch (_) {
    return new Set();
  }
};

const saveReadKeysSet = (uid, setObj) => {
  try {
    localStorage.setItem(`cs_read_notif_keys_${uid}`, JSON.stringify(Array.from(setObj)));
  } catch (_) {}
};

const getDeletedIdsSet = (uid) => {
  try {
    const arr = JSON.parse(localStorage.getItem(`cs_deleted_notif_ids_${uid}`) || "[]");
    return new Set(Array.isArray(arr) ? arr.map(String) : []);
  } catch (_) {
    return new Set();
  }
};

const saveDeletedIdsSet = (uid, setObj) => {
  try {
    localStorage.setItem(`cs_deleted_notif_ids_${uid}`, JSON.stringify(Array.from(setObj)));
  } catch (_) {}
};

// Keep a map of notificationId -> title::message from the most recent fetch
let lastFetchedMap = new Map();

const markLocalReadById = (id) => {
  const uid = getCurrentUserId();
  const readIds = getReadIdsSet(uid);
  readIds.add(String(id));
  saveReadIdsSet(uid, readIds);

  const key = lastFetchedMap.get(String(id));
  if (key) {
    const readKeys = getReadKeysSet(uid);
    readKeys.add(key);
    saveReadKeysSet(uid, readKeys);
  }

  try {
    const userNotifsKey = `cs_user_notifs_${uid}`;
    const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
    const updated = localSaved.map((n) => {
      const nid = String(n.notificationId ?? n.NotificationId ?? n.id ?? 0);
      const nkey = `${n.title || n.Title || ""}::${n.message || n.Message || ""}`;
      if (nid === String(id) || (key && nkey === key)) {
        return { ...n, isRead: true, IsRead: true };
      }
      return n;
    });
    localStorage.setItem(userNotifsKey, JSON.stringify(updated));
  } catch (_) {}
};

const markLocalAllRead = () => {
  const uid = getCurrentUserId();
  const readIds = getReadIdsSet(uid);
  const readKeys = getReadKeysSet(uid);

  for (const [idStr, keyStr] of lastFetchedMap.entries()) {
    readIds.add(idStr);
    if (keyStr) readKeys.add(keyStr);
  }

  saveReadIdsSet(uid, readIds);
  saveReadKeysSet(uid, readKeys);

  try {
    localStorage.setItem(`cs_read_all_at_${uid}`, String(Date.now()));
    const userNotifsKey = `cs_user_notifs_${uid}`;
    const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
    const updated = localSaved.map((n) => ({ ...n, isRead: true, IsRead: true }));
    localStorage.setItem(userNotifsKey, JSON.stringify(updated));
  } catch (_) {}
};

const markLocalDeletedById = (id) => {
  const uid = getCurrentUserId();
  const delIds = getDeletedIdsSet(uid);
  delIds.add(String(id));
  saveDeletedIdsSet(uid, delIds);
};

const applyLocalReadStateToResponse = (res) => {
  if (!res || !res.data) return res;
  const uid = getCurrentUserId();
  const readIds = getReadIdsSet(uid);
  const readKeys = getReadKeysSet(uid);
  const deletedIds = getDeletedIdsSet(uid);
  const readAllAt = Number(localStorage.getItem(`cs_read_all_at_${uid}`) || "0");

  const container = res.data?.items || res.data?.Items ? res.data : (res.data?.data || res.data);
  const rawItems = container?.items || container?.Items;
  if (!Array.isArray(rawItems)) return res;

  const processed = [];
  for (const item of rawItems) {
    const id = item.notificationId ?? item.NotificationId ?? item.id ?? 0;
    const idStr = String(id);
    if (deletedIds.has(idStr)) continue;

    const title = item.title || item.Title || "";
    const message = item.message || item.Message || item.content || "";
    const key = `${title}::${message}`;
    lastFetchedMap.set(idStr, key);

    const createdMs = item.createdAt || item.CreatedAt ? new Date(item.createdAt || item.CreatedAt).getTime() : 0;
    const isMarkedByReadAll = readAllAt > 0 && (!createdMs || createdMs <= readAllAt + 5000);
    const isRead =
      Boolean(item.isRead ?? item.IsRead) ||
      readIds.has(idStr) ||
      readKeys.has(key) ||
      isMarkedByReadAll;

    if (isRead) {
      readIds.add(idStr);
      readKeys.add(key);
    }

    processed.push({
      ...item,
      isRead,
      IsRead: isRead,
    });
  }

  saveReadIdsSet(uid, readIds);
  saveReadKeysSet(uid, readKeys);

  const unreadCount = processed.filter((x) => !x.isRead).length;
  if (container.items) container.items = processed;
  if (container.Items) container.Items = processed;
  container.unreadCount = unreadCount;
  container.UnreadCount = unreadCount;
  res.data.unreadCount = unreadCount;
  res.data.UnreadCount = unreadCount;

  return res;
};

// Attach JWT from localStorage when available
const addAuth = (config) => {
  const token = localStorage.getItem("accessToken");
  if (token) {
    config.headers = config.headers || {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  const uid = getCurrentUserId();
  if (uid > 0) {
    config.params = { ...(config.params || {}), userId: uid };
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
  getNotifications: async () => {
    const res = await withFallback("/notifications", "get");
    return applyLocalReadStateToResponse(res);
  },
  markAsRead: async (id) => {
    markLocalReadById(id);
    return await withFallback(`/notifications/${id}/read`, "put");
  },
  markAllAsRead: async () => {
    markLocalAllRead();
    return await withFallback("/notifications/read-all", "put");
  },
  deleteNotification: async (id) => {
    markLocalDeletedById(id);
    return await withFallback(`/notifications/${id}`, "delete");
  },
};

export default notificationApi;
