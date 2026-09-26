import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import eventApi from "../api/eventApi";
import notificationApi from "../api/notificationApi";
import "./Header.css";

// Quick-pick keywords shown under the search box so users can tap instead
const POPULAR_SEARCHES = ["Live Music", "EDM Festival", "K-Pop", "Jazz Night", "Acoustic"];

export default function Header() {
  const navigate = useNavigate();
  const { user, logout, isAuthenticated, isAdmin, isOrganizer } = useAuth();
  const [searchParams] = useSearchParams();
  const [keyword, setKeyword] = useState(searchParams.get("q") || "");
  const [menuOpen, setMenuOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [suggestLoading, setSuggestLoading] = useState(false);

  // Notification Bell Dropdown state
  const [notifOpen, setNotifOpen] = useState(false);
  const [notifications, setNotifications] = useState([]);
  const [unreadCount, setUnreadCount] = useState(0);

  const accountRef = useRef(null);
  const searchRef = useRef(null);
  const notifRef = useRef(null);
  const debounceRef = useRef(null);

  const runSearch = (term) => {
    const trimmed = term.trim();
    setSearchOpen(false);
    navigate(trimmed ? `/?q=${encodeURIComponent(trimmed)}` : "/");
  };

  const handleSearch = (e) => {
    e.preventDefault();
    runSearch(keyword);
  };

  const handlePickSuggestion = (term) => {
    setKeyword(term);
    runSearch(term);
  };

  const handleLogout = () => {
    logout();
    setMenuOpen(false);
    setNotifOpen(false);
    navigate("/");
  };

  const fetchHeaderNotifications = () => {
    if (!isAuthenticated || isAdmin) return;
    notificationApi
      .getNotifications()
      .then((res) => {
        const rawItems = res.data?.items || res.data?.Items || res.data?.data?.items || res.data?.data?.Items || [];
        const rawUnread = res.data?.unreadCount ?? res.data?.UnreadCount ?? res.data?.data?.unreadCount ?? res.data?.data?.UnreadCount ?? 0;
        setNotifications(
          rawItems.map((n) => ({
            notificationId: n.notificationId ?? n.NotificationId ?? n.id ?? 0,
            title: n.title || n.Title || "",
            message: n.message || n.Message || "",
            category: n.category || n.Category || "event_new",
            targetUrl: n.targetUrl || n.TargetUrl || "",
            isRead: n.isRead ?? n.IsRead ?? false,
            createdAt: n.createdAt || n.CreatedAt,
          }))
        );
        setUnreadCount(rawUnread);
      })
      .catch(() => {});
  };

  useEffect(() => {
    if (!isAuthenticated || isAdmin) return;

    fetchHeaderNotifications();

    // Real-time polling every 6 seconds for notifications
    const intervalId = setInterval(fetchHeaderNotifications, 6000);

    const handleUpdate = () => fetchHeaderNotifications();
    window.addEventListener("notification-updated", handleUpdate);
    window.addEventListener("focus", handleUpdate);

    const handleStorage = (e) => {
      if (e.key === "cs_latest_notification") {
        fetchHeaderNotifications();
      }
    };
    window.addEventListener("storage", handleStorage);

    let channel = null;
    try {
      channel = new BroadcastChannel("concertshield_notifications");
      channel.onmessage = () => {
        fetchHeaderNotifications();
      };
    } catch (e) {}

    return () => {
      clearInterval(intervalId);
      window.removeEventListener("notification-updated", handleUpdate);
      window.removeEventListener("focus", handleUpdate);
      window.removeEventListener("storage", handleStorage);
      if (channel) channel.close();
    };
  }, [isAuthenticated, isAdmin]);

  // Live suggestions as the user types (debounced).
  useEffect(() => {
    const trimmed = keyword.trim();
    if (debounceRef.current) clearTimeout(debounceRef.current);

    if (!trimmed) {
      setSuggestions([]);
      setSuggestLoading(false);
      return;
    }

    setSuggestLoading(true);
    debounceRef.current = setTimeout(() => {
      eventApi
        .getList({ search: trimmed, page: 1, pageSize: 5 })
        .then((res) => setSuggestions(res.data?.data?.items || []))
        .catch(() => setSuggestions([]))
        .finally(() => setSuggestLoading(false));
    }, 300);

    return () => clearTimeout(debounceRef.current);
  }, [keyword]);

  // Close dropdowns on outside click or Esc
  useEffect(() => {
    if (!menuOpen && !searchOpen && !notifOpen) return;

    const handleClickOutside = (e) => {
      if (accountRef.current && !accountRef.current.contains(e.target)) {
        setMenuOpen(false);
      }
      if (searchRef.current && !searchRef.current.contains(e.target)) {
        setSearchOpen(false);
      }
      if (notifRef.current && !notifRef.current.contains(e.target)) {
        setNotifOpen(false);
      }
    };
    const handleEsc = (e) => {
      if (e.key === "Escape") {
        setMenuOpen(false);
        setSearchOpen(false);
        setNotifOpen(false);
      }
    };

    document.addEventListener("mousedown", handleClickOutside);
    document.addEventListener("keydown", handleEsc);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
      document.removeEventListener("keydown", handleEsc);
    };
  }, [menuOpen, searchOpen, notifOpen]);

  return (
    <header className="tb-header">
      <div className="tb-header-top tb-container">
        <Link to="/" className="tb-logo">
          <span className="tb-logo-mark">
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <path
                d="M12 2 3 6v6c0 5 3.8 8.7 9 10 5.2-1.3 9-5 9-10V6l-9-4Z"
                fill="url(#tb-shield-grad)"
              />
              <path
                d="M8.5 12.3 11 14.8l4.7-5.4"
                stroke="#fff"
                strokeWidth="1.6"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
              <defs>
                <linearGradient id="tb-shield-grad" x1="3" y1="2" x2="21" y2="22">
                  <stop offset="0" stopColor="#7c3aed" />
                  <stop offset="0.55" stopColor="#c026d3" />
                  <stop offset="1" stopColor="#ec4899" />
                </linearGradient>
              </defs>
            </svg>
          </span>
          Concert<span>Shield</span>
        </Link>

        <div className="tb-search-wrap" ref={searchRef}>
          <form className="tb-search" onSubmit={handleSearch}>
            <svg
              className="tb-search-icon"
              viewBox="0 0 20 20"
              width="16"
              height="16"
              fill="none"
            >
              <circle cx="9" cy="9" r="6.5" stroke="currentColor" strokeWidth="1.6" />
              <path
                d="m18 18-4-4"
                stroke="currentColor"
                strokeWidth="1.6"
                strokeLinecap="round"
              />
            </svg>
            <input
              type="text"
              placeholder="Search concerts, artists..."
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              onFocus={() => setSearchOpen(true)}
            />
            <button type="submit">Search</button>
          </form>

          {searchOpen && (
            <div className="tb-search-dropdown">
              {keyword.trim() && (
                <div className="tb-search-section">
                  <span className="tb-search-section-label">Suggestions</span>
                  {suggestLoading && (
                    <div className="tb-search-hint">Searching...</div>
                  )}
                  {!suggestLoading && suggestions.length === 0 && (
                    <div className="tb-search-hint">No matching events</div>
                  )}
                  {!suggestLoading &&
                    suggestions.map((ev) => (
                      <Link
                        key={ev.eventId}
                        to={`/events/${ev.slug}`}
                        className="tb-search-suggestion"
                        onClick={() => setSearchOpen(false)}
                      >
                        {ev.title}
                      </Link>
                    ))}
                </div>
              )}

              <div className="tb-search-section">
                <span className="tb-search-section-label">Popular searches</span>
                <div className="tb-search-chips">
                  {POPULAR_SEARCHES.map((term) => (
                    <button
                      type="button"
                      key={term}
                      className="tb-search-chip"
                      onClick={() => handlePickSuggestion(term)}
                    >
                      {term}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="tb-header-actions">
          {/* Organizer: Create Event button */}
          {isAuthenticated && !isAdmin && isOrganizer && (
            <button
              type="button"
              className="tb-btn tb-btn-outline tb-create-btn"
              onClick={() => navigate("/organizer/events/new")}
            >
              <svg viewBox="0 0 20 20" width="15" height="15" fill="none">
                <path
                  d="M10 4v12M4 10h12"
                  stroke="currentColor"
                  strokeWidth="1.8"
                  strokeLinecap="round"
                />
              </svg>
              <span>Create Event</span>
            </button>
          )}

          {/* Customer (no organizer role): Become Organizer button */}
          {isAuthenticated && !isAdmin && !isOrganizer && (
            <button
              type="button"
              className="tb-btn tb-btn-outline tb-become-org-btn"
              onClick={() => navigate("/organizer/request")}
            >
              <svg viewBox="0 0 20 20" width="15" height="15" fill="none">
                <path
                  d="M10 3a7 7 0 1 1 0 14A7 7 0 0 1 10 3Zm0 4v3.5l2.5 1.5"
                  stroke="currentColor"
                  strokeWidth="1.7"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              <span>Become Organizer</span>
            </button>
          )}

          {/* Organizer: Dashboard & Vouchers links */}
          {isAuthenticated && !isAdmin && isOrganizer && (
            <>
              <Link to="/organizer/dashboard" className="tb-header-link">
                Dashboard
              </Link>
              <Link to="/organizer/vouchers" className="tb-header-link">
                Vouchers
              </Link>
            </>
          )}

          {/* Notification Bell Dropdown (Customer and Organizer only) */}
          {isAuthenticated && !isAdmin && (
            <div className="tb-notif-wrap" ref={notifRef}>
              <button
                type="button"
                className="tb-header-link tb-notif-btn"
                onClick={() => {
                  const next = !notifOpen;
                  setNotifOpen(next);
                  if (next) fetchHeaderNotifications();
                }}
                title="Notifications"
              >
                🔔 Notifications
                {unreadCount > 0 && (
                  <span className="tb-notif-badge">{unreadCount > 99 ? "99+" : unreadCount}</span>
                )}
              </button>

              {notifOpen && (
                <div className="tb-notif-dropdown">
                  <div className="tb-notif-header">
                    <strong>Notifications</strong>
                    {unreadCount > 0 && (
                      <button
                        type="button"
                        className="tb-notif-mark-all"
                        onClick={async () => {
                          setUnreadCount(0);
                          setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
                          try {
                            await notificationApi.markAllAsRead();
                          } catch (e) {}
                        }}
                      >
                        Mark all read
                      </button>
                    )}
                  </div>

                  <div className="tb-notif-list">
                    {notifications.length === 0 ? (
                      <div className="tb-notif-empty">No new notifications.</div>
                    ) : (
                      notifications.slice(0, 6).map((notif) => (
                        <div
                          key={notif.notificationId}
                          className={`tb-notif-item ${!notif.isRead ? "unread" : ""}`}
                          onClick={async () => {
                            if (!notif.isRead) {
                              setNotifications((prev) =>
                                prev.map((n) =>
                                  n.notificationId === notif.notificationId ? { ...n, isRead: true } : n
                                )
                              );
                              setUnreadCount((prev) => Math.max(0, prev - 1));
                              try {
                                await notificationApi.markAsRead(notif.notificationId);
                              } catch (e) {}
                            }
                            setNotifOpen(false);
                            if (notif.targetUrl) {
                              navigate(notif.targetUrl);
                            }
                          }}
                        >
                          <div className="tb-notif-item-title">{notif.title}</div>
                          <div className="tb-notif-item-msg">{notif.message}</div>
                        </div>
                      ))
                    )}
                  </div>

                  <div className="tb-notif-footer">
                    <Link to="/notifications" onClick={() => setNotifOpen(false)}>
                      View all notifications ➔
                    </Link>
                  </div>
                </div>
              )}
            </div>
          )}

          {isAuthenticated && (
            <Link to="/my-tickets" className="tb-header-link">
              My Tickets
            </Link>
          )}

          {isAdmin && (
            <Link to="/admin" className="tb-header-link">
              Admin
            </Link>
          )}

          <div className="tb-account" ref={accountRef}>
            {isAuthenticated ? (
              <>
                <button
                  type="button"
                  className="tb-header-link tb-account-toggle"
                  onClick={() => setMenuOpen((v) => !v)}
                >
                  {user?.fullName || user?.email || "Account"}
                  <svg
                    viewBox="0 0 20 20"
                    width="12"
                    height="12"
                    fill="none"
                    className={"tb-account-caret" + (menuOpen ? " open" : "")}
                  >
                    <path
                      d="m5 7 5 6 5-6"
                      stroke="currentColor"
                      strokeWidth="1.6"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
                {menuOpen && (
                  <div className="tb-account-menu">
                    <Link to="/profile" onClick={() => setMenuOpen(false)}>
                      My Profile
                    </Link>
                    {isAdmin && (
                      <Link to="/admin/vouchers" onClick={() => setMenuOpen(false)}>
                        Vouchers Admin
                      </Link>
                    )}
                    <button type="button" onClick={handleLogout}>
                      Log Out
                    </button>
                  </div>
                )}
              </>
            ) : (
              <Link to="/login" className="tb-btn tb-btn-primary tb-login-btn">
                Log In
              </Link>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
