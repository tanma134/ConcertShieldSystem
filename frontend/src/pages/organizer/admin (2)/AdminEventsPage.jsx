import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import eventApi from "../../api/eventApi";
import { formatDateRange, formatPrice } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";

const PAGE_SIZE = 12;

export default function AdminEventsPage() {
  const [items, setItems] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = (p = page) => {
    setLoading(true);
    setError("");
    eventApi
      .getPending(p, PAGE_SIZE)
      .then((res) => {
        const data = res.data?.data;
        setItems(data?.items || []);
        setTotalCount(data?.totalCount ?? data?.items?.length ?? 0);
        setPage(p);
      })
      .catch((err) =>
        setError(err.response?.data?.message || "Could not load the moderation queue.")
      )
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    load(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div className="tb-app">
      <Header />

      <div className="tb-container ow-wrap">
        <div className="ow-head">
          <div>
            <h1>Event Moderation</h1>
            <p className="ow-sub">
              Concerts submitted by organizers, waiting for approval or
              rejection.
            </p>
          </div>
        </div>

        {error && <div className="ow-error">{error}</div>}
        {loading && <div className="tb-loading">Loading...</div>}

        {!loading && !error && items.length === 0 && (
          <div className="tb-empty">Nothing pending review right now.</div>
        )}

        {!loading && !error && items.length > 0 && (
          <>
            <table className="ow-table admin-queue-table">
              <thead>
                <tr>
                  <th></th>
                  <th>Event</th>
                  <th>City</th>
                  <th>Dates</th>
                  <th>Tickets</th>
                  <th>From</th>
                  <th>Submitted</th>
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
                      {ev.hasSeatingChart && (
                        <div className="ow-td-sub">Has seating chart</div>
                      )}
                    </td>
                    <td>{ev.city || "—"}</td>
                    <td>{formatDateRange(ev.startsAt, ev.endsAt)}</td>
                    <td>{ev.totalTickets}</td>
                    <td>
                      {ev.minPrice != null ? formatPrice(ev.minPrice) : "—"}
                    </td>
                    <td>
                      {ev.submittedAt
                        ? formatDateRange(ev.submittedAt, ev.submittedAt)
                        : "—"}
                    </td>
                    <td>
                      <Link
                        to={`/admin/events/${ev.eventId}`}
                        className="tb-btn tb-btn-outline ow-btn-sm"
                      >
                        Review →
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
                  onClick={() => load(page - 1)}
                >
                  ← Prev
                </button>
                <span>
                  Page {page} / {totalPages}
                </span>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => load(page + 1)}
                >
                  Next →
                </button>
              </div>
            )}
          </>
        )}
      </div>

      <Footer />
    </div>
  );
}
