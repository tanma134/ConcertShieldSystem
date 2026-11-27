import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import ticketReturnApi from "../../api/ticketReturnApi";
import { REFUND_REQUEST_SUCCESS_MESSAGE } from "../../utils/refundPolicy";
import { useToast } from "../../components/ToastProvider";
import appealApi from "../../api/appealApi";
import EvidenceUploader from "../../components/EvidenceUploader";
import { VIETNAM_BANKS } from "../../constants/vietnamBanks";

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
  { id: "restricted", label: "Restricted" },
];

const normalize = (value) =>
  String(value || "").replace(/[\s_-]/g, "").toLowerCase();

function getTicketCategory(ticket, restricted = false) {
  const orderStatus = normalize(ticket.orderStatus);
  const ticketStatus = normalize(ticket.status);

  if (restricted || ticketStatus === "blocked") return "restricted";

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

  return isPaid && isUsable ? "success" : "processing";
}

function getStatusLabel(category, ticket) {
  if (category === "success") return "Successful";
  if (category === "processing") {
    const orderStatus = normalize(ticket.orderStatus);
    const ticketStatus = normalize(ticket.status);
    const refundProcessingStatuses = [
      "refundpending",
      "refundrequested",
      "pendingrefund",
      "processingrefund",
      "returnpending",
    ];

    if (
      refundProcessingStatuses.includes(orderStatus) ||
      refundProcessingStatuses.includes(ticketStatus)
    ) {
      return "Refund processing";
    }

    const paidStatuses = ["paid", "completed", "success", "succeeded"];
    if (ticket.orderStatus && !paidStatuses.includes(orderStatus)) {
      return ticket.orderStatus;
    }

    return ticket.status || ticket.orderStatus || "Processing";
  }
  if (category === "cancelled") return "Refunded";
  if (category === "restricted") return "Blocked";

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

function toLocalDateKey(value) {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
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
  const [restrictions, setRestrictions] = useState([]);
  const [activeTab, setActiveTab] = useState("all");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [batchModalOpen, setBatchModalOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [selectedTicketIds, setSelectedTicketIds] = useState([]);
  const [batchCategory, setBatchCategory] = useState("PersonalSchedule");
  const [batchReason, setBatchReason] = useState("");
  const [batchProofUrl, setBatchProofUrl] = useState("");
  const [bankAccountName, setBankAccountName] = useState("");
  const [bankAccountNumber, setBankAccountNumber] = useState("");
  const [bankName, setBankName] = useState("");
  const [submittingBatch, setSubmittingBatch] = useState(false);
  const [batchTouched, setBatchTouched] = useState({});
  const [cancellingReturnId, setCancellingReturnId] = useState(null);

  const handleOpenBatchModal = (order) => {
    const eligibleTickets = order.tickets.filter(
      (t) =>
        t.canReturn === true &&
        normalize(t.orderStatus) === "paid" &&
        normalize(t.status) === "active" &&
        !t.checkedInAt
    );
    if (eligibleTickets.length === 0) {
      toast.error(order.tickets.find((ticket) => ticket.returnMessage)?.returnMessage || "No eligible tickets found for refund in this order.");
      return;
    }
    setSelectedOrder(order);
    setSelectedTicketIds(eligibleTickets.map((t) => t.ticketId));
    setBatchCategory("PersonalSchedule");
    setBatchReason("");
    setBatchProofUrl("");
    setBankAccountName("");
    setBankAccountNumber("");
    setBankName("");
    setBatchModalOpen(true);
  };

  const toggleTicketSelection = (ticketId) => {
    setSelectedTicketIds((prev) =>
      prev.includes(ticketId)
        ? prev.filter((id) => id !== ticketId)
        : [...prev, ticketId]
    );
  };

  const getBatchFieldError = (field) => {
    if (!batchTouched[field]) return null;
    if (field === "batchReason") {
      if (!batchReason.trim()) return "Detailed explanation is required and cannot be empty.";
      if (batchReason.trim().length < 10) {
        return `Explanation must be at least 10 characters (currently ${batchReason.trim().length}/10).`;
      }
    }
    if (field === "bankName") {
      if (!bankName.trim()) return "Bank name is required and cannot be empty.";
    }
    if (field === "bankAccountNumber") {
      if (!bankAccountNumber.trim()) return "Bank account number is required and cannot be empty.";
      if (bankAccountNumber.trim().length < 6 || bankAccountNumber.trim().length > 20) {
        return `Account number must be between 6 and 20 digits (currently ${bankAccountNumber.trim().length} digits).`;
      }
    }
    if (field === "bankAccountName") {
      if (!bankAccountName.trim()) return "Account holder name is required and cannot be empty.";
      if (bankAccountName.trim().length < 2) return "Account holder name is too short.";
    }
    return null;
  };

  const handleSubmitBatchReturn = async () => {
    if (selectedTicketIds.length === 0) {
      toast.error("Please select at least one ticket to request a refund.");
      return;
    }

    setBatchTouched({
      batchReason: true,
      bankName: true,
      bankAccountNumber: true,
      bankAccountName: true,
    });

    const hasErrors =
      !batchReason.trim() ||
      batchReason.trim().length < 10 ||
      !bankName.trim() ||
      !bankAccountNumber.trim() ||
      bankAccountNumber.trim().length < 6 ||
      bankAccountNumber.trim().length > 20 ||
      !bankAccountName.trim() ||
      bankAccountName.trim().length < 2;

    if (hasErrors) {
      toast.error("Please fill in all required fields correctly.");
      return;
    }

    setSubmittingBatch(true);
    try {
      const categoryLabel = REFUND_CATEGORIES[batchCategory] || batchCategory;
      const fullReason = `[${categoryLabel}] ${batchReason.trim()}`;
      const proofUrl = batchProofUrl.trim() || null;
      const holderName = bankAccountName.trim().toUpperCase();
      const accNum = bankAccountNumber.trim();
      const bName = bankName.trim();

      await Promise.all(
        selectedTicketIds.map((ticketId) =>
          ticketReturnApi.submit(ticketId, fullReason, holderName, accNum, bName, proofUrl)
        )
      );

      toast.success(`${REFUND_REQUEST_SUCCESS_MESSAGE} (${selectedTicketIds.length} ticket(s).)`);
      setBatchModalOpen(false);
      window.location.reload();
    } catch (err) {
      const errMsg =
        err.response?.data?.message ||
        err.message ||
        "Could not submit refund request.";
      toast.error(errMsg);
    } finally {
      setSubmittingBatch(false);
    }
  };

  // Withdraw a return request that is still waiting for review. The backend allows this
  // only while the request is Pending, and it puts the ticket back to Active.
  const handleCancelReturn = async (requestId) => {
    if (!requestId || cancellingReturnId) return;
    setCancellingReturnId(requestId);
    try {
      await ticketReturnApi.cancel(requestId);
      toast.success("Return request cancelled. The ticket is active again.");
      await loadTicketsData();
    } catch (err) {
      const errMsg =
        err.response?.data?.message ||
        err.message ||
        "Could not cancel the return request.";
      toast.error(errMsg);
    } finally {
      setCancellingReturnId(null);
    }
  };

  // Shared loader so cancelling a return can refresh the list without a page reload.
  const loadTicketsData = useCallback(async (isCancelled = () => false) => {
    try {
      setLoading(true);
      setError("");

      const [response, returnResponse, decisionResponse] = await Promise.all([
        ticketApi.getMyTickets(),
        ticketReturnApi.getMyTickets().catch(() => ({ data: [] })),
        appealApi.getMyDecisions().catch(() => ({ data: [] })),
      ]);
      const data = response.data?.data ?? response.data;
      const returnData = returnResponse.data?.data ?? returnResponse.data;
      const returnByTicketId = new Map(
        (Array.isArray(returnData) ? returnData : []).map((item) => [Number(item.ticketId), item])
      );
      const enrichedData = Array.isArray(data)
        ? data.map((ticket) => {
            const policy = returnByTicketId.get(Number(ticket.ticketId));
            return policy ? { ...ticket, ...policy, orderStatus: ticket.orderStatus, unitPrice: ticket.unitPrice, orderDate: ticket.orderDate } : ticket;
          })
        : data;

      if (!Array.isArray(enrichedData)) {
        throw new Error("The ticket response is invalid.");
      }

      const decisionData = decisionResponse.data?.data ?? decisionResponse.data;
      if (!isCancelled()) {
        setTickets(enrichedData);
        setRestrictions(Array.isArray(decisionData) ? decisionData : []);
      }
    } catch (err) {
      if (!isCancelled()) {
        setError(
          err.response?.data?.message || "Could not load your tickets."
        );
      }
    } finally {
      if (!isCancelled()) setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    loadTicketsData(() => cancelled);

    return () => {
      cancelled = true;
    };
  }, [loadTicketsData]);

  // Active ticket blocks are joined from AdminAPI by ticket id. TicketAPI owns the
  // ticket status and AdminAPI owns the appeal, so the customer screen combines both.
  const restrictionByTicketId = useMemo(() => {
    const map = new Map();
    for (const d of restrictions) {
      if (d.ticketId == null) continue;
      const active = d.status === "Active" &&
        (!d.expiresAt || new Date(d.expiresAt).getTime() > Date.now());
      if (!active && d.existingAppealId == null) continue;
      map.set(Number(d.ticketId), d);
    }
    return map;
  }, [restrictions]);

  const categoryOf = (ticket) =>
    getTicketCategory(ticket, restrictionByTicketId.has(Number(ticket.ticketId)));

  const dateFilteredTickets = useMemo(() => {
    let filteredTickets = tickets;
    if (dateFrom || dateTo) {
      filteredTickets = filteredTickets.filter((ticket) => {
        const eventDate = toLocalDateKey(ticket.startsAt);
        return eventDate && (!dateFrom || eventDate >= dateFrom) && (!dateTo || eventDate <= dateTo);
      });
    }
    return filteredTickets;
  }, [tickets, dateFrom, dateTo]);

  const visibleTickets = useMemo(() => {
    if (activeTab === "all") return dateFilteredTickets;

    return dateFilteredTickets.filter((ticket) => categoryOf(ticket) === activeTab);
  }, [dateFilteredTickets, activeTab, restrictionByTicketId]);

  const visibleOrders = useMemo(
    () => groupTicketsByOrder(visibleTickets),
    [visibleTickets]
  );

  function getTabCount(tabId) {
    if (tabId === "all") return dateFilteredTickets.length;

    return dateFilteredTickets.filter((ticket) => categoryOf(ticket) === tabId).length;
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

          <div className="mt-toolbar">
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

            <div className="mt-date-filter" aria-label="Filter by event date">
              <label>
                <span>From</span>
                <input type="date" value={dateFrom} max={dateTo || undefined} onChange={(event) => setDateFrom(event.target.value)} />
              </label>
              <label>
                <span>To</span>
                <input type="date" value={dateTo} min={dateFrom || undefined} onChange={(event) => setDateTo(event.target.value)} />
              </label>
              {(dateFrom || dateTo) && (
                <button type="button" onClick={() => { setDateFrom(""); setDateTo(""); }}>
                  Clear
                </button>
              )}
            </div>
          </div>

          {loading ? (
            <div className="mt-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
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
                      {(() => {
                          const eligibleCount = order.tickets.filter(
                            (t) =>
                              t.canReturn === true &&
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
                      const restriction = restrictionByTicketId.get(Number(ticket.ticketId));
                      const category = getTicketCategory(ticket, !!restriction);

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

                          {restriction && (
                            <div className="mt-restriction-note">
                              {restriction.existingAppealId ? (
                                <span>
                                  Appeal #{restriction.existingAppealId}: <b>{restriction.existingAppealStatus}</b>
                                </span>
                              ) : (
                                <span>
                                  Under risk review
                                  {restriction.appealDeadlineAt
                                    ? ` · appeal by ${formatDate(restriction.appealDeadlineAt)}`
                                    : ""}
                                </span>
                              )}
                              {(restriction.canAppeal || restriction.existingAppealId != null) && (
                                <Link
                                  className="mt-appeal-link"
                                  to={`/risk/decisions/${restriction.riskDecisionId}/appeal`}
                                >
                                  {restriction.existingAppealId ? "View appeal" : "Submit appeal"}
                                </Link>
                              )}
                            </div>
                          )}

                          {ticket.latestReturn && (
                            <div className="mt-return-status">
                              <span className="mt-return-label">Return request</span>
                              <span className={`mt-return-badge status-${normalize(ticket.latestReturn.status)}`}>
                                {ticket.latestReturn.status}
                              </span>
                              {ticket.latestReturn.refundAmount > 0 && (
                                <span className="mt-return-amount">
                                  {formatPrice(ticket.latestReturn.refundAmount)}
                                </span>
                              )}
                              {ticket.latestReturn.refundReference && (
                                <span className="mt-return-ref">
                                  Ref: {ticket.latestReturn.refundReference}
                                </span>
                              )}
                              {ticket.latestReturn.reviewNote && (
                                <span className="mt-return-note">
                                  Note: {ticket.latestReturn.reviewNote}
                                </span>
                              )}
                              {ticket.latestReturn.canCancel && (
                                <button
                                  type="button"
                                  className="mt-return-cancel"
                                  disabled={cancellingReturnId === ticket.latestReturn.ticketReturnRequestId}
                                  onClick={() => handleCancelReturn(ticket.latestReturn.ticketReturnRequestId)}
                                >
                                  {cancellingReturnId === ticket.latestReturn.ticketReturnRequestId
                                    ? "Cancelling…"
                                    : "Cancel return request"}
                                </button>
                              )}
                            </div>
                          )}
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

      {/* Batch Return Modal */}
      {batchModalOpen && selectedOrder && (
        <div
          className="bm-overlay"
          onClick={() => !submittingBatch && setBatchModalOpen(false)}
        >
          <div className="bm-card" style={{ maxWidth: "600px", maxHeight: "90vh", display: "flex", flexDirection: "column", overflow: "hidden", padding: 0 }} onClick={(e) => e.stopPropagation()}>
            <div className="bm-header" style={{ padding: "16px 22px 14px", borderBottom: "1px solid #f1f5f9", flexShrink: 0, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <div>
                <h3 style={{ margin: 0, fontSize: "17px", fontWeight: 700, color: "#1e1b2e" }}>
                  Batch Ticket Refund - Order #{selectedOrder.orderId}
                </h3>
                <p style={{ margin: "2px 0 0", fontSize: "13px", color: "#6b6480" }}>
                  {selectedOrder.eventName}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setBatchModalOpen(false)}
                disabled={submittingBatch}
                style={{
                  background: "#f8fafc",
                  border: "1px solid #e2e8f0",
                  fontSize: "12px",
                  fontWeight: 600,
                  color: "#64748b",
                  cursor: "pointer",
                  padding: "5px 12px",
                  borderRadius: "8px"
                }}
              >
                Close
              </button>
            </div>

            <div style={{ padding: "16px 22px", overflowY: "auto", flex: 1 }}>
              <div style={{ marginBottom: "14px" }}>
                <label style={{ display: "block", marginBottom: "6px", fontSize: "13px", fontWeight: 600, color: "#374151" }}>
                  1. Select tickets to refund in this order:
                </label>

                <div className="bm-ticket-list" style={{ maxHeight: "160px", overflowY: "auto" }}>
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

                <div style={{ fontSize: "12px", color: "#7e22ce", fontWeight: 600, marginTop: "6px" }}>
                  Selected: {selectedTicketIds.length} ticket(s) for refund
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
                  onBlur={() => setBatchTouched((prev) => ({ ...prev, batchReason: true }))}
                  onChange={(e) => {
                    setBatchReason(e.target.value);
                    setBatchTouched((prev) => ({ ...prev, batchReason: true }));
                  }}
                  style={{
                    width: "100%",
                    padding: "9px 12px",
                    borderRadius: "8px",
                    border: getBatchFieldError("batchReason") ? "1px solid #e11d48" : "1px solid #d1d5db",
                    boxShadow: getBatchFieldError("batchReason") ? "0 0 0 2px rgba(225, 29, 72, 0.15)" : "none",
                    fontSize: "13px",
                    outline: "none",
                    resize: "vertical",
                    boxSizing: "border-box",
                  }}
                />
                {getBatchFieldError("batchReason") && (
                  <span style={{ display: "block", color: "#e11d48", fontSize: "11.5px", marginTop: "4px", fontWeight: 500 }}>
                    {getBatchFieldError("batchReason")}
                  </span>
                )}
              </div>

              {/* Shared Evidence Uploader */}
              <EvidenceUploader
                value={batchProofUrl}
                onChange={setBatchProofUrl}
                maxSizeMb={8}
                label="4. Supporting Document / Evidence"
              />

              {/* Bank Account Destination */}
              <div style={{ background: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "10px", padding: "12px", marginTop: "12px" }}>
                <strong style={{ display: "block", fontSize: "13px", color: "#0f172a", marginBottom: "2px" }}>
                  Bank Account for Refund
                </strong>
                <p style={{ margin: "0 0 10px", fontSize: "11.5px", color: "#64748b" }}>
                  Approved refunds will be transferred manually to this account
                </p>

                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px" }}>
                  <div>
                    <label style={{ display: "block", fontSize: "11.5px", fontWeight: 600, color: "#475569", marginBottom: "4px" }}>
                      Bank Name *
                    </label>
                    <input
                      list="batch-bank-list"
                      style={{
                        width: "100%",
                        padding: "8px 10px",
                        borderRadius: "6px",
                        border: getBatchFieldError("bankName") ? "1px solid #e11d48" : "1px solid #cbd5e1",
                        boxShadow: getBatchFieldError("bankName") ? "0 0 0 2px rgba(225, 29, 72, 0.15)" : "none",
                        fontSize: "13px",
                        boxSizing: "border-box"
                      }}
                      placeholder="Select bank..."
                      value={bankName}
                      onBlur={() => setBatchTouched((prev) => ({ ...prev, bankName: true }))}
                      onChange={(e) => {
                        setBankName(e.target.value);
                        setBatchTouched((prev) => ({ ...prev, bankName: true }));
                      }}
                    />
                    <datalist id="batch-bank-list">
                      {VIETNAM_BANKS.map((b) => (
                        <option key={b} value={b} />
                      ))}
                    </datalist>
                    {getBatchFieldError("bankName") && (
                      <span style={{ color: "#e11d48", fontSize: "11px", fontWeight: 500, marginTop: "2px", display: "block" }}>
                        {getBatchFieldError("bankName")}
                      </span>
                    )}
                  </div>

                  <div>
                    <label style={{ display: "block", fontSize: "11.5px", fontWeight: 600, color: "#475569", marginBottom: "4px" }}>
                      Account Number *
                    </label>
                    <input
                      type="password"
                      autoComplete="off"
                      style={{
                        width: "100%",
                        padding: "8px 10px",
                        borderRadius: "6px",
                        border: getBatchFieldError("bankAccountNumber") ? "1px solid #e11d48" : "1px solid #cbd5e1",
                        boxShadow: getBatchFieldError("bankAccountNumber") ? "0 0 0 2px rgba(225, 29, 72, 0.15)" : "none",
                        fontSize: "13px",
                        boxSizing: "border-box"
                      }}
                      placeholder="e.g. 1029384756..."
                      value={bankAccountNumber}
                      onBlur={() => setBatchTouched((prev) => ({ ...prev, bankAccountNumber: true }))}
                      onChange={(e) => {
                        setBankAccountNumber(e.target.value.replace(/[^0-9A-Za-z]/g, ""));
                        setBatchTouched((prev) => ({ ...prev, bankAccountNumber: true }));
                      }}
                      maxLength={20}
                    />
                    {getBatchFieldError("bankAccountNumber") && (
                      <span style={{ color: "#e11d48", fontSize: "11px", fontWeight: 500, marginTop: "2px", display: "block" }}>
                        {getBatchFieldError("bankAccountNumber")}
                      </span>
                    )}
                  </div>

                  <div style={{ gridColumn: "1 / -1" }}>
                    <label style={{ display: "block", fontSize: "11.5px", fontWeight: 600, color: "#475569", marginBottom: "4px" }}>
                      Account Holder Name (Uppercase) *
                    </label>
                    <input
                      style={{
                        width: "100%",
                        padding: "8px 10px",
                        borderRadius: "6px",
                        border: getBatchFieldError("bankAccountName") ? "1px solid #e11d48" : "1px solid #cbd5e1",
                        boxShadow: getBatchFieldError("bankAccountName") ? "0 0 0 2px rgba(225, 29, 72, 0.15)" : "none",
                        fontSize: "13px",
                        boxSizing: "border-box"
                      }}
                      placeholder="e.g. NGUYEN VAN A"
                      value={bankAccountName}
                      onBlur={() => setBatchTouched((prev) => ({ ...prev, bankAccountName: true }))}
                      onChange={(e) => {
                        setBankAccountName(e.target.value.toUpperCase());
                        setBatchTouched((prev) => ({ ...prev, bankAccountName: true }));
                      }}
                      maxLength={100}
                    />
                    {getBatchFieldError("bankAccountName") && (
                      <span style={{ color: "#e11d48", fontSize: "11px", fontWeight: 500, marginTop: "2px", display: "block" }}>
                        {getBatchFieldError("bankAccountName")}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "14px 22px", borderTop: "1px solid #f1f5f9", background: "#ffffff", flexShrink: 0 }}>
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
                disabled={submittingBatch || selectedTicketIds.length === 0}
                style={{
                  padding: "9px 20px",
                  borderRadius: "8px",
                  border: "none",
                  background: submittingBatch || selectedTicketIds.length === 0 ? "#e2e8f0" : "#7e22ce",
                  color: submittingBatch || selectedTicketIds.length === 0 ? "#94a3b8" : "#ffffff",
                  fontSize: "13px",
                  fontWeight: 600,
                  cursor: submittingBatch || selectedTicketIds.length === 0 ? "not-allowed" : "pointer",
                }}
              >
                {submittingBatch ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : `Confirm Refund (${selectedTicketIds.length} tickets)`}
              </button>
            </div>
          </div>
        </div>
      )}

      <Footer />
    </>
  );
}
