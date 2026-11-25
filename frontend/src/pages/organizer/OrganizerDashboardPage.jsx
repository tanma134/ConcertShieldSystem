import { useEffect, useState, useMemo } from "react";
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
  Postponed: "Postponed",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
};

const STATUS_CLASS = {
  Draft: "mc-status-draft",
  Pending: "mc-status-pending",
  Published: "mc-status-published",
  Postponed: "mc-status-pending",
  Rejected: "mc-status-rejected",
  Cancelled: "mc-status-cancelled",
};

export default function OrganizerDashboardPage() {
  const { isOrganizer } = useAuth();
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");

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
        const mergedEvents = await Promise.all(
          (base.events || []).map(async (ev) => {
            try {
              const report = await reportApi.getRevenue(ev.eventId, {});
              const data = report.data?.data;
              const totals = data?.summary || data;
              return totals
                ? {
                    ...ev,
                    soldTickets: totals.ticketsSold ?? 0,
                    revenue: totals.grossRevenue ?? 0,
                  }
                : ev;
            } catch {
              return ev;
            }
          })
        );

        const merged = {
          ...base,
          events: mergedEvents,
          totalTicketsSold: mergedEvents.reduce(
            (sum, ev) => sum + (ev.soldTickets || 0),
            0
          ),
          totalRevenue: mergedEvents.reduce(
            (sum, ev) => sum + (ev.revenue || 0),
            0
          ),
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

  const allEvents = summary?.events || [];

  const filteredEvents = useMemo(() => {
    if (statusFilter === "ALL") return allEvents;
    return allEvents.filter((ev) => ev.status === statusFilter);
  }, [allEvents, statusFilter]);

  return (
    <OrganizerShell title="Organizer Dashboard">
      <div className="tb-container od-wrap">
        <div className="od-head">
          <div>
            <p className="od-eyebrow">Organizer</p>
            <h1>Dashboard</h1>
          </div>
          {isOrganizer && (
            <div style={{ display: "flex", gap: "10px", flexWrap: "wrap" }}>
              <Link to="/organizer/vouchers" className="tb-btn tb-btn-outline">
                Voucher Management
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

        {loading && <div className="tb-loading"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && summary && (
          <>
            {/* Stat Cards */}
            <div className="od-cards">
              <div className="od-card">
                <span className="od-card-label">Total Events</span>
                <span className="od-card-value">{summary.totalEvents ?? 0}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">On Sale</span>
                <span className="od-card-value">
                  {summary.publishedEvents ?? 0}
                </span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Tickets Sold</span>
                <span className="od-card-value">
                  {summary.totalTicketsSold ?? 0}
                </span>
              </div>
              <div className="od-card od-card-highlight">
                <span className="od-card-label">Gross Revenue</span>
                <span className="od-card-value">
                  {formatPrice(summary.totalRevenue ?? 0)}
                </span>
              </div>
            </div>

            {/* Status Filter Tabs */}
            <div className="od-filter-tabs">
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "ALL" ? "active" : ""}`}
                onClick={() => setStatusFilter("ALL")}
              >
                All Events
                <span className="od-tab-count">({allEvents.length})</span>
              </button>
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "Published" ? "active" : ""}`}
                onClick={() => setStatusFilter("Published")}
              >
                On Sale
                <span className="od-tab-count">
                  ({summary.publishedEvents ?? 0})
                </span>
              </button>
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "Pending" ? "active" : ""}`}
                onClick={() => setStatusFilter("Pending")}
              >
                Pending Review
                <span className="od-tab-count">
                  ({summary.pendingEvents ?? 0})
                </span>
              </button>
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "Draft" ? "active" : ""}`}
                onClick={() => setStatusFilter("Draft")}
              >
                Draft
                <span className="od-tab-count">
                  ({summary.draftEvents ?? 0})
                </span>
              </button>
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "Rejected" ? "active" : ""}`}
                onClick={() => setStatusFilter("Rejected")}
              >
                Rejected
                <span className="od-tab-count">
                  ({summary.rejectedEvents ?? 0})
                </span>
              </button>
              <button
                type="button"
                className={`od-filter-tab ${statusFilter === "Cancelled" ? "active" : ""}`}
                onClick={() => setStatusFilter("Cancelled")}
              >
                Cancelled
                <span className="od-tab-count">
                  ({summary.cancelledEvents ?? 0})
                </span>
              </button>
            </div>

            {/* Section Head */}
            <div className="od-section-head">
              <h2>Your Events ({filteredEvents.length})</h2>
              <Link to="/organizer/events" className="od-view-all">
                View as cards →
              </Link>
            </div>

            {filteredEvents.length === 0 ? (
              <div className="tb-empty" style={{ margin: "20px 0" }}>
                {statusFilter === "ALL"
                  ? "You haven't created any events yet."
                  : `No events in "${STATUS_LABELS[statusFilter] || statusFilter}" status.`}{" "}
                {statusFilter === "ALL" && (
                  <Link to="/organizer/events/new">Create your first event</Link>
                )}
              </div>
            ) : (
              <div className="od-table-wrap">
                <table className="od-table">
                  <thead>
                    <tr>
                      <th style={{ width: "30%" }}>Event</th>
                      <th style={{ width: "12%", whiteSpace: "nowrap" }}>Status</th>
                      <th style={{ width: "14%", whiteSpace: "nowrap" }}>Starts</th>
                      <th style={{ width: "11%", whiteSpace: "nowrap" }}>Tickets Sold</th>
                      <th style={{ width: "13%", whiteSpace: "nowrap" }}>Revenue</th>
                      <th style={{ width: "20%", textAlign: "right" }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredEvents.map((ev) => (
                      <tr key={ev.eventId}>
                        <td>
                          <Link
                            to={`/organizer/events/${ev.eventId}/edit`}
                            className="od-event-title"
                            title={ev.title}
                          >
                            {ev.title}
                          </Link>
                        </td>
                        <td style={{ whiteSpace: "nowrap" }}>
                          <span
                            className={
                              "mc-status-pill " + (STATUS_CLASS[ev.status] || "")
                            }
                          >
                            {STATUS_LABELS[ev.status] || ev.status}
                          </span>
                        </td>
                        <td style={{ whiteSpace: "nowrap" }}>{formatDate(ev.startsAt)}</td>
                        <td style={{ whiteSpace: "nowrap" }}>
                          {ev.soldTickets ?? 0} / {ev.totalTickets ?? 0}
                        </td>
                        <td style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                          {formatPrice(ev.revenue ?? 0)}
                        </td>
                        <td>
                          <div
                            className="od-action-group"
                            style={{ display: "flex", flexWrap: "wrap", justifyContent: "flex-end", gap: "6px" }}
                          >
                            {ev.status === "Published" ? (
                              <>
                                <Link
                                  to={`/organizer/events/${ev.eventId}/changes`}
                                  className="od-action-btn od-action-btn-primary"
                                  title="Submit Event Change Request - Reschedule, Postpone, or Cancel"
                                >
                                  Change
                                </Link>
                                <Link
                                  to={`/organizer/events/${ev.eventId}/revenue`}
                                  className="od-action-btn"
                                >
                                  Revenue
                                </Link>
                                <Link
                                  to={`/organizer/events/${ev.eventId}/staff`}
                                  className="od-action-btn"
                                >
                                  Staff
                                </Link>
                              </>
                            ) : ["Draft", "Rejected"].includes(ev.status) ? (
                              <Link
                                to={`/organizer/events/${ev.eventId}/edit`}
                                className="od-action-btn od-action-btn-primary"
                                title="Edit Concert Information"
                              >
                                Edit
                              </Link>
                            ) : (
                              <Link
                                to={`/organizer/events/${ev.eventId}/edit`}
                                className="od-action-btn"
                                title="View event details"
                              >
                                View
                              </Link>
                            )}
                          </div>
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
