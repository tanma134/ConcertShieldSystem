import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import { useAuth } from "../../context/AuthContext";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "./MyConcertsPage.css";

const STATUS_LABELS = {
  Draft: "Draft",
  Pending: "Pending Review",
  Published: "Published",
  Postponed: "Postponed",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
};

const STATUS_CLASS = {
  Draft: "mc-status-draft",
  Pending: "mc-status-pending",
  Published: "mc-status-published",
  Rejected: "mc-status-rejected",
  Cancelled: "mc-status-cancelled",
};

export default function MyConcertsPage() {
  const { isOrganizer } = useAuth();
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [deletingId, setDeletingId] = useState(null);

  useEffect(() => {
    setLoading(true);
    eventApi
      .getMine()
      .then((res) => setEvents(res.data?.data || []))
      .catch(() => setError("Could not load your events list."))
      .finally(() => setLoading(false));
  }, []);

  const handleDeleteDraft = async (event) => {
    event.preventDefault();
    event.stopPropagation();
    const eventId = Number(event.currentTarget.dataset.eventId);
    const title = event.currentTarget.dataset.eventTitle;
    if (!window.confirm(`Delete event "${title}"? This action cannot be undone.`)) return;

    setDeletingId(eventId);
    setError("");
    try {
      await eventApi.remove(eventId);
      setEvents((items) => items.filter((item) => item.eventId !== eventId));
    } catch (err) {
      setError(err.response?.data?.message || "Could not delete this event.");
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <OrganizerShell title="My Concerts">
      <div className="tb-container mc-wrap">
        <div className="mc-head">
          <div>
            <h1>My Events</h1>
            <p className="mc-head-sub">
              Manage your concert submissions, ticket setups, and live on-site operations.
            </p>
          </div>
          {isOrganizer && (
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary mc-create-btn">
              + Create New Event
            </Link>
          )}
        </div>

        {loading && <div className="tb-loading">Loading your events...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="mc-empty-card">
            <div className="mc-empty-icon">🎪</div>
            <h3>No events created yet</h3>
            <p>Start organizing your first concert or live music event with ConcertShield.</p>
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
              + Create Your First Event
            </Link>
          </div>
        )}

        {!loading && events.length > 0 && (
          <div className="mc-list">
            {events.map((ev) => {
              const isDraftOrRejected = ["Draft", "Rejected"].includes(ev.status);
              const isPublished = ev.status === "Published";
              const isPending = ev.status === "Pending";

              return (
                <article key={ev.eventId} className="mc-card">
                  {/* Cột 1: Poster hình ảnh */}
                  <Link
                    to={`/organizer/events/${ev.eventId}/edit`}
                    className="mc-card-poster-link"
                    title={`Edit ${ev.title}`}
                  >
                    <div className="mc-card-poster">
                      {ev.posterUrl ? (
                        <img src={ev.posterUrl} alt={ev.title} />
                      ) : (
                        <div className="mc-card-poster-placeholder">
                          <span>No Poster</span>
                        </div>
                      )}
                    </div>
                  </Link>

                  {/* Cột 2: Thông tin sự kiện và Quick Chips */}
                  <div className="mc-card-info">
                    <div className="mc-card-title-row">
                      <Link
                        to={`/organizer/events/${ev.eventId}/edit`}
                        className="mc-card-title-link"
                      >
                        <h3 className="mc-card-title">{ev.title}</h3>
                      </Link>
                      <span className={"mc-status-pill " + (STATUS_CLASS[ev.status] || "")}>
                        {STATUS_LABELS[ev.status] || ev.status}
                      </span>
                    </div>

                    <div className="mc-card-meta-list">
                      <div className="mc-meta-item">
                        <span className="mc-meta-icon" aria-hidden="true">📅</span>
                        <span>{formatDateRange(ev.startsAt, ev.endsAt)}</span>
                      </div>
                      <div className="mc-meta-item">
                        <span className="mc-meta-icon" aria-hidden="true">📍</span>
                        <span>{ev.city || "Venue TBA"}</span>
                        <span className="mc-meta-bullet">•</span>
                        <span className="mc-meta-price">
                          {ev.minPrice != null ? `From ${formatPrice(ev.minPrice)}` : "Price TBA"}
                        </span>
                      </div>
                    </div>

                    {/* Thanh chip cấu hình nhanh */}
                    <div className="mc-config-chip-group">
                      <span className="mc-config-chip-label">Quick Settings:</span>
                      {isDraftOrRejected && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/compliance`} className="mc-chip">
                            📋 Compliance
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/changes`} className="mc-chip">
                            🕒 Schedule
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}

                      {isPublished && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/checkin`} className="mc-chip">
                            👥 Staff & Gates
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/revenue`} className="mc-chip">
                            📊 Revenue
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}

                      {isPending && (
                        <>
                          <Link to={`/organizer/events/${ev.eventId}/compliance`} className="mc-chip">
                            📋 Compliance
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/seating`} className="mc-chip">
                            🪑 Seating
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/pricing`} className="mc-chip">
                            🏷️ Pricing
                          </Link>
                          <Link to={`/organizer/events/${ev.eventId}/refunds`} className="mc-chip">
                            🔄 Refunds
                          </Link>
                        </>
                      )}
                    </div>
                  </div>

                  {/* Cột 3: Nút thao tác chính */}
                  <div className="mc-card-actions">
                    {isPublished && (
                      <Link
                        to={`/organizer/events/${ev.eventId}/realtime-checkin`}
                        className="mc-btn-live-checkin"
                        title="Open real-time check-in monitor"
                      >
                        📡 Live Check-in
                      </Link>
                    )}

                    <Link
                      to={`/organizer/events/${ev.eventId}/edit`}
                      className="mc-btn-edit"
                    >
                      ✏️ {isPublished ? "View / Edit" : "Edit Event"}
                    </Link>

                    <Link
                      to={`/organizer/events/${ev.eventId}/orders`}
                      className="mc-btn-orders"
                    >
                      📦 View Orders
                    </Link>

                    {isDraftOrRejected && (
                      <button
                        type="button"
                        className="mc-btn-delete"
                        data-event-id={ev.eventId}
                        data-event-title={ev.title}
                        onClick={handleDeleteDraft}
                        disabled={deletingId === ev.eventId}
                        title={`Delete this ${ev.status.toLowerCase()} event`}
                      >
                        🗑️ {deletingId === ev.eventId ? "Deleting..." : `Delete ${ev.status.toLowerCase()}`}
                      </button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        )}
      </div>
    </OrganizerShell>
  );
}
