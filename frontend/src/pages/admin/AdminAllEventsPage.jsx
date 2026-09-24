import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";

const PAGE_SIZE = 12;

const STATUS_OPTIONS = [
  ["", "All statuses"],
  ["Draft", "Draft"],
  ["Pending", "Pending"],
  ["Published", "Published"],
  ["Rejected", "Rejected"],
  ["Cancelled", "Cancelled"],
];

export default function AdminAllEventsPage() {
  const [items, setItems] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Filter inputs (what the person is typing/selecting)
  const [status, setStatus] = useState("");
  const [city, setCity] = useState("");
  const [search, setSearch] = useState("");
  const [dateFrom, setDateFrom] = useState("");

  // Filters actually applied to the last request (so typing doesn't refetch
  // on every keystroke — only on submit / status change / page change).
  const [appliedFilter, setAppliedFilter] = useState({});

  const load = (p, filter) => {
    setLoading(true);
    setError("");
    eventApi
      .getAllAdmin({ ...filter, page: p, pageSize: PAGE_SIZE })
      .then((res) => {
        const data = res.data?.data;
        setItems(data?.items || []);
        setTotalCount(data?.totalCount ?? data?.items?.length ?? 0);
        setPage(p);
      })
      .catch((err) =>
        setError(err.response?.data?.message || "Could not load events.")
      )
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load(1, {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const applyFilters = (e) => {
    e?.preventDefault();
    const filter = {
      status: status || undefined,
      city: city.trim() || undefined,
      search: search.trim() || undefined,
      dateFrom: dateFrom || undefined,
    };
    setAppliedFilter(filter);
    load(1, filter);
  };

  const clearFilters = () => {
    setStatus("");
    setCity("");
    setSearch("");
    setDateFrom("");
    setAppliedFilter({});
    load(1, {});
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <AdminShell title="All Events">
      <div className="ow-wrap">
        <div className="ow-head">
          <div>
            <h1>All Events</h1>
            <p className="ow-sub">
              Every concert regardless of status — Draft, Pending, Published,
              Rejected, or Cancelled. For approvals specifically, use the
              Pending queue.
            </p>
          </div>
          <Link to="/admin/events" className="tb-btn tb-btn-outline">
            Pending queue →
          </Link>
        </div>

        <form className="admin-filter-bar" onSubmit={applyFilters}>
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            {STATUS_OPTIONS.map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
          <input
            type="text"
            placeholder="City"
            value={city}
            onChange={(e) => setCity(e.target.value)}
          />
          <input
            type="text"
            placeholder="Search title..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            title="Events starting on or after this date"
          />
          <button type="submit" className="tb-btn tb-btn-primary ow-btn-sm">
            Apply
          </button>
          {(status || city || search || dateFrom) && (
            <button
              type="button"
              className="tb-btn tb-btn-outline ow-btn-sm"
              onClick={clearFilters}
            >
              Clear
            </button>
          )}
        </form>

        {error && <div className="ow-error">{error}</div>}
        {loading && <div className="tb-loading">Loading...</div>}

        {!loading && !error && items.length === 0 && (
          <div className="tb-empty">No events match these filters.</div>
        )}

        {!loading && !error && items.length > 0 && (
          <>
            <table className="ow-table admin-queue-table">
              <thead>
                <tr>
                  <th></th>
                  <th>Event</th>
                  <th>Status</th>
                  <th>City</th>
                  <th>Dates</th>
                  <th>Tickets (sold/total)</th>
                  <th>From</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((ev) => (
                  <tr key={ev.eventId}>
                    <td>
                      {ev.posterUrl ? (
                        <img
                          className="admin-queue-thumb"
                          src={ev.posterUrl}
                          alt={ev.title}
                        />
                      ) : (
                        <div className="admin-queue-thumb admin-queue-thumb-empty" />
                      )}
                    </td>
                    <td>
                      <div className="ow-td-title">{ev.title}</div>
                    </td>
                    <td>
                      <span
                        className={
                          "admin-status-pill admin-status-" +
                          (ev.status || "").toLowerCase()
                        }
                      >
                        {ev.status}
                      </span>
                    </td>
                    <td>{ev.city || "—"}</td>
                    <td>{formatDateRange(ev.startsAt, ev.endsAt)}</td>
                    <td>
                      {ev.soldTickets ?? 0}/{ev.totalTickets ?? 0}
                    </td>
                    <td>
                      {ev.minPrice != null ? formatPrice(ev.minPrice) : "—"}
                    </td>
                    <td>
                      <Link
                        to={`/admin/events/${ev.eventId}`}
                        className="tb-btn tb-btn-outline ow-btn-sm"
                      >
                        View →
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {totalPages > 1 && (
              <div className="admin-pagination">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  disabled={page <= 1}
                  onClick={() => load(page - 1, appliedFilter)}
                >
                  ← Prev
                </button>
                <span>
                  Page {page} / {totalPages} ({totalCount} total)
                </span>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => load(page + 1, appliedFilter)}
                >
                  Next →
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </AdminShell>
  );
}
