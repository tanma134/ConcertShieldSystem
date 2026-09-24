import { useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import EventCard from "../components/EventCard";
import eventApi from "../api/eventApi";
import "../styles/theme-dark.css";
import "./HomePage.css";

// value = giá trị gửi lên API (giữ nguyên tên thành phố trong DB), label = hiển thị tiếng Anh
const CITIES = [
  { value: "Hồ Chí Minh", label: "Ho Chi Minh City" },
  { value: "Hà Nội", label: "Hanoi" },
  { value: "Đà Nẵng", label: "Da Nang" },
];

export default function HomePage() {
  const [searchParams] = useSearchParams();
  const q = searchParams.get("q") || "";

  const hotRef = useRef(null);
  const upcomingRef = useRef(null);

  const [featured, setFeatured] = useState([]);
  const [featuredLoading, setFeaturedLoading] = useState(true);

  const [events, setEvents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [city, setCity] = useState("");

  // "Trending Concerts" - lấy sự kiện nổi bật
  useEffect(() => {
    setFeaturedLoading(true);
    eventApi
      .getFeatured(3)
      .then((res) => setFeatured(res.data?.data || []))
      .catch(() => setFeatured([]))
      .finally(() => setFeaturedLoading(false));
  }, []);

  // Danh sách sự kiện (tìm kiếm / lọc theo thành phố / sắp diễn ra)
  useEffect(() => {
    setLoading(true);
    setError("");

    const filter = {
      page: 1,
      // The home page is the public catalogue, so a newly approved concert
      // must not disappear merely because it falls after the first six rows.
      pageSize: 100,
      sortBy: "StartsAt",
      sortOrder: "asc",
    };
    if (q) filter.search = q;

    const request = city
      ? eventApi.getByCity(city, filter)
      : eventApi.getList(filter);

    request
      .then((res) => setEvents(res.data?.data?.items || []))
      .catch(() => setError("Couldn't load the event list. Please try again."))
      .finally(() => setLoading(false));
  }, [q, city]);

  const sectionTitle = useMemo(
    () => (q ? `Search results for "${q}"` : "🎵 Upcoming Concerts"),
    [q]
  );

  const scrollTo = (ref) => {
    ref.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  return (
    <div className="tb-app">
      <Header />

      {!q && (
        <section className="tb-hero">
          <div className="tb-container tb-hero-inner">
            <span className="tb-hero-badge">🎵 LEADING CONCERT PLATFORM</span>
            <h1 className="tb-hero-title">
              Discover top-tier <span>concerts</span>
              <br />
              curated just for you
            </h1>
            <p className="tb-hero-sub">
              Book fast - Get your ticket instantly
              <br />
              Hundreds of concerts big and small are waiting for you
            </p>
            <div className="tb-hero-actions">
              <button
                type="button"
                className="tb-btn tb-btn-primary"
                onClick={() => scrollTo(upcomingRef)}
              >
                Explore Concerts
              </button>
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                onClick={() => scrollTo(hotRef)}
              >
                Hot Concerts
              </button>
            </div>
          </div>
        </section>
      )}

      {!q && (featuredLoading || featured.length > 0) && (
        <section className="tb-container tb-list-section" ref={hotRef}>
          <div className="tb-list-header">
            <h2>🔥 Trending Concerts</h2>
            <a
              className="tb-section-link"
              onClick={(e) => {
                e.preventDefault();
                scrollTo(upcomingRef);
              }}
              href="#upcoming"
            >
              View all →
            </a>
          </div>

          {featuredLoading && <div className="tb-loading">Loading...</div>}

          {!featuredLoading && featured.length > 0 && (
            <div className="tb-grid tb-grid-3">
              {featured.map((ev) => (
                <EventCard key={ev.eventId} event={ev} />
              ))}
            </div>
          )}
        </section>
      )}

      <section
        className="tb-container tb-list-section"
        ref={upcomingRef}
        id="upcoming"
      >
        <div className="tb-list-header">
          <h2>{sectionTitle}</h2>
          <div className="tb-city-filter">
            <button
              type="button"
              className={city === "" ? "active" : ""}
              onClick={() => setCity("")}
            >
              All Cities
            </button>
            {CITIES.map((c) => (
              <button
                key={c.value}
                type="button"
                className={city === c.value ? "active" : ""}
                onClick={() => setCity(c.value)}
              >
                {c.label}
              </button>
            ))}
          </div>
        </div>

        {loading && <div className="tb-loading">Loading events...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}
        {!loading && !error && events.length === 0 && (
          <div className="tb-empty">No matching events yet.</div>
        )}

        {!loading && !error && events.length > 0 && (
          <div className="tb-grid">
            {events.map((ev) => (
              <EventCard key={ev.eventId} event={ev} />
            ))}
          </div>
        )}
      </section>

      <Footer />
    </div>
  );
}
