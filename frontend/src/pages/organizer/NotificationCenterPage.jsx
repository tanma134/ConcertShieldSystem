import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import notificationApi from "../api/notificationApi";
import { useAuth } from "../context/AuthContext";
import "./NotificationCenterPage.css";

const normalizeNotification = (n) => ({
  notificationId: n.notificationId ?? n.NotificationId ?? n.id ?? 0,
  userId: n.userId ?? n.UserId ?? 0,
  title: n.title || n.Title || "",
  message: n.message || n.Message || "",
  category: n.category || n.Category || "event_new",
  targetUrl: n.targetUrl || n.TargetUrl || "",
  isRead: n.isRead ?? n.IsRead ?? false,
  readAt: n.readAt || n.ReadAt,
  createdAt: n.createdAt || n.CreatedAt,
});

export default function NotificationCenterPage() {
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [filter, setFilter] = useState("all"); // 'all' | 'unread'
  const [deleteConfirmId, setDeleteConfirmId] = useState(null);

  const fetchNotifications = () => {
    notificationApi
      .getNotifications()
      .then((res) => {
        const rawItems = res.data?.items || res.data?.Items || res.data?.data?.items || res.data?.data?.Items || [];
        let normalized = rawItems.map(normalizeNotification);

        const uid = user?.userId ?? user?.id;
        if (uid) {
          try {
            const userNotifsKey = `cs_user_notifs_${uid}`;
            const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
            let localChanged = false;
            for (const item of localSaved) {
              const matched = normalized.find((n) => n.title === item.title && n.message === item.message);
              if (!matched) {
                normalized.unshift(item);
              } else {
                if (matched.isRead && !item.isRead) {
                  item.isRead = true;
                  localChanged = true;
                } else if (!matched.isRead && item.isRead) {
                  matched.isRead = true;
                }
              }
            }
            if (localChanged) {
              localStorage.setItem(userNotifsKey, JSON.stringify(localSaved));
            }
          } catch (_) {}
        }

        setNotifications(normalized);
        setUnreadCount(normalized.filter((n) => !n.isRead).length);
      })
      .catch((err) => {
        console.warn("Could not fetch remote notifications:", err?.message);
        const uid = user?.userId ?? user?.id;
        if (uid) {
          try {
            const userNotifsKey = `cs_user_notifs_${uid}`;
            const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
            setNotifications(localSaved);
            setUnreadCount(localSaved.filter((n) => !n.isRead).length);
          } catch (_) {}
        }
      });
  };

  useEffect(() => {
    if (isAuthenticated) {
      fetchNotifications();
    } else {
      navigate("/login");
    }
  }, [isAuthenticated, navigate]);

  const handleCardClick = async (notif) => {
    if (!notif.isRead) {
      setNotifications((prev) =>
        prev.map((n) =>
          n.notificationId === notif.notificationId ? { ...n, isRead: true } : n
        )
      );
      setUnreadCount((prev) => Math.max(0, prev - 1));

      const uid = user?.userId ?? user?.id;
      if (uid) {
        try {
          const userNotifsKey = `cs_user_notifs_${uid}`;
          const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
          const updatedLocal = localSaved.map((n) =>
            n.notificationId === notif.notificationId || (n.title === notif.title && n.message === notif.message)
              ? { ...n, isRead: true }
              : n
          );
          localStorage.setItem(userNotifsKey, JSON.stringify(updatedLocal));
        } catch (_) {}
      }

      try {
        await notificationApi.markAsRead(notif.notificationId);
      } catch (err) {
        console.warn("Background mark read warning:", err?.message);
      }
      window.dispatchEvent(new Event("notification-updated"));
    }
    if (notif.targetUrl) {
      navigate(notif.targetUrl);
    }
  };

  const handleMarkAllAsRead = async () => {
    setUnreadCount(0);
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));

    const uid = user?.userId ?? user?.id;
    if (uid) {
      try {
        const userNotifsKey = `cs_user_notifs_${uid}`;
        const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
        const updatedLocal = localSaved.map((n) => ({ ...n, isRead: true }));
        localStorage.setItem(userNotifsKey, JSON.stringify(updatedLocal));
      } catch (_) {}
    }

    try {
      await notificationApi.markAllAsRead();
    } catch (err) {
      console.warn("Background mark all read warning:", err?.message);
    }
    window.dispatchEvent(new Event("notification-updated"));
  };

  const handleDelete = async (id, e) => {
    e.stopPropagation();
    try {
      const res = await notificationApi.deleteNotification(id);
      setNotifications((prev) => prev.filter((n) => n.notificationId !== id));
      if (res.data?.unreadCount !== undefined) {
        setUnreadCount(res.data.unreadCount);
      }
      setDeleteConfirmId(null);
    } catch (err) {
      console.error("Delete notification error", err);
    }
  };

  const formatTime = (dateStr) => {
    if (!dateStr) return "";
    return new Date(dateStr).toLocaleString("vi-VN", {
      hour: "2-digit",
      minute: "2-digit",
      day: "2-digit",
      month: "2-digit",
      year: "numeric",
    });
  };

  const filteredItems = notifications.filter((n) => {
    if (filter === "unread") return !n.isRead;
    return true;
  });

  return (
    <div className="nc-container">
      <div className="nc-header">
        <h1 className="nc-title">
          <span>Notification Center</span>
          {unreadCount > 0 && <span className="nc-unread-badge">{unreadCount} unread</span>}
        </h1>

        {unreadCount > 0 && (
          <div className="nc-actions">
            <button
              type="button"
              className="tb-btn tb-btn-outline"
              onClick={handleMarkAllAsRead}
            >
              Mark all as read
            </button>
          </div>
        )}
      </div>

      <div className="nc-filter-tabs">
        <button
          type="button"
          className={`nc-tab ${filter === "all" ? "active" : ""}`}
          onClick={() => setFilter("all")}
        >
          All ({notifications.length})
        </button>
        <button
          type="button"
          className={`nc-tab ${filter === "unread" ? "active" : ""}`}
          onClick={() => setFilter("unread")}
        >
          Unread ({unreadCount})
        </button>
      </div>

      {loading ? (
        <div className="nc-empty">Loading notifications...</div>
      ) : filteredItems.length === 0 ? (
        <div className="nc-empty">
          <div className="nc-empty-icon">🔔</div>
          <div className="nc-empty-text">No notifications found</div>
        </div>
      ) : (
        <div className="nc-list">
          {filteredItems.map((n) => (
            <div
              key={n.notificationId}
              className={`nc-card ${!n.isRead ? "unread" : ""}`}
              onClick={() => handleCardClick(n)}
            >
              <div className="nc-card-icon">
                <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
                  <path
                    d="M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM19 19H5V8H19V19ZM7 10H17V12H7V10ZM7 14H14V16H7V14Z"
                    fill="var(--tb-pink)"
                  />
                </svg>
              </div>

              <div className="nc-card-body">
                <div className="nc-card-title">{n.title}</div>
                <div className="nc-card-msg">{n.message}</div>
                <div className="nc-card-time">{formatTime(n.createdAt)}</div>
              </div>

              <button
                type="button"
                className="nc-card-delete"
                title="Delete notification"
                onClick={(e) => {
                  e.stopPropagation();
                  setDeleteConfirmId(n.notificationId);
                }}
              >
                &times;
              </button>

              {deleteConfirmId === n.notificationId && (
                <div className="tb-notif-confirm-overlay" onClick={(e) => e.stopPropagation()}>
                  <div className="tb-notif-confirm-box">
                    <p>Are you sure you want to delete this notification?</p>
                    <div className="tb-notif-confirm-actions">
                      <button
                        type="button"
                        className="tb-btn tb-btn-sm tb-btn-danger"
                        onClick={(e) => handleDelete(n.notificationId, e)}
                      >
                        Delete
                      </button>
                      <button
                        type="button"
                        className="tb-btn tb-btn-sm tb-btn-secondary"
                        onClick={(e) => {
                          e.stopPropagation();
                          setDeleteConfirmId(null);
                        }}
                      >
                        Cancel
                      </button>
                    </div>
                  </div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
