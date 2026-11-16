import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import ticketReturnApi from "../../api/ticketReturnApi";
import { useToast } from "../../components/ToastProvider";
import "./MyTicketsPage.css";

const REFUND_CATEGORIES = {
  PersonalSchedule: "Personal schedule conflict / Unable to attend",
  MedicalEmergency: "Medical emergency / Health issue / Hospitalization",
  EventChanged: "Event schedule or venue changed",
  AccidentalBooking: "Accidental booking / Wrong tier or quantity",
  ForceMajeure: "Severe weather / Natural disaster / Travel disruption",
  Other: "Other reason (detailed explanation required)",
};

const TABS = [
  { id: "all", label: "All" },
  { id: "success", label: "Successful" },
  { id: "processing", label: "Processing" },
  { id: "cancelled", label: "Cancelled" },
];

const normalize = (value) =>
  String(value || "").replace(/[\s_-]/g, "").toLowerCase();

function getTicketCategory(ticket) {
  const orderStatus = normalize(ticket.orderStatus);
  const ticketStatus = normalize(ticket.status);

  const processingStatuses = [
    "refundpending",
    "refundrequested",
    "pendingrefund",
    "processingrefund",
    "returnpending",
  ];

  const refundedStatuses = [
    "refunded",
    "refundcompleted",
    "cancelled",
    "canceled",
    "returned",
    "revoked",
  ];

  if (
    processingStatuses.includes(orderStatus) ||
    processingStatuses.includes(ticketStatus)
  ) {
    return "processing";
  }

  if (
    refundedStatuses.includes(orderStatus) ||
    refundedStatuses.includes(ticketStatus)
  ) {
    return "cancelled";
  }

  const isPaid = ["paid", "completed", "success", "succeeded"].includes(
    orderStatus
  );
  const isUsable = ["active", "used", "checkedin"].includes(ticketStatus);

  return isPaid && isUsable ? "success" : "other";
}

function getStatusLabel(category, ticket) {
  if (category === "success") return "Successful";
  if (category === "processing") return "Refund processing";
  if (category === "cancelled") return "Refunded";

  return ticket.status || ticket.orderStatus || "Status unavailable";
}

