import { useEffect, useMemo, useState } from "react";
import { useSearchParams, Link } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import EventCard from "../components/EventCard";
import eventApi from "../api/eventApi";
import "../styles/theme-dark.css";
import "./EventsListPage.css";

const CITIES = [
  { value: "Hồ Chí Minh", label: "Ho Chi Minh City" },
  { value: "Hà Nội", label: "Hanoi" },
  { value: "Đà Nẵng", label: "Da Nang" },
];

function EventsListPage() {
  const [searchParams] = useSearchParams();
  const qParam = searchParams.get("q") || "";
  const cityParam = searchParams.get("city") || "";

  const [city, setCity] = useState(cityParam);
  const [saleStatus, setSaleStatus] = useState("all"); // "all" | "on_sale" | "upcoming_sale"
  const [sortBy, setSortBy] = useState("StartsAt");
  const [sortOrder, setSortOrder] = useState("asc");

  const [events, setEvents] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 12;

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Sync city when search params change
  useEffect(() => {
    if (cityParam) setCity(cityParam);
  }, [cityParam]);

  // Fetch events from API
  useEffect(() => {
    setLoading(true);
    setError("");

    const filter = {
      page,
      pageSize,
      sortBy,
      sortOrder,
    };

    if (qParam.trim()) {
      filter.search = qParam.trim();
    }

    const req = city
      ? eventApi.getByCity(city, filter)
      : eventApi.getList(filter);

    req
      .then((res) => {
        const data = res.data?.data;
        setEvents(data?.items || []);
        setTotalCount(data?.totalCount || 0);
      })
      .catch(() => {
        setError("Could not load concerts. Please try again later.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [qParam, city, page, sortBy, sortOrder]);

  // Client-side filtering by ticket sale status
  const displayedEvents = useMemo(() => {
    return events.filter((ev) => {
      const isEnded = new Date(ev.endsAt || ev.startsAt) < new Date();
      if (isEnded) return false;

      if (saleStatus === "on_sale") {
        return ev.availableTickets === undefined || ev.availableTickets > 0;
      }
      if (saleStatus === "upcoming_sale") {
        return ev.availableTickets === 0 || ev.canSell === false;
      }
      return true;
    });
  }, [events, saleStatus]);

  const totalPages = Math.ceil(totalCount / pageSize);

  return (
    <div className="tb-app tb-events-page">
      <Header />

      <main className="tb-events-main">
        {/* Page Hero Header */}
        <section className="tb-events-hero">
          <div className="tb-container">
            <div className="tb-events-breadcrumbs">
              <Link to="/">Home</Link>
              <span className="separator">/</span>
              <span>All Concerts & Events</span>
            </div>
            <h1 className="tb-events-title">
              Discover <span>Top Concerts & Live Shows</span>
            </h1>
            <p className="tb-events-desc">
              Experience the best live music events with high-security Dynamic Rotating QR Tickets and fair virtual queues.
            </p>
          </div>
        </section>

        {/* Filter Bar & Results Section */}
        <section className="tb-container tb-events-content">
          <div className="tb-events-filter-bar">
            {/* Left: Heading & Count */}
            <div className="tb-results-heading">
              {qParam ? (
                <h2>
                  Search results for: <span>"{qParam}"</span>
                </h2>
              ) : (
                <h2>All Concerts</h2>
              )}
              <span className="tb-results-count">({totalCount} events)</span>
            </div>

            {/* Right: Modern Filter Controls (Ticketbox / Ticketmaster style) */}
            <div className="tb-filter-controls">
              {/* Sale Status Tabs */}
              <div className="tb-status-tabs">
                <button
                  type="button"
                  className={saleStatus === "all" ? "active" : ""}
                  onClick={() => setSaleStatus("all")}
                >
                  All
                </button>
                <button
                  type="button"
                  className={saleStatus === "on_sale" ? "active" : ""}
                  onClick={() => setSaleStatus("on_sale")}
                >
                  🔥 On Sale
                </button>
                <button
                  type="button"
                  className={saleStatus === "upcoming_sale" ? "active" : ""}
                  onClick={() => setSaleStatus("upcoming_sale")}
                >
                  ⏰ Coming Soon
                </button>
              </div>

              {/* City Filter Pills */}
              <div className="tb-city-pills">
                <button
                  type="button"
                  className={city === "" ? "active" : ""}
                  onClick={() => {
                    setCity("");
                    setPage(1);
                  }}
                >
                  All Cities
                </button>
                {CITIES.map((c) => (
                  <button
                    key={c.value}
                    type="button"
                    className={city === c.value ? "active" : ""}
                    onClick={() => {
                      setCity(c.value);
                      setPage(1);
                    }}
                  >
                    {c.label}
                  </button>
                ))}
              </div>

              {/* Sort Dropdown */}
              <div className="tb-sort-group">
                <select
                  value={`${sortBy}-${sortOrder}`}
                  onChange={(e) => {
                    const [b, o] = e.target.value.split("-");
                    setSortBy(b);
                    setSortOrder(o);
                    setPage(1);
                  }}
                  className="tb-sort-select"
                >
                  <option value="StartsAt-asc">Sort: Upcoming First</option>
                  <option value="CreatedAt-desc">Sort: Recently Added</option>
                  <option value="ViewCount-desc">Sort: Most Popular</option>
                </select>
              </div>
            </div>
          </div>

          {/* Loading, Error, Empty States */}
          {loading && <div className="tb-loading">Loading concerts...</div>}
          {!loading && error && <div className="tb-error">{error}</div>}
          {!loading && !error && displayedEvents.length === 0 && (
            <div className="tb-empty">
              No matching concerts found for the selected filters.
            </div>
          )}

          {/* Event Cards Grid */}
          {!loading && !error && displayedEvents.length > 0 && (
            <>
              <div className="tb-grid">
                {displayedEvents.map((ev) => (
                  <EventCard key={ev.eventId} event={ev} />
                ))}
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="tb-pagination">
                  <button
                    type="button"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    className="tb-page-btn"
                  >
                    ← Previous
                  </button>
                  <span className="tb-page-info">
                    Page {page} of {totalPages}
                  </span>
                  <button
                    type="button"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    className="tb-page-btn"
                  >
                    Next →
                  </button>
                </div>
              )}
            </>
          )}
        </section>
      </main>

      <Footer />
    </div>
  );
}

export default EventsListPage;
