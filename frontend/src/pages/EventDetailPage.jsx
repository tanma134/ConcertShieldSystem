import { useEffect, useMemo, useState } from "react";
import { useParams, Link } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import EventCard from "../components/EventCard";
import ReviewSection from "../components/ReviewSection";
import eventApi from "../api/eventApi";
import wishlistApi from "../api/wishlistApi";
import { useAuth } from "../context/AuthContext";
import { formatDateRange, formatPrice } from "../utils/format";
import "../styles/theme-dark.css";
import "./EventDetailPage.css";

const STATUS_LABELS = {
  Published: "On Sale",
  Draft: "Draft",
  PendingApproval: "Pending Approval",
  Cancelled: "Cancelled",
  Completed: "Ended",
};

export default function EventDetailPage() {
  const { slug } = useParams();
  const { isAuthenticated } = useAuth();
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [activeImg, setActiveImg] = useState(0);

  const [related, setRelated] = useState([]);
  const [relatedLoading, setRelatedLoading] = useState(true);
  const [isWishlisted, setIsWishlisted] = useState(false);
  const [wishlistLoading, setWishlistLoading] = useState(false);
  const [wishlistError, setWishlistError] = useState("");

  // Event data is synchronized with the current route.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => {
    setLoading(true);
    setError("");
    setActiveImg(0);
    eventApi
      .getBySlug(slug)
      .then((res) => setEvent(res.data?.data || null))
      .catch(() => setError("Event not found."))
      .finally(() => setLoading(false));
  }, [slug]);

  // Wishlist status is synchronized with the current event and session.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => {
    if (!event?.eventId || !isAuthenticated) {
      setIsWishlisted(false);
      return;
    }
    wishlistApi.getStatus(event.eventId)
      .then((status) => setIsWishlisted(Boolean(status?.isWishlisted)))
      .catch(() => setIsWishlisted(false));
  }, [event?.eventId, isAuthenticated]);

  const toggleWishlist = async () => {
    if (!isAuthenticated) {
      window.location.href = "/login";
      return;
    }
    try {
      setWishlistLoading(true);
      setWishlistError("");
      if (isWishlisted) await wishlistApi.remove(event.eventId);
      else await wishlistApi.add(event.eventId);
      setIsWishlisted(!isWishlisted);
    } catch (requestError) {
      setWishlistError(requestError.response?.data?.message || "Wishlist update failed.");
    } finally {
      setWishlistLoading(false);
    }
  };

  // Một vài sự kiện khác đang mở bán, hiển thị bên dưới trang chi tiết
  useEffect(() => {
    setRelatedLoading(true);
    eventApi
      .getList({ page: 1, pageSize: 5, sortBy: "StartsAt", sortOrder: "asc" })
      .then((res) => {
        const items = res.data?.data?.items || [];
        setRelated(items.filter((ev) => ev.slug !== slug).slice(0, 4));
      })
      .catch(() => setRelated([]))
      .finally(() => setRelatedLoading(false));
  }, [slug]);

  const gallery = useMemo(() => {
    if (!event) return [];
    const imgs = [...(event.images || [])].sort(
      (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)
    );
    const urls = imgs.map((img) => img.imageUrl).filter(Boolean);
    const main = event.bannerUrl || event.posterUrl;
    if (main && !urls.includes(main)) urls.unshift(main);
    return urls;
  }, [event]);

  const minPrice = event?.ticketTypes?.length
    ? Math.min(...event.ticketTypes.map((t) => t.price))
    : null;

  const availableTickets =
    typeof event?.availableTickets === "number" ? event.availableTickets : null;

  // Chỉ hiện nút "Mua vé" khi sự kiện đang thực sự diễn ra (giữa startsAt và
  // endsAt). Đã kết thúc hoặc chưa bắt đầu thì ẩn nút, hiện thông báo tương ứng.
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    setNow(Date.now());
  }, [event]);

  const timingStatus = useMemo(() => {
    if (!event?.startsAt) return null;
    const startsAtTime = new Date(event.startsAt).getTime();
    const endsAtTime = event.endsAt ? new Date(event.endsAt).getTime() : startsAtTime;
    if (Number.isNaN(startsAtTime)) return null;
    if (now < startsAtTime) return "upcoming";
    if (now > endsAtTime) return "ended";
    return "ongoing";
  }, [event, now]);

  const canBuy =
    timingStatus === "ongoing" &&
    event?.status !== "Cancelled" &&
    (availableTickets === null || availableTickets > 0);

  const buyBlockedMessage = useMemo(() => {
    if (event?.status === "Cancelled") return "This event has been cancelled.";
    if (timingStatus === "ended") return "This event has already ended.";
    if (timingStatus === "upcoming")
      return "Ticket sales open once the event begins.";
    if (availableTickets === 0) return "Sold out.";
    return null;
  }, [event, timingStatus, availableTickets]);

  return (
    <div className="tb-app">
      <Header />

      {loading && <div className="tb-loading">Loading event...</div>}
      {!loading && error && <div className="tb-error">{error}</div>}

      {!loading && !error && event && (
        <>
          <section className="tb-detail-banner-wrap">
            <img
              className="tb-detail-banner-img"
              src={gallery[activeImg] || event.bannerUrl || event.posterUrl}
              alt={event.title}
            />
            <div className="tb-detail-banner-overlay" />
            <div className="tb-container tb-detail-banner-content">
              {event.status && (
                <span className="tb-detail-status">
                  {STATUS_LABELS[event.status] || event.status}
                </span>
              )}
              <h1>{event.title}</h1>
            </div>
          </section>

          {gallery.length > 1 && (
            <div className="tb-container tb-detail-thumbs">
              {gallery.map((url, idx) => (
                <button
                  type="button"
                  key={url + idx}
                  className={
                    "tb-detail-thumb" + (idx === activeImg ? " active" : "")
                  }
                  onClick={() => setActiveImg(idx)}
                >
                  <img src={url} alt={`${event.title} ${idx + 1}`} />
                </button>
              ))}
            </div>
          )}

          <div className="tb-container tb-detail-layout">
            <div className="tb-detail-main">
              {(event.shortDescription || event.description) && (
                <section className="tb-detail-block">
                  <h2>About</h2>
                  {event.shortDescription && (
                    <p className="tb-detail-desc">{event.shortDescription}</p>
                  )}
                  {event.description && (
                    <p className="tb-detail-desc">{event.description}</p>
                  )}
                </section>
              )}

              {event.ticketTypes?.length > 0 && (
                <section className="tb-detail-block">
                  <h2>Ticket Types</h2>
                  <div className="tb-ticket-list">
                    {event.ticketTypes.map((t) => (
                      <div className="tb-ticket-row" key={t.ticketTypeId}>
                        <div>
                          <div className="tb-ticket-name">{t.typeName}</div>
                          {t.description && (
                            <div className="tb-ticket-desc">{t.description}</div>
                          )}
                          {typeof t.availableQuantity === "number" && (
                            <div className="tb-ticket-avail">
                              {t.availableQuantity} left
                            </div>
                          )}
                        </div>
                        <div className="tb-ticket-price-wrap">
                          {t.originalPrice && t.originalPrice > t.price && (
                            <div className="tb-ticket-original">
                              {formatPrice(t.originalPrice)}
                            </div>
                          )}
                          <div className="tb-ticket-price">
                            {formatPrice(t.price)}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                </section>
              )}

              {event.refundPolicies?.length > 0 && (
                <section className="tb-detail-block">
                  <h2>Refund Policy</h2>
                  <div className="tb-refund-list">
                    {event.refundPolicies.map((p) => (
                      <div className="tb-refund-row" key={p.refundPolicyId}>
                        <div className="tb-refund-head">
                          <span className="tb-refund-name">{p.policyName}</span>
                          <span className="tb-refund-percent">
                            {p.refundPercent}% refund
                          </span>
                        </div>
                        {p.description && (
                          <p className="tb-refund-desc">{p.description}</p>
                        )}
                        <p className="tb-refund-deadline">
                          Applies up to {p.deadlineBeforeEventHours} hours before
                          the event starts.
                        </p>
                      </div>
                    ))}
                  </div>
                </section>
              )}

              <ReviewSection eventId={event.eventId} />
            </div>

            <aside className="tb-detail-side">
              <div className="tb-buy-card">
                <div className="tb-detail-row">
                  <svg viewBox="0 0 20 20" width="16" height="16" fill="none">
                    <rect
                      x="2.5"
                      y="4"
                      width="15"
                      height="13"
                      rx="2"
                      stroke="currentColor"
                      strokeWidth="1.4"
                    />
                    <path
                      d="M2.5 8h15M6.5 2.5v3M13.5 2.5v3"
                      stroke="currentColor"
                      strokeWidth="1.4"
                      strokeLinecap="round"
                    />
                  </svg>
                  {formatDateRange(event.startsAt, event.endsAt)}
                </div>

                {(event.locationName || event.city) && (
                  <div className="tb-detail-row">
                    <svg viewBox="0 0 20 20" width="16" height="16" fill="none">
                      <path
                        d="M10 18s6-5.686 6-10a6 6 0 1 0-12 0c0 4.314 6 10 6 10Z"
                        stroke="currentColor"
                        strokeWidth="1.4"
                      />
                      <circle
                        cx="10"
                        cy="8"
                        r="2.3"
                        stroke="currentColor"
                        strokeWidth="1.4"
                      />
                    </svg>
                    <span>
                      {event.locationName}
                      {event.address ? `, ${event.address}` : ""}
                      {event.city ? `, ${event.city}` : ""}
                    </span>
                  </div>
                )}

                {availableTickets !== null && (
                  <div className="tb-detail-row">
                    <svg viewBox="0 0 20 20" width="16" height="16" fill="none">
                      <path
                        d="M3 8a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v1a1.5 1.5 0 0 0 0 3v1a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1a1.5 1.5 0 0 0 0-3V8Z"
                        stroke="currentColor"
                        strokeWidth="1.4"
                      />
                    </svg>
                    {availableTickets} tickets available
                  </div>
                )}

                <div className="tb-detail-price">
                  Starting from <strong>{formatPrice(minPrice)}</strong>
                </div>

                <button type="button" className="tb-btn tb-btn-outline tb-wishlist-btn" onClick={toggleWishlist} disabled={wishlistLoading}>
                  {wishlistLoading ? "Updating..." : isWishlisted ? "♥ Wishlisted" : "♡ Add to Wishlist"}
                </button>
                {wishlistError && <div className="tb-buy-blocked">{wishlistError}</div>}

                {canBuy ? (
                  <button type="button" className="tb-btn tb-btn-primary tb-buy-btn">
                    Buy Tickets
                  </button>
                ) : (
                  <div className="tb-buy-blocked">
                    {buyBlockedMessage || "Tickets are not available right now."}
                  </div>
                )}
              </div>
            </aside>
          </div>

          {(relatedLoading || related.length > 0) && (
            <section className="tb-container tb-list-section">
              <div className="tb-list-header">
                <h2>🎟️ Other Events On Sale</h2>
              </div>
              {relatedLoading && <div className="tb-loading">Loading...</div>}
              {!relatedLoading && related.length > 0 && (
                <div className="tb-grid tb-grid-3">
                  {related.map((ev) => (
                    <EventCard key={ev.eventId} event={ev} />
                  ))}
                </div>
              )}
            </section>
          )}

          <div className="tb-container tb-detail-back">
            <Link to="/" className="tb-btn tb-btn-outline">
              ← Back to home
            </Link>
          </div>
        </>
      )}

      <Footer />
    </div>
  );
}
