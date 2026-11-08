import { useEffect, useState, useMemo } from "react";
import { useSearchParams, Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import EventCard from "../../components/EventCard";
import eventApi from "../../api/eventApi";
import "../../styles/theme-dark.css";
import "./AllEventsPage.css";

const CITIES = [
  { value: "", label: "All Cities" },
  { value: "Hồ Chí Minh", label: "Ho Chi Minh City" },
  { value: "Hà Nội", label: "Hanoi" },
  { value: "Đà Nẵng", label: "Da Nang" },
  { value: "Cần Thơ", label: "Can Tho" },
];

const SORT_OPTIONS = [
  { value: "StartsAt:asc", label: "Upcoming (Earliest first)" },
  { value: "StartsAt:desc", label: "Upcoming (Furthest first)" },
  { value: "CreatedAt:desc", label: "Newly Published" },
  { value: "Title:asc", label: "Name (A - Z)" },
];

export default function AllEventsPage() {
  const [searchParams, setSearchParams] = useSearchParams();

  const [keyword, setKeyword] = useState(searchParams.get("q") || "");
  const [city, setCity] = useState(searchParams.get("city") || "");
  const [sort, setSort] = useState(searchParams.get("sort") || "StartsAt:asc");
  const [page, setPage] = useState(Number(searchParams.get("page")) || 1);
  const pageSize = 12;

  const [events, setEvents] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadEvents = async () => {
    setLoading(true);
    setError("");

    const [sortBy, sortOrder] = sort.split(":");
    const filter = {
      page,
      pageSize,
      sortBy: sortBy || "StartsAt",
      sortOrder: sortOrder || "asc",
    };

    if (keyword.trim()) filter.search = keyword.trim();
    if (city) filter.city = city;

    try {
      const res = await eventApi.getList(filter);
      const data = res.data?.data;
      const items = data?.items || [];
      const total = data?.totalCount || items.length;

      setEvents(items);
      setTotalCount(total);
      setTotalPages(Math.max(1, Math.ceil(total / pageSize)));
    } catch (err) {
      setError(err.response?.data?.message || "Could not load concerts. Please retry.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEvents();
    // Sync URL query params
    const nextParams = {};
    if (keyword.trim()) nextParams.q = keyword.trim();
    if (city) nextParams.city = city;
    if (sort !== "StartsAt:asc") nextParams.sort = sort;
    if (page > 1) nextParams.page = String(page);
    setSearchParams(nextParams, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keyword, city, sort, page]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    setPage(1);
    loadEvents();
  };

  const handleReset = () => {
    setKeyword("");
    setCity("");
    setSort("StartsAt:asc");
    setPage(1);
  };

  return (
    <div className="tb-app all-events-page">
      <Header />

      <main className="tb-container" style={{ padding: "40px 20px 80px" }}>
        {/* Page Header */}
        <div className="all-events-header">
          <div>
            <span className="all-events-badge">LIVE ON SALE</span>
            <h1 className="all-events-title">All Concerts &amp; Live Events</h1>
            <p className="all-events-sub">
              Browse all verified concerts officially on sale. Pick your favorite seats and experience world-class performances.
            </p>
          </div>
        </div>

        {/* Filters Bar */}
        <div className="all-events-toolbar">
          <div className="all-events-filters">
            <div className="filter-group">
              <label htmlFor="city-filter">City</label>
              <select
                id="city-filter"
                value={city}
                onChange={(e) => {
                  setCity(e.target.value);
                  setPage(1);
                }}
              >
                {CITIES.map((c) => (
                  <option key={c.value} value={c.value}>
                    {c.label}
                  </option>
                ))}
              </select>
            </div>

            <div className="filter-group">
              <label htmlFor="sort-filter">Sort by</label>
              <select
                id="sort-filter"
                value={sort}
                onChange={(e) => {
                  setSort(e.target.value);
                  setPage(1);
                }}
              >
                {SORT_OPTIONS.map((opt) => (
                  <option key={opt.value} value={opt.value}>
                    {opt.label}
                  </option>
                ))}
              </select>
            </div>

            {(keyword || city || sort !== "StartsAt:asc") && (
              <button
                type="button"
                className="tb-btn tb-btn-outline filter-reset-btn"
                onClick={handleReset}
              >
                Reset Filters
              </button>
            )}
          </div>
        </div>

        {/* Results Count Meta */}
        <div className="all-events-meta">
          <span>
            {loading ? "Searching concerts..." : `Showing ${events.length} of ${totalCount} concerts`}
          </span>
          {city && (
            <span className="active-tag">
              City: {CITIES.find((c) => c.value === city)?.label || city}
              <button onClick={() => { setCity(""); setPage(1); }}>✕</button>
            </span>
          )}
          {keyword && (
            <span className="active-tag">
              Keyword: "{keyword}"
              <button onClick={() => { setKeyword(""); setPage(1); }}>✕</button>
            </span>
          )}
        </div>

        {/* Status display */}
        {loading && <div className="tb-loading" style={{ margin: "40px 0" }}>Loading concerts...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {/* Empty state */}
        {!loading && !error && events.length === 0 && (
          <div className="all-events-empty">
            <div className="empty-icon">🎵</div>
            <h3>No concerts found</h3>
            <p>We couldn't find any concerts matching your criteria. Try adjusting your filters or search terms.</p>
            <button type="button" className="tb-btn tb-btn-outline" onClick={handleReset}>
              Clear all filters
            </button>
          </div>
        )}

        {/* Grid display */}
        {!loading && !error && events.length > 0 && (
          <div className="tb-grid tb-grid-3">
            {events.map((ev) => (
              <EventCard key={ev.eventId} event={ev} />
            ))}
          </div>
        )}

        {/* Pagination */}
        {!loading && !error && totalPages > 1 && (
          <div className="all-events-pagination">
            <button
              type="button"
              className="tb-btn tb-btn-outline"
              disabled={page <= 1}
              onClick={() => {
                setPage((p) => Math.max(1, p - 1));
                window.scrollTo({ top: 0, behavior: "smooth" });
              }}
            >
              ← Previous
            </button>

            <span className="pagination-info">
              Page <strong>{page}</strong> of <strong>{totalPages}</strong>
            </span>

            <button
              type="button"
              className="tb-btn tb-btn-outline"
              disabled={page >= totalPages}
              onClick={() => {
                setPage((p) => Math.min(totalPages, p + 1));
                window.scrollTo({ top: 0, behavior: "smooth" });
              }}
            >
              Next →
            </button>
          </div>
        )}
      </main>

      <Footer />
    </div>
  );
}

