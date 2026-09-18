import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
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
  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    setLoading(true);
    eventApi
      .getMine()
      .then((res) => setEvents(res.data?.data || []))
      .catch(() => setError("Could not load your events list."))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="tb-app">
      <Header />

      <div className="tb-container mc-wrap">
        <div className="mc-head">
          <h1>My Events</h1>
          <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
            + Create New Event
          </Link>
        </div>

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="tb-empty">
            You haven't created any events yet.{" "}
            <Link to="/organizer/events/new">Create your first event</Link>
          </div>
        )}

        {!loading && !error && events.length > 0 && (
          <div className="mc-list">
            {events.map((ev) => (
              <Link
                key={ev.eventId}
                to={`/organizer/events/${ev.eventId}/edit`}
                className="mc-card"
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
            ))}
          </div>
        )}
      </div>

      <Footer />
    </div>
  );
}
