import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import QRCode from "qrcode";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import "./TicketDetailsPage.css";

function getValidDate(value) {
  if (!value) return null;

  const date = new Date(value);

  // Ignore empty/default values such as year 1.
  if (Number.isNaN(date.getTime()) || date.getFullYear() < 2000) {
    return null;
  }

  return date;
}

function formatDate(value) {
  const date = getValidDate(value);
  if (!date) return null;

  return date.toLocaleString("en-US", {
    dateStyle: "full",
    timeStyle: "short",
  });
}

function formatPrice(value) {
  if (value == null) return null;

  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
}

function getTicketStatus(ticket) {
  const orderStatus = String(ticket.orderStatus || "").toLowerCase();
  const ticketStatus = String(ticket.status || "").toLowerCase();

  if (
    orderStatus.includes("refundpending") ||
    orderStatus.includes("refundrequested") ||
    ticketStatus.includes("refundpending")
  ) {
    return { label: "Refund processing", className: "processing" };
  }

  if (
    orderStatus.includes("refunded") ||
    orderStatus.includes("refundcompleted") ||
    ticketStatus.includes("cancel")
  ) {
    return { label: "Refunded", className: "cancelled" };
  }

  if (ticket.checkedInAt) {
    return { label: "Checked in", className: "checked-in" };
  }

  if (orderStatus === "paid" && ticketStatus === "active") {
    return { label: "Active", className: "active" };
  }

  return {
    label: ticket.status || ticket.orderStatus || "Pending",
    className: "other",
  };
}

function getSeatDescription(ticket) {
  const zone = ticket.zoneName || ticket.seatZoneName;
  const row = ticket.rowLabel || ticket.seatRowLabel;
  const seat = ticket.seatNumber || ticket.seatLabel;

  const description = [
    zone,
    row ? `Row ${row}` : null,
    seat ? `Seat ${seat}` : null,
  ]
    .filter(Boolean)
    .join(" · ");

  // Temporary fallback until the API returns human-readable seat details.
  return description || (ticket.seatId ? `Seat ID ${ticket.seatId}` : null);
}

