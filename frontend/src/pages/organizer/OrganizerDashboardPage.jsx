import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import { useAuth } from "../../context/AuthContext";
import eventApi from "../../api/eventApi";
import reportApi from "../../api/reportApi";
import { formatDate, formatPrice } from "../../utils/format";
import "./MyConcertsPage.css";
import "./OrganizerWizard.css";
import "./OrganizerDashboardPage.css";

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

export default function OrganizerDashboardPage() {
  const { isOrganizer } = useAuth();
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    eventApi
      .getMyDashboard()
      .then(async (res) => {
        const base = res.data?.data || null;
        if (!base) return;

        // EventAPI owns event configuration, while paid orders/tickets live in TicketAPI.
        // Merge TicketAPI report totals so the outer dashboard and the event revenue page
        // always show the same sold-ticket/revenue numbers.
        const mergedEvents = await Promise.all((base.events || []).map(async (ev) => {
          try {
            const report = await reportApi.getRevenue(ev.eventId, {});
            const data = report.data?.data;
            const totals = data?.summary || data;
            return totals ? { ...ev, soldTickets: totals.ticketsSold ?? 0, revenue: totals.grossRevenue ?? 0 } : ev;
          } catch {
            return ev;
          }
        }));

        const merged = {
          ...base,
          events: mergedEvents,
          totalTicketsSold: mergedEvents.reduce((sum, ev) => sum + (ev.soldTickets || 0), 0),
          totalRevenue: mergedEvents.reduce((sum, ev) => sum + (ev.revenue || 0), 0),
        };
        if (!cancelled) setSummary(merged);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load your dashboard.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const events = summary?.events || [];

  return (
    <OrganizerShell title="Organizer Dashboard">

      <div className="tb-container od-wrap">
        <div className="od-head">
          <div>
            <p className="od-eyebrow">Organizer</p>
            <h1>Dashboard</h1>
          </div>
          {isOrganizer && (
            <div style={{ display: "flex", gap: "10px" }}>
              <Link to="/organizer/vouchers" className="tb-btn tb-btn-outline">
                🎟️ Quản lý Voucher
              </Link>
              <Link to="/organizer/events/new" className="tb-btn tb-btn-primary">
                + Create New Event
              </Link>
            </div>
          )}
        </div>

        {!isOrganizer && (
          <div className="od-banner-info">
            <strong>You don't have the Organizer role yet.</strong>{" "}
            <Link to="/organizer/request">Request to become an Organizer</Link>{" "}
            to unlock event creation.
          </div>
        )}

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && summary && (
          <>
            <div className="od-cards">
              <div className="od-card">
                <span className="od-card-label">Total events</span>
                <span className="od-card-value">{summary.totalEvents ?? 0}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">On sale</span>
                <span className="od-card-value">{summary.publishedEvents ?? 0}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Tickets sold</span>
                <span className="od-card-value">{summary.totalTicketsSold ?? 0}</span>
              </div>
              <div className="od-card od-card-highlight">
                <span className="od-card-label">Revenue (gross)</span>
                <span className="od-card-value">{formatPrice(summary.totalRevenue ?? 0)}</span>
              </div>
            </div>

            <div className="od-status-strip">
              <span>Draft: {summary.draftEvents ?? 0}</span>
              <span>Pending: {summary.pendingEvents ?? 0}</span>
              <span>Rejected: {summary.rejectedEvents ?? 0}</span>
              <span>Cancelled: {summary.cancelledEvents ?? 0}</span>
            </div>

            <div className="od-section-head">
              <h2>Your events</h2>
              <Link to="/organizer/events" className="ow-hint od-view-all">
                View as cards →
              </Link>
            </div>

            {events.length === 0 && (
              <div className="tb-empty">
                You haven't created any events yet.{" "}
                <Link to="/organizer/events/new">Create your first event</Link>
              </div>
            )}

            {events.length > 0 && (
              <div className="od-table-wrap">
                <table className="ow-table">
                  <thead>
                    <tr>
                      <th>Event</th>
                      <th>Status</th>
                      <th>Starts</th>
                      <th>Tickets sold</th>
                      <th>Revenue</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {events.map((ev) => (
                      <tr key={ev.eventId}>
                        <td>
                          <div className="ow-td-title">{ev.title}</div>
                        </td>
                        <td>
                          <span className={"mc-status " + (STATUS_CLASS[ev.status] || "")}>
                            {STATUS_LABELS[ev.status] || ev.status}
                          </span>
                        </td>
                        <td>{formatDate(ev.startsAt)}</td>
                        <td>
                          {ev.soldTickets ?? 0} / {ev.totalTickets ?? 0}
                        </td>
                        <td>{formatPrice(ev.revenue ?? 0)}</td>
                        <td>
                          <Link to={`/organizer/events/${ev.eventId}/edit`} className="ow-link">
                            Manage
                          </Link>
                          {ev.status === "Published" && (
                            <>
                              {" · "}
                              <Link to={`/organizer/events/${ev.eventId}/revenue`} className="ow-link">
                                Revenue
                              </Link>
                              {" · "}
                              <Link to={`/organizer/events/${ev.eventId}/staff`} className="ow-link">
                                Check-in &amp; staff
                              </Link>
                            </>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>

      </OrganizerShell>
  );
}
