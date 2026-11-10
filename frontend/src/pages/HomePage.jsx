import { useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import EventCard from "../components/EventCard";
import eventApi from "../api/eventApi";
import { formatDate, formatPrice } from "../utils/format";
import "../styles/theme-dark.css";
import "./HomePage.css";

// value = giá trị gửi lên API (giữ nguyên tên thành phố trong DB), label = hiển thị tiếng Anh
const CITIES = [
  { value: "Hồ Chí Minh", label: "Ho Chi Minh City" },
  { value: "Hà Nội", label: "Hanoi" },
  { value: "Đà Nẵng", label: "Da Nang" },
];

const FALLBACK_POSTER =
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    `<svg xmlns='http://www.w3.org/2000/svg' width='640' height='400'><defs><linearGradient id='g' x1='0' y1='0' x2='1' y2='1'><stop offset='0' stop-color='#a855f7'/><stop offset='1' stop-color='#e8439c'/></linearGradient></defs><rect width='100%' height='100%' fill='url(#g)'/></svg>`
  );

// Thẻ sự kiện nổi bật ở cột phải hero (giống preview trong ảnh thiết kế).
function SpotlightCard({ event }) {
  const navigate = useNavigate();

  const open = () => {
    if (event?.slug) navigate(`/events/${event.slug}`);
    else document.getElementById("upcoming")?.scrollIntoView({ behavior: "smooth" });
  };

  return (
    <div
      className="tb-spotlight"
      role="button"
      tabIndex={0}
      onClick={open}
      onKeyDown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          open();
        }
      }}
      style={{ cursor: "pointer" }}
    >
      <div className="tb-spotlight-media">
        <span className="tb-spotlight-pill">
          <svg viewBox="0 0 20 20" width="12" height="12" fill="none">
            <path d="M10 3a7 7 0 0 1 7 7M10 6a4 4 0 0 1 4 4" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
            <circle cx="10" cy="12.5" r="1.6" fill="currentColor" />
          </svg>
          Live Virtual Queue
        </span>
        <img
          src={event?.posterUrl || FALLBACK_POSTER}
          alt={event?.title || "Featured concert"}
          onError={(e) => {
            e.currentTarget.src = FALLBACK_POSTER;
          }}
        />
        <div className="tb-spotlight-caption">
          <span className="tb-spotlight-kicker">
            {event?.city || "Featured Concert"}
          </span>
          <span className="tb-spotlight-title">
            {event?.title || "Discover your next live experience"}
          </span>
        </div>
      </div>

      <div className="tb-spotlight-meta">
        <span className="tb-spotlight-meta-item">
          <svg viewBox="0 0 20 20" width="14" height="14" fill="none">
            <rect x="2.5" y="4" width="15" height="13" rx="2" stroke="currentColor" strokeWidth="1.4" />
            <path d="M2.5 8h15M6.5 2.5v3M13.5 2.5v3" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
          </svg>
          <span>{event?.startsAt ? formatDate(event.startsAt) : "Date to be announced"}</span>
        </span>
        <span className="tb-spotlight-meta-item">
          <svg viewBox="0 0 20 20" width="14" height="14" fill="none">
            <path d="M10 18s6-5.686 6-10a6 6 0 1 0-12 0c0 4.314 6 10 6 10Z" stroke="currentColor" strokeWidth="1.4" />
            <circle cx="10" cy="8" r="2.3" stroke="currentColor" strokeWidth="1.4" />
          </svg>
          <span>{event?.locationName || event?.city || "Venue to be announced"}</span>
        </span>
      </div>

      <div className="tb-spotlight-foot">
        <span className="tb-spotlight-protected">
          <svg viewBox="0 0 20 20" width="16" height="16" fill="none">
            <path d="M10 2 3.5 4.7v4.6c0 3.9 2.8 6.7 6.5 7.7 3.7-1 6.5-3.8 6.5-7.7V4.7L10 2Z" stroke="#0e9e78" strokeWidth="1.5" />
            <path d="m7.4 10 1.9 1.9 3.3-3.8" stroke="#0e9e78" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          ConcertShield Protocol
        </span>
        <span className="tb-spotlight-price">
          <small>Starting from</small>
          <strong>
            {event?.minPrice != null ? formatPrice(event.minPrice) : "Coming soon"}
          </strong>
        </span>
      </div>
    </div>
  );
}

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

  // Sự kiện nổi bật nhất dùng cho thẻ preview trong hero
  const spotlight = useMemo(
    () => featured.find((ev) => ev?.posterUrl) || featured[0] || null,
    [featured]
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
            <div className="tb-hero-copy">
              <span className="tb-hero-badge">
                <span className="tb-hero-badge-dot" aria-hidden="true">
                  <svg viewBox="0 0 20 20" width="12" height="12" fill="none">
                    <path
                      d="M8 15V5.6l6-1.4v9.2"
                      stroke="currentColor"
                      strokeWidth="1.6"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                    <circle cx="6" cy="15" r="2" stroke="currentColor" strokeWidth="1.6" />
                    <circle cx="12" cy="13.4" r="2" stroke="currentColor" strokeWidth="1.6" />
                  </svg>
                </span>
                Leading Concert Platform
              </span>
              <h1 className="tb-hero-title">
                Discover top-tier <span>concerts</span> curated just for you
              </h1>
              <p className="tb-hero-sub">
                Book fast with zero scalper bot interference. Experience
                high-security Dynamic Rotating QR Tickets and fair admission
                virtual queues.
              </p>
              <div className="tb-hero-actions">
                <button
                  type="button"
                  className="tb-btn tb-btn-primary"
                  onClick={() => scrollTo(upcomingRef)}
                >
                  <span aria-hidden="true">🎆</span> Explore Concerts
                  <svg viewBox="0 0 20 20" width="15" height="15" fill="none">
                    <path
                      d="M4 10h12M11 5l5 5-5 5"
                      stroke="currentColor"
                      strokeWidth="1.8"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                    />
                  </svg>
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={() => scrollTo(hotRef)}
                >
                  <span aria-hidden="true">🔥</span> Hot Festivals
                </button>
              </div>
            </div>

            <div className="tb-hero-visual">
              <SpotlightCard event={spotlight} />
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
