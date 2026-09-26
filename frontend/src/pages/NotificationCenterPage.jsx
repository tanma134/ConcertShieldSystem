import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import notificationApi from "../api/notificationApi";
import { useAuth } from "../context/AuthContext";
import Header from "../components/Header";
import Footer from "../components/Footer";
import "./organizer/NotificationCenterPage.css";

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
  const { isAuthenticated, isAdmin } = useAuth();
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("all"); // 'all' | 'unread'
  const [deleteConfirmId, setDeleteConfirmId] = useState(null);

  const fetchNotifications = () => {
    setLoading(true);
    notificationApi
      .getNotifications()
      .then((res) => {
        const rawItems = res.data?.items || res.data?.Items || res.data?.data?.items || res.data?.data?.Items || [];
        const rawUnread = res.data?.unreadCount ?? res.data?.UnreadCount ?? res.data?.data?.unreadCount ?? res.data?.data?.UnreadCount ?? 0;
        const normalized = rawItems.map(normalizeNotification);
        setNotifications(normalized);
        setUnreadCount(rawUnread);
      })
      .catch((err) => {
        console.error("Fetch notifications error", err);
      })
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    if (isAuthenticated) {
      fetchNotifications();
      const intervalId = setInterval(fetchNotifications, 10000);
      const handleUpdate = () => fetchNotifications();
      window.addEventListener("notification-updated", handleUpdate);
      return () => {
        clearInterval(intervalId);
        window.removeEventListener("notification-updated", handleUpdate);
      };
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

      try {
        await notificationApi.markAsRead(notif.notificationId);
      } catch (err) {
        console.warn("Background mark read warning:", err?.message);
      }
    }
    if (notif.targetUrl) {
      navigate(notif.targetUrl);
    }
  };

  const handleMarkAllAsRead = async () => {
    setUnreadCount(0);
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));

    try {
      await notificationApi.markAllAsRead();
    } catch (err) {
      console.warn("Background mark all read warning:", err?.message);
    }
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
    <div className="tb-app">
      <Header />
      <div className="tb-container nc-container" style={{ padding: "30px 20px", minHeight: "60vh" }}>
        <div className="nc-header">
          <h1 className="nc-title">
            <span>Trung tâm Thông báo</span>
            {unreadCount > 0 && <span className="nc-unread-badge">{unreadCount} chưa đọc</span>}
          </h1>

          {unreadCount > 0 && (
            <div className="nc-actions">
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                onClick={handleMarkAllAsRead}
              >
                Đánh dấu tất cả đã đọc
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
            Tất cả ({notifications.length})
          </button>
          <button
            type="button"
            className={`nc-tab ${filter === "unread" ? "active" : ""}`}
            onClick={() => setFilter("unread")}
          >
            Chưa đọc ({unreadCount})
          </button>
        </div>

        {loading ? (
          <div className="nc-empty">Đang tải thông báo...</div>
        ) : filteredItems.length === 0 ? (
          <div className="nc-empty">
            <div className="nc-empty-icon">🔔</div>
            <div className="nc-empty-text">Chưa có thông báo nào</div>
          </div>
        ) : (
          <div className="nc-list">
            {filteredItems.map((n) => (
              <div
                key={n.notificationId}
                className={`nc-card ${!n.isRead ? "unread" : ""}`}
                onClick={() => handleCardClick(n)}
                style={{ cursor: "pointer" }}
              >
                <div className="nc-card-icon">
                  <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
                    <path
                      d="M19 3H5C3.9 3 3 3.9 3 5V19C3 20.1 3.9 21 5 21H19C20.1 21 21 20.1 21 19V5C21 3.9 20.1 3 19 3ZM19 19H5V8H19V19ZM7 10H17V12H7V10ZM7 14H14V16H7V14Z"
                      fill="var(--tb-pink, #ec4899)"
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
                  title="Xóa thông báo"
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
                      <p>Bạn có chắc chắn muốn xóa thông báo này?</p>
                      <div className="tb-notif-confirm-actions">
                        <button
                          type="button"
                          className="tb-btn tb-btn-sm tb-btn-danger"
                          onClick={(e) => handleDelete(n.notificationId, e)}
                        >
                          Xóa
                        </button>
                        <button
                          type="button"
                          className="tb-btn tb-btn-sm tb-btn-secondary"
                          onClick={(e) => {
                            e.stopPropagation();
                            setDeleteConfirmId(null);
                          }}
                        >
                          Hủy
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
      <Footer />
    </div>
  );
}