function formatDate(value) {
  if (!value) return "Not available";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Not available";

  return date.toLocaleString("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function formatPrice(value) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
}

function groupTicketsByOrder(tickets) {
  const groups = new Map();

  for (const ticket of tickets) {
    // Used internally for grouping only. The order ID is not shown in the UI.
    if (!groups.has(ticket.orderId)) {
      groups.set(ticket.orderId, {
        orderId: ticket.orderId,
        eventId: ticket.eventId,
        eventName: ticket.eventName,
        orderDate: ticket.orderDate,
        startsAt: ticket.startsAt,
        tickets: [],
      });
    }

    groups.get(ticket.orderId).tickets.push(ticket);
  }

  return [...groups.values()];
}

export default function MyTicketsPage() {
  const toast = useToast();
  const [tickets, setTickets] = useState([]);
  const [activeTab, setActiveTab] = useState("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [batchModalOpen, setBatchModalOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [selectedTicketIds, setSelectedTicketIds] = useState([]);
  const [batchCategory, setBatchCategory] = useState("PersonalSchedule");
  const [batchReason, setBatchReason] = useState("");
  const [batchProofUrl, setBatchProofUrl] = useState("");
  const [submittingBatch, setSubmittingBatch] = useState(false);

  const handleOpenBatchModal = (order) => {
    // Check if event cutoff window passed (< 24h)
    const isWindowOpen =
      order.startsAt &&
      new Date(order.startsAt).getTime() - Date.now() >= 24 * 3600 * 1000;

    if (!isWindowOpen) {
      toast.error("Refund window has closed (returns close 24 hours before showtime).");
      return;
    }

    const eligibleTickets = order.tickets.filter(
      (t) =>
        normalize(t.orderStatus) === "paid" &&
        normalize(t.status) === "active" &&
        !t.checkedInAt
    );
    if (eligibleTickets.length === 0) {
      toast.error("No eligible tickets found for refund in this order.");
      return;
    }
    setSelectedOrder(order);
    setSelectedTicketIds(eligibleTickets.map((t) => t.ticketId));
    setBatchCategory("PersonalSchedule");
    setBatchReason("");
    setBatchProofUrl("");
    setBatchModalOpen(true);
  };

  const toggleTicketSelection = (ticketId) => {
    setSelectedTicketIds((prev) =>
      prev.includes(ticketId)
        ? prev.filter((id) => id !== ticketId)
        : [...prev, ticketId]
    );
  };

  const handleSubmitBatchReturn = async () => {
    if (selectedTicketIds.length === 0) {
      toast.error("Please select at least one ticket to request a refund.");
      return;
    }
    if (batchReason.trim().length < 10) {
      toast.error("Please enter a detailed explanation of at least 10 characters.");
      return;
    }

    setSubmittingBatch(true);
    try {
      const categoryLabel = REFUND_CATEGORIES[batchCategory] || batchCategory;
      const fullReason = `[${categoryLabel}] ${batchReason.trim()}`;
      const proofUrl = batchProofUrl.trim() || null;

      await ticketReturnApi.submitBatch(selectedTicketIds, fullReason, proofUrl);
      toast.success(
        `Refund request submitted successfully for ${selectedTicketIds.length} ticket(s)! The ticket QR codes have been deactivated.`
      );
      setBatchModalOpen(false);
      window.location.reload();
    } catch (err) {
      const errMsg =
        err.response?.data?.message ||
        err.message ||
        "Could not submit batch refund request.";
      toast.error(errMsg);
    } finally {
      setSubmittingBatch(false);
    }
  };

  useEffect(() => {
    let cancelled = false;

    async function loadTickets() {
      try {
        setLoading(true);
        setError("");

        const response = await ticketApi.getMyTickets();
        const data = response.data?.data ?? response.data;

        if (!Array.isArray(data)) {
          throw new Error("The ticket response is invalid.");
        }

        if (!cancelled) setTickets(data);
      } catch (err) {
        if (!cancelled) {
          setError(
            err.response?.data?.message || "Could not load your tickets."
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadTickets();

    return () => {
      cancelled = true;
    };
  }, []);

  const visibleTickets = useMemo(() => {
    if (activeTab === "all") return tickets;

    return tickets.filter(
      (ticket) => getTicketCategory(ticket) === activeTab
    );
  }, [tickets, activeTab]);

  const visibleOrders = useMemo(
    () => groupTicketsByOrder(visibleTickets),
    [visibleTickets]
  );

  function getTabCount(tabId) {
    if (tabId === "all") return tickets.length;

    return tickets.filter(
      (ticket) => getTicketCategory(ticket) === tabId
    ).length;
  }

  return (
    <>
      <Header />

      <main className="mt-page">
        <div className="mt-container">
          <header className="mt-heading">
            <div>
              <p className="mt-eyebrow">My Account</p>
              <h1>My Tickets</h1>
              <p>View your tickets and manage your event bookings.</p>
            </div>

            <span className="mt-total">
              {tickets.length} {tickets.length === 1 ? "ticket" : "tickets"}
            </span>
          </header>

          <nav className="mt-tabs" aria-label="Filter tickets">
            {TABS.map((tab) => (
              <button
                type="button"
                key={tab.id}
                className={activeTab === tab.id ? "is-active" : ""}
                aria-pressed={activeTab === tab.id}
                onClick={() => setActiveTab(tab.id)}
              >
                {tab.label}
                <span>{getTabCount(tab.id)}</span>
              </button>
            ))}
          </nav>

          {loading ? (
            <div className="mt-state">Loading tickets...</div>
          ) : error ? (
            <div className="mt-state is-error" role="alert">
              {error}
            </div>
          ) : visibleOrders.length === 0 ? (
            <div className="mt-state">
              <strong>
                {activeTab === "all"
                  ? "You do not have any tickets yet."
                  : "There are no tickets in this category."}
              </strong>
              <p>Your purchased tickets will appear here.</p>
            </div>
          ) : (
            <section className="mt-orders" aria-label="Ticket orders">
              {visibleOrders.map((order) => (
                <article className="mt-order" key={order.orderId}>
                  <header className="mt-order-header">
                    <div>
                      <p className="mt-order-label">Order</p>
                      <h2>
                        {order.eventName || `Event #${order.eventId}`}
                      </h2>
                      <p className="mt-order-dates">
                        <span>Event date: {formatDate(order.startsAt)}</span>
                        <span aria-hidden="true">·</span>
                        <span>Purchased: {formatDate(order.orderDate)}</span>
                      </p>
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                      {order.startsAt &&
                        new Date(order.startsAt).getTime() - Date.now() >= 24 * 3600 * 1000 &&
                        (() => {
                          const eligibleCount = order.tickets.filter(
                            (t) =>
                              normalize(t.orderStatus) === "paid" &&
                              normalize(t.status) === "active" &&
                              !t.checkedInAt
                          ).length;
                          if (eligibleCount === 0) return null;
                          return (
                            <button
                              type="button"
                              className="mt-batch-refund-btn"
                              onClick={() => handleOpenBatchModal(order)}
                              title="Request refund for tickets in this order"
                            >
                              ↺ Request Order Refund ({eligibleCount})
                            </button>
                          );
                        })()}
                      <span className="mt-order-count">
                        {order.tickets.length}{" "}
                        {order.tickets.length === 1 ? "ticket" : "tickets"}
                      </span>
                    </div>
                  </header>

                  <div className="mt-order-tickets">
                    {order.tickets.map((ticket) => {
                      const category = getTicketCategory(ticket);

                      return (
                        <article className="mt-ticket" key={ticket.ticketId}>
                          <div className="mt-ticket-poster">
                            {ticket.posterUrl ? (
                              <img src={ticket.posterUrl} alt="" />
                            ) : (
                              <span>
                                LIVE
                                <br />
                                EVENT
                              </span>
                            )}
                          </div>

                          <div className="mt-ticket-content">
                            <h3>
                              {ticket.ticketTypeName || "Event ticket"}
                            </h3>
                            <p className="mt-ticket-event">
                              {ticket.eventName ||
                                `Event #${ticket.eventId}`}
                            </p>

                            <div className="mt-ticket-info">
                              <span>
                                {ticket.seatId
                                  ? `Seat ${ticket.seatId}`
                                  : "General admission"}
                              </span>

                              {ticket.ownerName && (
                                <span>
                                  Ticket holder: {ticket.ownerName}
                                </span>
                              )}

                              <span>
                                Event date: {formatDate(ticket.startsAt)}
                              </span>

                              <strong>{formatPrice(ticket.unitPrice)}</strong>
                            </div>
                          </div>

                          <div className="mt-ticket-actions">
                            <span className={`mt-status ${category}`}>
                              {getStatusLabel(category, ticket)}
                            </span>

                            <Link
                              className="mt-details-link"
                              to={`/my-tickets/${ticket.ticketId}`}
                            >
                              View ticket details
                            </Link>
                          </div>
                        </article>
                      );
                    })}
                  </div>
                </article>
              ))}
            </section>
          )}
        </div>
      </main>

      {/* Modal Hoàn Vé Theo Lô (Batch Return Modal) */}
      {batchModalOpen && selectedOrder && (
        <div
          className="bm-overlay"
          onClick={() => !submittingBatch && setBatchModalOpen(false)}
        >
          <div className="bm-card" onClick={(e) => e.stopPropagation()}>
            <div className="bm-header">
              <div className="bm-icon">↺</div>
              <div>
                <h3 style={{ margin: 0, fontSize: "17px", fontWeight: 700, color: "#1e1b2e" }}>
                  Batch Ticket Refund — Order #{selectedOrder.orderId}
                </h3>
                <p style={{ margin: "2px 0 0", fontSize: "13px", color: "#6b6480" }}>
                  {selectedOrder.eventName}
                </p>
              </div>
            </div>

            <div style={{ marginTop: "14px" }}>
              <label style={{ display: "block", marginBottom: "6px", fontSize: "13px", fontWeight: 600, color: "#374151" }}>
                1. Select tickets to refund in this order:
              </label>

              <div className="bm-ticket-list">
                {selectedOrder.tickets.map((t) => {
                  const isEligible =
                    normalize(t.orderStatus) === "paid" &&
                    normalize(t.status) === "active" &&
                    !t.checkedInAt;
                  const isSelected = selectedTicketIds.includes(t.ticketId);

                  return (
                    <div
                      key={t.ticketId}
                      className={`bm-ticket-row ${isSelected ? "is-selected" : ""} ${
                        !isEligible ? "is-disabled" : ""
                      }`}
                      onClick={() => isEligible && toggleTicketSelection(t.ticketId)}
                    >
                      <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                        <input
                          type="checkbox"
                          checked={isSelected}
                          disabled={!isEligible}
                          onChange={() => isEligible && toggleTicketSelection(t.ticketId)}
                          style={{ cursor: isEligible ? "pointer" : "not-allowed", width: "16px", height: "16px" }}
                        />
                        <div>
                          <div style={{ fontSize: "13px", fontWeight: 600, color: "#1f2937" }}>
                            Ticket #{t.ticketId} · {t.ticketTypeName || "Ticket"}
                          </div>
                          <div style={{ fontSize: "12px", color: "#6b7280" }}>
                            {t.seatId ? `Seat ${t.seatId}` : "General Admission"} · {formatPrice(t.unitPrice)}
                          </div>
                        </div>
                      </div>

                      {!isEligible && (
                        <span style={{ fontSize: "11px", padding: "2px 8px", borderRadius: "4px", background: "#fee2e2", color: "#991b1b" }}>
                          {t.checkedInAt ? "Checked-in" : t.status}
                        </span>
                      )}
                    </div>
                  );
                })}
              </div>

              <div style={{ fontSize: "12px", color: "#7e22ce", fontWeight: 600, marginBottom: "12px" }}>
                ✓ {selectedTicketIds.length} ticket(s) selected for refund
              </div>
            </div>

            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "block", marginBottom: "6px", fontSize: "13px", fontWeight: 600, color: "#374151" }}>
                2. Refund Reason Category:
              </label>
              <select
                value={batchCategory}
                onChange={(e) => setBatchCategory(e.target.value)}
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
              >
                {Object.entries(REFUND_CATEGORIES).map(([key, label]) => (
                  <option key={key} value={key}>
                    {label}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ marginBottom: "12px" }}>
              <label style={{ display: "flex", justifyContent: "space-between", marginBottom: "6px", fontSize: "13px", fontWeight: 600, color: "#374151" }}>
                <span>3. Detailed Explanation / Notes *</span>
                <span style={{ fontSize: "12px", color: "#9ca3af", fontWeight: "normal" }}>
                  {batchReason.trim().length}/10 min characters
                </span>
              </label>
              <textarea
                rows={2}
                placeholder="Please describe your reason clearly to help staff verify..."
                value={batchReason}
                onChange={(e) => setBatchReason(e.target.value)}
                style={{
                  width: "100%",
                  padding: "9px 12px",
                  borderRadius: "8px",
                  border: "1px solid #d1d5db",
                  fontSize: "13px",
                  outline: "none",
                  resize: "vertical",
                }}
              />
            </div>

            <div style={{ marginBottom: "16px" }}>
              <label style={{ display: "flex", justifyContent: "space-between", marginBottom: "6px", fontSize: "13px", fontWeight: 600, color: "#374151" }}>
                <span>4. Supporting Document / Evidence Link</span>
                <span style={{ fontSize: "12px", color: "#6b7280", fontWeight: "normal" }}>(Optional)</span>
              </label>
              <input
                type="url"
                placeholder="https://... (Link to doctor's note, travel disruption notice...)"
                value={batchProofUrl}
                onChange={(e) => setBatchProofUrl(e.target.value)}
                style={{
                  width: "100%",
                  padding: "8px 12px",
                  borderRadius: "8px",
                  border: "1px solid #d1d5db",
                  background: "#ffffff",
                  fontSize: "13px",
                  outline: "none",
                }}
              />
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
              <button
                type="button"
                onClick={() => setBatchModalOpen(false)}
                disabled={submittingBatch}
                style={{
                  padding: "9px 18px",
                  borderRadius: "8px",
                  border: "1px solid #cbd5e1",
                  background: "#ffffff",
                  color: "#475569",
                  fontSize: "13px",
                  fontWeight: 600,
                  cursor: "pointer",
                }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleSubmitBatchReturn}
                disabled={submittingBatch || selectedTicketIds.length === 0 || batchReason.trim().length < 10}
                style={{
                  padding: "9px 20px",
                  borderRadius: "8px",
                  border: "none",
                  background:
                    submittingBatch || selectedTicketIds.length === 0 || batchReason.trim().length < 10
                      ? "#e2e8f0"
                      : "#7e22ce",
                  color:
                    submittingBatch || selectedTicketIds.length === 0 || batchReason.trim().length < 10
                      ? "#94a3b8"
                      : "#ffffff",
                  fontSize: "13px",
                  fontWeight: 600,
                  cursor:
                    submittingBatch || selectedTicketIds.length === 0 || batchReason.trim().length < 10
                      ? "not-allowed"
                      : "pointer",
                }}
              >
                {submittingBatch ? "Submitting request..." : `Confirm Refund (${selectedTicketIds.length} tickets)`}
              </button>
            </div>
          </div>
        </div>
      )}

      <Footer />
    </>
  );
}