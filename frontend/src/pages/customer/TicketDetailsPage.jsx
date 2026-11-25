import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import QRCode from "qrcode";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import ticketReturnApi from "../../api/ticketReturnApi";
import { useToast } from "../../components/ToastProvider";
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
    ticketStatus.includes("refundpending") ||
    ticketStatus.includes("returnpending")
  ) {
    return { label: "Refund processing", className: "processing" };
  }

  if (
    orderStatus.includes("refunded") ||
    orderStatus.includes("refundcompleted") ||
    ticketStatus.includes("cancel") ||
    ticketStatus.includes("returned") ||
    ticketStatus.includes("revoked")
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

function cleanEventTitle(title) {
  if (!title) return "Event";
  return String(title)
    .replace(/\s*-\s*offline\s*sync\s*demo/gi, "")
    .replace(/\s*offline\s*sync\s*demo/gi, "")
    .replace(/\s*offlinesync\s*demo/gi, "")
    .trim();
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

function getHistoryActionLabel(item) {
  const labels = {
    HoldCreated:
      item.targetType === "Seat" ? "Seat held" : "Tickets held",
    HoldReleased: "Hold released",
    OrderCreated: "Order created",
    PaymentSucceeded: "Payment successful",
    InventorySold: "Ticket inventory sold",
    SeatSold: "Seat sold",
    TicketIssued: "Ticket issued",
    TicketCheckedIn: "Ticket checked in",
    ReturnRequested: "Ticket return requested",
    ReturnApproved: "Ticket return approved",
    TicketRevoked: "Ticket revoked",
    TicketReissued: "Ticket reissued",
  };

  return (
    labels[item.action] ||
    item.action?.replace(/([a-z])([A-Z])/g, "$1 $2") ||
    "Status updated"
  );
}

export default function TicketDetailsPage() {
  const { ticketId } = useParams();

  const [ticket, setTicket] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [history, setHistory] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(true);
  const [historyError, setHistoryError] = useState("");

  const toast = useToast();
  const [returnModalOpen, setReturnModalOpen] = useState(false);
  const [returnCategory, setReturnCategory] = useState("PersonalSchedule");
  const [returnReason, setReturnReason] = useState("");
  const [returnProofUrl, setReturnProofUrl] = useState("");
  const [submittingReturn, setSubmittingReturn] = useState(false);

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

  useEffect(() => {
    let cancelled = false;

    async function loadTicketHistory() {
      try {
        setHistoryLoading(true);
        setHistoryError("");
        setHistory([]);

        const response = await ticketApi.getTicketHistory(ticketId);
        const data = response.data?.data ?? response.data;

        if (!Array.isArray(data)) {
          throw new Error("The ticket history response is invalid.");
        }

        if (!cancelled) {
          setHistory(data);
        }
      } catch (err) {
        if (!cancelled) {
          setHistoryError(
            err.response?.data?.message ||
              err.message ||
              "Could not load ticket history."
          );
        }
      } finally {
        if (!cancelled) {
          setHistoryLoading(false);
        }
      }
    }

    if (ticketId) {
      loadTicketHistory();
    } else {
      setHistoryLoading(false);
    }

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
          <div className="ticket-detail-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
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

  // Close return window if event starts within 24 hours (or in the past)
  const isReturnWindowOpen =
    ticket.startsAt &&
    new Date(ticket.startsAt).getTime() - Date.now() >= 24 * 3600 * 1000;

  const canRequestReturn =
    String(ticket.orderStatus || "").toLowerCase() === "paid" &&
    String(ticket.status || "").toLowerCase() === "active" &&
    !ticket.checkedInAt &&
    isReturnWindowOpen;

  const REFUND_CATEGORIES = {
    PersonalSchedule: "Personal schedule conflict / Unable to attend",
    MedicalEmergency: "Medical emergency / Health issue / Hospitalization",
    EventChanged: "Event schedule or venue changed",
    AccidentalBooking: "Accidental booking / Wrong tier or quantity",
    ForceMajeure: "Severe weather / Natural disaster / Travel disruption",
    Other: "Other reason (detailed explanation required)",
  };

  const handleSubmitReturn = async () => {
    if (returnReason.trim().length < 10) {
      toast.error("Please enter a detailed explanation of at least 10 characters.");
      return;
    }
    setSubmittingReturn(true);
    try {
      const categoryLabel = REFUND_CATEGORIES[returnCategory] || returnCategory;
      const fullReason = `[${categoryLabel}] ${returnReason.trim()}`;
      const proofUrl = returnProofUrl.trim() || null;
      await ticketReturnApi.submit(ticket.ticketId, fullReason, proofUrl);
      toast.success("Refund request submitted successfully! Staff will review shortly.");
      setReturnModalOpen(false);
      window.location.reload();
    } catch (err) {
      const errMsg = err.response?.data?.message || err.message || "Could not submit refund request.";
      toast.error(errMsg);
    } finally {
      setSubmittingReturn(false);
    }
  };

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
              <h1>{cleanEventTitle(ticket.eventName)}</h1>

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

              <div className="ticket-detail-field">
                <span>Check-in Gate{ticket.permittedGates && ticket.permittedGates.length > 1 ? "s" : ""}</span>
                {ticket.permittedGates && ticket.permittedGates.length > 0 ? (
                  <div style={{ display: "flex", flexWrap: "wrap", gap: "6px", marginTop: "4px" }}>
                    {ticket.permittedGates.map((g, idx) => (
                      <span
                        key={idx}
                        style={{
                          display: "inline-block",
                          background: "rgba(236, 72, 153, 0.12)",
                          color: "#ff2d75",
                          border: "1px solid rgba(236, 72, 153, 0.3)",
                          padding: "3px 10px",
                          borderRadius: "6px",
                          fontSize: "0.88rem",
                          fontWeight: 600,
                        }}
                      >
                        {g}
                      </span>
                    ))}
                  </div>
                ) : (
                  <strong>
                    {ticket.gateName || ticket.gateId || "Main Gate"}
                  </strong>
                )}
              </div>

              {ticket.checkedInGate && (
                <div className="ticket-detail-field">
                  <span>Admitted Gate</span>
                  <strong style={{ color: "#10b981" }}>{ticket.checkedInGate}</strong>
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

              <div style={{ display: "flex", gap: "10px", flexWrap: "wrap", justifyContent: "flex-end", alignItems: "center" }}>
                {ticket.orderId && (
                  <Link
                    to={`/my-orders/${ticket.orderId}`}
                    className="ticket-help-link"
                    title="Get help or dispute issues regarding this order"
                  >
                    💬 Need Help?
                  </Link>
                )}
                {canRequestReturn && (
                  <button
                    type="button"
                    className="ticket-refund-btn"
                    onClick={() => setReturnModalOpen(true)}
                  >
                    ↺ Request Ticket Refund
                  </button>
                )}
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
            </div>
          </section>

          <section
            className="ticket-history-card"
            aria-labelledby="ticket-history-title"
          >
            <header className="ticket-history-header">
              <div>
                <p className="ticket-detail-eyebrow">Ticket activity</p>
                <h2 id="ticket-history-title">Status history</h2>
              </div>
            </header>

            {historyLoading ? (
              <p className="ticket-history-state">
                <span className="app-loading-spinner" role="status" aria-label="Loading" /></p>


            ) : historyError ? (
              <p className="ticket-history-state is-error" role="alert">
                {historyError}
              </p>
            ) : history.length === 0 ? (
              <p className="ticket-history-state">
                No status history is available for this ticket yet.
              </p>
            ) : (
              <ol className="ticket-history-list">
                {history.map((item, index) => (
                  <li
                    className="ticket-history-item"
                    key={
                      item.statusHistoryId ??
                      `${item.action}-${item.createdAt}-${index}`
                    }
                  >
                    <span
                      className="ticket-history-marker"
                      aria-hidden="true"
                    />

                    <div className="ticket-history-content">
                      <div className="ticket-history-main">
                        <strong>{getHistoryActionLabel(item)}</strong>
                        <time dateTime={item.createdAt}>
                          {formatDate(item.createdAt) || "Time unavailable"}
                        </time>
                      </div>

                      {(item.oldStatus || item.newStatus) && (
                        <p className="ticket-history-status">
                          {item.oldStatus || "-"}
                          {" → "}
                          {item.newStatus || "-"}
                        </p>
                      )}

                      {item.seatId && (
                        <p className="ticket-history-meta">
                          Seat ID {item.seatId}
                        </p>
                      )}

                      {item.quantity != null && (
                        <p className="ticket-history-meta">
                          Quantity: {item.quantity}
                        </p>
                      )}

                      {item.reason && (
                        <p className="ticket-history-meta">{item.reason}</p>
                      )}
                    </div>
                  </li>
                ))}
              </ol>
            )}
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
              {cleanEventTitle(ticket.eventName)}
            </p>

            {qrLoading && !qrImage ? (
              <div className="ticket-qr-placeholder">
                <span className="app-loading-spinner" role="status" aria-label="Loading" />
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

      {returnModalOpen && (
        <div className="ticket-modal-overlay" onClick={() => !submittingReturn && setReturnModalOpen(false)}>
          <div className="ticket-modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="ticket-modal-header">
              <div className="ticket-modal-icon">↺</div>
              <div>
                <h3 className="ticket-modal-title">Ticket Refund Request</h3>
                <p className="ticket-modal-subtitle">Step 11 · Customer ticket refund submission</p>
              </div>
            </div>

            <div className="ticket-modal-info-box">
              <div className="ticket-modal-info-row">
                <span>Event</span>
                <strong>{ticket.eventName}</strong>
              </div>
              <div className="ticket-modal-info-row">
                <span>Ticket Type</span>
                <strong>{ticket.ticketTypeName} ({formatPrice(ticket.unitPrice || 2500000)})</strong>
              </div>
              <div className="ticket-modal-info-row">
                <span>Payment Method</span>
                <strong>{ticket.paymentMethod || "VNPay"}</strong>
              </div>
              <div className="ticket-modal-policy-badge">
                ✓ 100% Refund Eligible (Policy Active)
              </div>
            </div>

            <div style={{ marginBottom: "14px" }}>
              <label className="ticket-modal-label" style={{ marginBottom: "6px", display: "block" }}>
                <span>Refund Reason Category *</span>
              </label>
              <select
                className="ticket-modal-select"
                value={returnCategory}
                onChange={(e) => setReturnCategory(e.target.value)}
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  borderRadius: "8px",
                  border: "1px solid #d1d5db",
                  background: "#ffffff",
                  fontSize: "13px",
                  fontWeight: 500,
                  color: "#1f2937",
                  outline: "none",
                }}
              >
                <option value="PersonalSchedule">Personal schedule conflict / Unable to attend</option>
                <option value="MedicalEmergency">Medical emergency / Health issue / Hospitalization</option>
                <option value="EventChanged">Event schedule or venue changed</option>
                <option value="AccidentalBooking">Accidental booking / Wrong tier or quantity</option>
                <option value="ForceMajeure">Severe weather / Natural disaster / Travel disruption</option>
                <option value="Other">Other reason (detailed explanation required)</option>
              </select>
            </div>

            <label className="ticket-modal-label">
              <span>Detailed Explanation / Notes *</span>
              <span className="ticket-modal-char-count">
                {returnReason.trim().length}/10 min characters
              </span>
            </label>
            <textarea
              className="ticket-modal-textarea"
              rows={3}
              placeholder="Please describe your reason clearly to help staff review and approve your refund..."
              value={returnReason}
              onChange={(e) => setReturnReason(e.target.value)}
            />

            <div style={{ marginTop: "12px", marginBottom: "14px" }}>
              <label className="ticket-modal-label" style={{ marginBottom: "6px", display: "flex", justifyContent: "space-between" }}>
                <span>Supporting Document / Evidence Link</span>
                <span style={{ fontSize: "12px", color: "#6b7280" }}>(Optional)</span>
              </label>
              <input
                type="url"
                placeholder="https://... (Link to doctor's note, travel disruption notice...)"
                value={returnProofUrl}
                onChange={(e) => setReturnProofUrl(e.target.value)}
                style={{
                  width: "100%",
                  padding: "9px 12px",
                  borderRadius: "8px",
                  border: "1px solid #d1d5db",
                  background: "#ffffff",
                  fontSize: "13px",
                  color: "#1f2937",
                  outline: "none",
                }}
              />
              <small style={{ display: "block", marginTop: "4px", fontSize: "11px", color: "#6b7280" }}>
                Providing valid evidence helps expedite the review process for medical or force majeure reasons.
              </small>
            </div>

            <div className="ticket-modal-actions">
              <button
                type="button"
                className="ticket-modal-cancel-btn"
                onClick={() => setReturnModalOpen(false)}
                disabled={submittingReturn}
              >
                Cancel
              </button>
              <button
                type="button"
                className="ticket-modal-submit-btn"
                disabled={submittingReturn || returnReason.trim().length < 10}
                onClick={handleSubmitReturn}
              >
                {submittingReturn ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Submit Refund Request"}
              </button>
            </div>
          </div>
        </div>
      )}

      <Footer />
    </>
  );
}
