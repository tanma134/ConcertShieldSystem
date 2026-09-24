import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import { useAuth } from "../../context/AuthContext";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "./MyConcertsPage.css";

const STATUS_LABELS = {
  Draft: "Draft",
  Pending: "Pending Review",
  Published: "On Sale",
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
    <div className="tb-app">
      <Header />

      <div className="tb-container mc-wrap">
        <div className="mc-head">
          <h1>My Events</h1>
          {isOrganizer && (
            <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
              + Create New Event
            </Link>
          )}
        </div>

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="tb-empty">
            You haven't created any events yet.{" "}
            <Link to="/organizer/events/new">Create your first event</Link>
          </div>
        )}

        {!loading && events.length > 0 && (
          <div className="mc-list">
            {events.map((ev) => (
              <article key={ev.eventId} className="mc-card">
                <Link
                  to={`/organizer/events/${ev.eventId}/edit`}
                  className="mc-card-link"
                >
                <div className="mc-card-poster">
                  {ev.posterUrl ? (
                    <img src={ev.posterUrl} alt={ev.title} />
                  ) : (
                    <div className="mc-card-poster-placeholder">No poster</div>
                  )}
                </div>
                <div className="mc-card-body">
                  <div className="mc-card-top">
                    <h3>{ev.title}</h3>
                    <span className={"mc-status " + (STATUS_CLASS[ev.status] || "")}>
                      {STATUS_LABELS[ev.status] || ev.status}
                    </span>
                  </div>
                  <p className="mc-card-date">{formatDateRange(ev.startsAt, ev.endsAt)}</p>
                  <p className="mc-card-meta">
                    {ev.city || "No city"} ·{" "}
                    {ev.minPrice != null ? `From ${formatPrice(ev.minPrice)}` : "No price"}
                  </p>
                </div>
                </Link>
                {["Draft", "Rejected"].includes(ev.status) && <div className="mc-config-links">
                  <Link to={`/organizer/events/${ev.eventId}/seating`}>Seating</Link>
                  <Link to={`/organizer/events/${ev.eventId}/pricing`}>Pricing</Link>
                  <Link to={`/organizer/events/${ev.eventId}/refunds`}>Refund policies</Link>
                </div>}
                {["Draft", "Rejected"].includes(ev.status) && (
                  <button
                    type="button"
                    className="mc-delete-draft"
                    data-event-id={ev.eventId}
                    data-event-title={ev.title}
                    onClick={handleDeleteDraft}
                    disabled={deletingId === ev.eventId}
                  >
                    {deletingId === ev.eventId ? "Deleting..." : `Delete ${ev.status.toLowerCase()}`}
                  </button>
                )}
              </article>
            ))}
          </div>
        )}
      </div>

      <Footer />
    </div>
  );
}