export default function TicketDetailsPage() {
  const { ticketId } = useParams();

  const [ticket, setTicket] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [qrOpen, setQrOpen] = useState(false);
  const [qrImage, setQrImage] = useState("");
  const [qrExpiresAt, setQrExpiresAt] = useState(null);
  const [secondsLeft, setSecondsLeft] = useState(0);
  const [qrLoading, setQrLoading] = useState(false);
  const [qrError, setQrError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadTicket() {
      try {
        setLoading(true);
        setError("");

        const response = await ticketApi.getTicketDetails(ticketId);
        const data = response.data?.data ?? response.data;

        if (!data) {
          throw new Error("Ticket not found.");
        }

        if (!cancelled) setTicket(data);
      } catch (err) {
        if (!cancelled) {
          setError(
            err.response?.data?.message ||
              err.message ||
              "Could not load this ticket."
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadTicket();

    return () => {
      cancelled = true;
    };
  }, [ticketId]);

  const rotateQr = useCallback(async () => {
    if (!ticket) return;

    try {
      setQrLoading(true);
      setQrError("");

      const response = await ticketApi.rotateTicketQr(ticket.ticketId);
      const result = response.data?.data ?? response.data;

      if (!result?.qrToken) {
        throw new Error("The QR token was not returned.");
      }

      const expiresAt = Date.parse(result.expiresAtUtc);

      if (!Number.isFinite(expiresAt)) {
        throw new Error("The QR expiration time is invalid.");
      }

      const image = await QRCode.toDataURL(result.qrToken, {
        width: 340,
        margin: 2,
        errorCorrectionLevel: "H",
      });

      setQrImage(image);
      setQrExpiresAt(expiresAt);
    } catch (err) {
      setQrError(
        err.response?.data?.message ||
          err.message ||
          "Could not generate the QR code."
      );
    } finally {
      setQrLoading(false);
    }
  }, [ticket]);

  async function handleShowQr() {
    setQrImage("");
    setQrExpiresAt(null);
    setQrError("");
    setQrOpen(true);
    await rotateQr();
  }

  useEffect(() => {
    if (!qrOpen || !qrExpiresAt) return undefined;

    // Rotate 3 seconds before the backend-provided expiration time.
    const delay = Math.max(qrExpiresAt - Date.now() - 3000, 0);
    const timer = window.setTimeout(rotateQr, delay);

    return () => window.clearTimeout(timer);
  }, [qrOpen, qrExpiresAt, rotateQr]);

  useEffect(() => {
    if (!qrOpen || !qrExpiresAt) return undefined;

    const updateCountdown = () => {
      setSecondsLeft(
        Math.max(0, Math.ceil((qrExpiresAt - Date.now()) / 1000))
      );
    };

    updateCountdown();

    const timer = window.setInterval(updateCountdown, 1000);
    return () => window.clearInterval(timer);
  }, [qrOpen, qrExpiresAt]);

  if (loading) {
    return (
      <>
        <Header />
        <main className="ticket-detail-page">
          <div className="ticket-detail-state">Loading ticket...</div>
        </main>
        <Footer />
      </>
    );
  }

  if (error || !ticket) {
    return (
      <>
        <Header />
        <main className="ticket-detail-page">
          <div className="ticket-detail-state is-error" role="alert">
            <strong>Could not open this ticket</strong>
            <p>{error || "Ticket not found."}</p>
          </div>
        </main>
        <Footer />
      </>
    );
  }

  const status = getTicketStatus(ticket);
  const eventStart = formatDate(ticket.startsAt);
  const eventEnd = formatDate(ticket.endsAt);
  const purchaseDate = formatDate(ticket.orderDate);
  const amountPaid = formatPrice(ticket.unitPrice);
  const seatDescription = getSeatDescription(ticket);

  const venue =
    ticket.venueName ||
    ticket.locationName ||
    ticket.venue ||
    ticket.location;

  const address = ticket.address || ticket.fullAddress;

  const canShowQr =
    String(ticket.orderStatus || "").toLowerCase() === "paid" &&
    String(ticket.status || "").toLowerCase() === "active" &&
    !ticket.checkedInAt;

  const hasPurchaseInfo = Boolean(purchaseDate || amountPaid);

  return (
    <>
      <Header />

      <main className="ticket-detail-page">
        <div className="ticket-detail-container">

          <section className="ticket-event-card">
            {ticket.posterUrl && (
              <div className="ticket-event-poster">
                <img src={ticket.posterUrl} alt="" />
              </div>
            )}

            <div className="ticket-event-info">
              <p className="ticket-detail-eyebrow">Event</p>
              <h1>{ticket.eventName || "Event"}</h1>

              <div className="ticket-event-facts">
                {eventStart && (
                  <div className="ticket-event-fact">
                    <span className="ticket-fact-icon" aria-hidden="true">
                      ◷
                    </span>
                    <div>
                      <span>Date and time</span>
                      <strong>{eventStart}</strong>
                      {eventEnd && <small>Ends {eventEnd}</small>}
                    </div>
                  </div>
                )}

                {(venue || address) && (
                  <div className="ticket-event-fact">
                    <span className="ticket-fact-icon" aria-hidden="true">
                      ◎
                    </span>
                    <div>
                      <span>Venue</span>
                      {venue && <strong>{venue}</strong>}
                      {address && <small>{address}</small>}
                    </div>
                  </div>
                )}
              </div>
            </div>
          </section>

          <section className="ticket-detail-card">
            <header className="ticket-detail-header">
              <div>
                <p className="ticket-detail-eyebrow">Admission pass</p>
                <h2>Ticket details</h2>
              </div>

              <span className={`ticket-status ${status.className}`}>
                {status.label}
              </span>
            </header>

            <div className="ticket-detail-grid">
              {ticket.ticketTypeName && (
                <div className="ticket-detail-field">
                  <span>Ticket type</span>
                  <strong>{ticket.ticketTypeName}</strong>
                </div>
              )}

              {seatDescription && (
                <div className="ticket-detail-field">
                  <span>Seat / area</span>
                  <strong>{seatDescription}</strong>
                </div>
              )}

              {ticket.ownerName && (
                <div className="ticket-detail-field">
                  <span>Ticket holder</span>
                  <strong>{ticket.ownerName}</strong>
                </div>
              )}

              {ticket.checkedInAt && (
                <div className="ticket-detail-field">
                  <span>Checked in</span>
                  <strong>{formatDate(ticket.checkedInAt)}</strong>
                </div>
              )}
            </div>

            {hasPurchaseInfo && (
              <section className="ticket-purchase-panel">
                <h3>Purchase information</h3>

                <div className="ticket-purchase-grid">
                  {purchaseDate && (
                    <div className="ticket-detail-field">
                      <span>Purchased</span>
                      <strong>{purchaseDate}</strong>
                    </div>
                  )}

                  {amountPaid && (
                    <div className="ticket-detail-field">
                      <span>Ticket price</span>
                      <strong>{amountPaid}</strong>
                    </div>
                  )}
                </div>
              </section>
            )}

            <div className="ticket-detail-footer">
              <p>Show your QR code at the entrance to check in.</p>

              {canShowQr && (
                <button
                  type="button"
                  className="ticket-show-qr-button"
                  onClick={handleShowQr}
                >
                  Show QR Code
                </button>
              )}
            </div>
          </section>
        </div>
      </main>

      {qrOpen && (
        <div
          className="ticket-qr-overlay"
          onClick={() => setQrOpen(false)}
        >
          <section
            className="ticket-qr-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="ticket-qr-title"
            onClick={(event) => event.stopPropagation()}
          >
            <button
              type="button"
              className="ticket-qr-close"
              aria-label="Close QR code"
              onClick={() => setQrOpen(false)}
            >
              ×
            </button>

            <p className="ticket-detail-eyebrow">Entry pass</p>
            <h2 id="ticket-qr-title">Your ticket QR code</h2>
            <p className="ticket-qr-event">
              {ticket.eventName || "Event"}
            </p>

            {qrLoading && !qrImage ? (
              <div className="ticket-qr-placeholder">
                Generating QR code...
              </div>
            ) : (
              qrImage && (
                <img
                  className="ticket-qr-image"
                  src={qrImage}
                  alt="Ticket QR code"
                />
              )
            )}

            {qrError && (
              <p className="ticket-qr-error" role="alert">
                {qrError}
              </p>
            )}

            {qrImage && (
              <p className="ticket-qr-countdown">
                QR code updates automatically in {secondsLeft}s
              </p>
            )}

            <p className="ticket-qr-instruction">
              Keep this screen open while your ticket is being scanned.
            </p>
          </section>
        </div>
      )}

      <Footer />
    </>
  );
}