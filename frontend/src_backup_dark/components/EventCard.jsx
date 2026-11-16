import { Link } from "react-router-dom";
import { formatDate, formatPrice } from "../utils/format";
import "./EventCard.css";

const FALLBACK_IMG =
  "data:image/svg+xml;utf8," +
  encodeURIComponent(
    `<svg xmlns='http://www.w3.org/2000/svg' width='400' height='260'><rect width='100%' height='100%' fill='#201a2b'/></svg>`
  );

export default function EventCard({ event }) {
  return (
    <Link to={`/events/${event.slug}`} className="tb-card">
      <div className="tb-card-img">
        <img
          src={event.posterUrl || FALLBACK_IMG}
          alt={event.title}
          loading="lazy"
          onError={(e) => {
            e.currentTarget.src = FALLBACK_IMG;
          }}
        />
        {event.isFeatured && <span className="tb-card-hot">🔥 HOT</span>}
      </div>
      <div className="tb-card-body">
        <span className="tb-card-tag">CONCERT</span>
        <h3 className="tb-card-title">{event.title}</h3>

        <div className="tb-card-meta">
          <div className="tb-card-date">
            <svg viewBox="0 0 20 20" width="14" height="14" fill="none">
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
            {formatDate(event.startsAt)}
          </div>

          {(event.locationName || event.city) && (
            <div className="tb-card-loc">
              <svg viewBox="0 0 20 20" width="14" height="14" fill="none">
                <path
                  d="M10 18s6-5.686 6-10a6 6 0 1 0-12 0c0 4.314 6 10 6 10Z"
                  stroke="currentColor"
                  strokeWidth="1.4"
                />
                <circle cx="10" cy="8" r="2.3" stroke="currentColor" strokeWidth="1.4" />
              </svg>
              <span>{event.locationName || event.city}</span>
            </div>
          )}
        </div>

        <div className="tb-card-price">From {formatPrice(event.minPrice)}</div>
      </div>
    </Link>
  );
}
