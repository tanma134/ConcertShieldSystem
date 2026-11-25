import React, { useState, useEffect, useRef } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import notificationApi from "../api/notificationApi";
import "./AdminNotificationBell.css";

export default function AdminNotificationBell() {
  const [open, setOpen] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const dropdownRef = useRef(null);
  const navigate = useNavigate();
  const { user, isAuthenticated } = useAuth();

  const currentUid = user?.userId ?? user?.id;

  const fetchNotifications = () => {
    if (!isAuthenticated) return;

    notificationApi
      .getNotifications()
      .then((res) => {
        const rawItems = res.data?.items || res.data?.Items || res.data?.data?.items || res.data?.data?.Items || [];
        let items = rawItems.map((n) => ({
          notificationId: n.notificationId ?? n.NotificationId ?? n.id ?? 0,
          title: n.title || n.Title || "",
          message: n.message || n.Message || n.content || "",
          category: n.category || n.Category || "admin_alert",
          targetUrl: n.targetUrl || n.TargetUrl || "",
          isRead: n.isRead ?? n.IsRead ?? false,
          createdAt: n.createdAt || n.CreatedAt,
        }));

        // Merge locally stored notifications for this user
        if (currentUid) {
          try {
            const userNotifsKey = `cs_user_notifs_${currentUid}`;
            const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
            let localChanged = false;
            for (const item of localSaved) {
              const matched = items.find((n) => n.title === item.title && n.message === item.message);
              if (!matched) {
                items.unshift(item);
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

        setNotifications(items);
        setUnreadCount(items.filter((n) => !n.isRead).length);
      })
      .catch(() => {
        if (currentUid) {
          try {
            const localSaved = JSON.parse(localStorage.getItem(`cs_user_notifs_${currentUid}`) || "[]");
            setNotifications(localSaved);
            setUnreadCount(localSaved.filter((n) => !n.isRead).length);
          } catch (_) {}
        }
      });
  };

  useEffect(() => {
    if (!isAuthenticated) return;

    fetchNotifications();
    const intervalId = setInterval(fetchNotifications, 6000);

    const handleUpdate = () => fetchNotifications();
    window.addEventListener("notification-updated", handleUpdate);
    window.addEventListener("focus", handleUpdate);

    const handleStorage = (e) => {
      if (e.key === "cs_latest_notification" || e.key?.startsWith("cs_user_notifs_")) {
        fetchNotifications();
      }
    };
    window.addEventListener("storage", handleStorage);

    let channel = null;
    try {
      channel = new BroadcastChannel("concertshield_notifications");
      channel.onmessage = () => fetchNotifications();
    } catch (_) {}

    return () => {
      clearInterval(intervalId);
      window.removeEventListener("notification-updated", handleUpdate);
      window.removeEventListener("focus", handleUpdate);
      window.removeEventListener("storage", handleStorage);
      if (channel) channel.close();
    };
  }, [isAuthenticated, currentUid]);

  useEffect(() => {
    const handleClickOutside = (e) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target)) {
        setOpen(false);
      }
    };
    const handleEsc = (e) => {
      if (e.key === "Escape") setOpen(false);
    };

    if (open) {
      document.addEventListener("mousedown", handleClickOutside);
      document.addEventListener("keydown", handleEsc);
    }
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEsc);
    };
  }, [open]);

  const handleMarkAllRead = async () => {
    setUnreadCount(0);
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
    if (currentUid) {
      try {
        const userNotifsKey = `cs_user_notifs_${currentUid}`;
        const localSaved = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
        const updatedLocal = localSaved.map((n) => ({ ...n, isRead: true }));
        localStorage.setItem(userNotifsKey, JSON.stringify(updatedLocal));
      } catch (_) {}
    }
    try {
      await notificationApi.markAllAsRead();
    } catch (_) {}
    window.dispatchEvent(new Event("notification-updated"));
  };

  const handleNotificationClick = async (notif) => {
    if (!notif.isRead) {
      setNotifications((prev) =>
        prev.map((n) => (n.notificationId === notif.notificationId ? { ...n, isRead: true } : n))
      );
      setUnreadCount((prev) => Math.max(0, prev - 1));
      if (currentUid) {
        try {
          const userNotifsKey = `cs_user_notifs_${currentUid}`;
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
      } catch (_) {}
      window.dispatchEvent(new Event("notification-updated"));
    }
    setOpen(false);
    if (notif.targetUrl) {
      navigate(notif.targetUrl);
    }
  };

  const handleDeleteNotification = async (e, targetId) => {
    e.stopPropagation();
    const target = notifications.find((n) => n.notificationId === targetId);
    setNotifications((prev) => prev.filter((n) => n.notificationId !== targetId));
    if (target && !target.isRead) {
      setUnreadCount((prev) => Math.max(0, prev - 1));
    }
    try {
      await notificationApi.deleteNotification(targetId);
    } catch (_) {}
    window.dispatchEvent(new Event("notification-updated"));
  };

  return (
    <div className="admin-notif-wrap" ref={dropdownRef}>
      <button
        type="button"
        className="admin-notif-btn"
        aria-haspopup="true"
        aria-expanded={open}
        onClick={() => {
          const next = !open;
          setOpen(next);
          if (next) fetchNotifications();
        }}
        title="Admin Notifications"
      >
        <svg
          viewBox="0 0 24 24"
          width="16"
          height="16"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
          <path d="M13.73 21a2 2 0 0 1-3.46 0" />
        </svg>
        <span>Notifications</span>
        {unreadCount > 0 && (
          <span className="admin-notif-badge">{unreadCount > 99 ? "99+" : unreadCount}</span>
        )}
      </button>

      {open && (
        <div className="admin-notif-dropdown">
          <div className="admin-notif-header">
            <span>
              <strong>Notifications</strong> {unreadCount > 0 && `(${unreadCount} unread)`}
            </span>
            {unreadCount > 0 && (
              <button type="button" className="admin-notif-mark-all" onClick={handleMarkAllRead}>
                Mark all read
              </button>
            )}
          </div>

          <div className="admin-notif-list">
            {notifications.length === 0 ? (
              <div className="admin-notif-empty">No notifications yet.</div>
            ) : (
              notifications.slice(0, 8).map((notif) => (
                <div
                  key={notif.notificationId}
                  className={`admin-notif-item ${!notif.isRead ? "unread" : ""}`}
                  onClick={() => handleNotificationClick(notif)}
                >
                  <div className={`admin-notif-dot ${notif.isRead ? "read" : ""}`} />
                  <div className="admin-notif-content">
                    <div className="admin-notif-title">{notif.title}</div>
                    <div className="admin-notif-msg">{notif.message}</div>
                  </div>
                  <button
                    type="button"
                    className="admin-notif-delete"
                    title="Delete notification"
                    aria-label="Delete notification"
                    onClick={(e) => handleDeleteNotification(e, notif.notificationId)}
                  >
                    ✕
                  </button>
                </div>
              ))
            )}
          </div>

          <div className="admin-notif-footer">
            <Link to="/notifications" onClick={() => setOpen(false)}>
              View all in Notification Center →
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}
