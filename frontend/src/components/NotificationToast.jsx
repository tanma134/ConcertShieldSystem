import { useEffect, useState, useRef, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import notificationApi from "../api/notificationApi";
import { useAuth } from "../context/AuthContext";
import "./NotificationToast.css";

export default function NotificationToast() {
  const navigate = useNavigate();
  const { isAuthenticated, isAdmin } = useAuth();

  // Keep refs so callbacks always see fresh values (avoids stale closure)
  const isAdminRef = useRef(isAdmin);
  const isAuthRef = useRef(isAuthenticated);
  useEffect(() => { isAdminRef.current = isAdmin; }, [isAdmin]);
  useEffect(() => { isAuthRef.current = isAuthenticated; }, [isAuthenticated]);

  const [toasts, setToasts] = useState([]);
  const seenIdsRef = useRef(new Set());
  const recentTitlesRef = useRef(new Map());
  const initialLoadDone = useRef(false);
  const timeoutMap = useRef({});

  const removeToast = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    if (timeoutMap.current[id]) {
      clearTimeout(timeoutMap.current[id]);
      delete timeoutMap.current[id];
    }
  }, []);

  const extractKey = (title) => {
    if (!title) return "";
    const parts = String(title).split(":");
    return (parts.length > 1 ? parts[parts.length - 1] : String(title)).trim().toUpperCase();
  };

  const addToast = useCallback((toast) => {
    if (isAdminRef.current || !toast?.title) return; // Never show toasts to Admin

    const dedupeKey = extractKey(toast.title);
    const now = Date.now();

    // ── STRICT DEDUPLICATION ───────────────────────────────────────────────
    // Drop toast if a notification for the same code/subject was processed in the last 15s
    const lastTime = recentTitlesRef.current.get(dedupeKey) || 0;
    if (now - lastTime < 15000) {
      return;
    }
    recentTitlesRef.current.set(dedupeKey, now);

    // Clean up old title entries older than 30s
    recentTitlesRef.current.forEach((time, key) => {
      if (now - time > 30000) recentTitlesRef.current.delete(key);
    });

    const id = `${now}_${Math.random()}`;
    const newToast = { id, ...toast };

    setToasts((prev) => {
      // Don't duplicate card in current visible list
      if (prev.some((t) => extractKey(t.title) === dedupeKey)) {
        return prev;
      }
      return [newToast, ...prev].slice(0, 3);
    });

    // Auto-dismiss after 7s
    timeoutMap.current[id] = setTimeout(() => {
      removeToast(id);
    }, 7000);
  }, [removeToast]);

  // ─── Listener: custom window event (same-tab immediate dispatch) ──────────
  useEffect(() => {
    if (!isAuthenticated || isAdmin) return;

    const onToastEvent = (e) => {
      if (e.detail) addToast(e.detail);
    };
    window.addEventListener("show-notification-toast", onToastEvent);
    return () => window.removeEventListener("show-notification-toast", onToastEvent);
  }, [isAuthenticated, isAdmin, addToast]);

  // ─── Listener: cross-tab BroadcastChannel & storage ────────────────────────
  useEffect(() => {
    if (!isAuthenticated || isAdmin) return;

    let bc = null;
    try {
      bc = new BroadcastChannel("concertshield_notifications");
      bc.onmessage = (e) => {
        if (e.data) {
          addToast(e.data);
          window.dispatchEvent(new CustomEvent("notification-updated"));
        }
      };
    } catch (_) {}

    const onStorage = (e) => {
      if (e.key === "cs_latest_notification" && e.newValue) {
        try { addToast(JSON.parse(e.newValue)); } catch (_) {}
      }
    };
    window.addEventListener("storage", onStorage);

    return () => {
      window.removeEventListener("storage", onStorage);
      if (bc) bc.close();
    };
  }, [isAuthenticated, isAdmin, addToast]);

  // ─── Polling: fetch API every 5s ──────────────────────────────────────────
  useEffect(() => {
    if (!isAuthenticated || isAdmin) return;

    let alive = true;

    const poll = async () => {
      if (!alive) return;
      try {
        const res = await notificationApi.getNotifications();
        if (!alive) return;

        const items =
          res.data?.items ||
          res.data?.Items ||
          res.data?.data?.items ||
          res.data?.data?.Items ||
          [];

        if (!initialLoadDone.current) {
          // On first load: show ONE toast for the most recent unread (last 2h)
          initialLoadDone.current = true;
          const now = Date.now();
          const recent = items.filter((n) => {
            const read = n.isRead ?? n.IsRead ?? false;
            const t = n.createdAt ? new Date(n.createdAt).getTime() : 0;
            return !read && now - t < 2 * 60 * 60 * 1000;
          });

          // Mark all items as seen ID-wise
          items.forEach((n) => {
            const nid = n.notificationId ?? n.NotificationId ?? n.id;
            if (nid) seenIdsRef.current.add(nid);
          });

          if (recent.length > 0) {
            const n = recent[0];
            addToast({
              title: n.title || n.Title || "New Notification",
              message: n.message || n.Message || "",
              category: n.category || n.Category || "general",
              targetUrl: n.targetUrl || n.TargetUrl || "/",
            });
          }
          return;
        }

        // Subsequent polls: toast for any new unseen+unread notification
        items.forEach((n) => {
          const nid = n.notificationId ?? n.NotificationId ?? n.id;
          const read = n.isRead ?? n.IsRead ?? false;
          if (nid && !seenIdsRef.current.has(nid)) {
            seenIdsRef.current.add(nid);
            if (!read) {
              addToast({
                title: n.title || n.Title || "New Notification",
                message: n.message || n.Message || "",
                category: n.category || n.Category || "general",
                targetUrl: n.targetUrl || n.TargetUrl || "/",
              });
              window.dispatchEvent(new CustomEvent("notification-updated"));
            }
          }
        });
      } catch (err) {
        // Silent
      }
    };

    poll();
    const intervalId = setInterval(poll, 5000);
    return () => {
      alive = false;
      clearInterval(intervalId);
    };
  }, [isAuthenticated, isAdmin, addToast]);

  // Reset state on logout
  useEffect(() => {
    if (!isAuthenticated) {
      seenIdsRef.current = new Set();
      recentTitlesRef.current = new Map();
      initialLoadDone.current = false;
    }
  }, [isAuthenticated]);

  if (!isAuthenticated || isAdmin || toasts.length === 0) return null;

  return (
    <div className="tb-toast-container">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className="tb-toast-card"
          onClick={() => {
            if (toast.targetUrl) navigate(toast.targetUrl);
            removeToast(toast.id);
          }}
        >
          <div className="tb-toast-icon-wrap">🔔</div>
          <div className="tb-toast-body">
            <div className="tb-toast-header">
              <span className="tb-toast-title">{toast.title}</span>
              <span className="tb-toast-tag">
                {toast.category === "voucher_new" ? "Voucher" : "Alert"}
              </span>
            </div>
            <p className="tb-toast-desc">{toast.message}</p>
          </div>
          <button
            type="button"
            className="tb-toast-close"
            onClick={(e) => {
              e.stopPropagation();
              removeToast(toast.id);
            }}
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  );
}
