import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import ticketReturnApi from "../../api/ticketReturnApi";
import { apiErrorMessage } from "../../utils/apiError";
import "./MyTicketsPage.css";
import "../../styles/reportPages.css";

// UC_9 (return a ticket): reason limits are the same as the server's.
const MIN_REASON = 10;
const MAX_REASON = 500;

const TABS = [
  { id: "all", label: "All" },
  { id: "success", label: "Successful" },
  { id: "processing", label: "Processing" },
  { id: "cancelled", label: "Cancelled" },
  { id: "returns", label: "Return requests" },
];

const normalize = (value) =>
  String(value || "").replace(/[\s_-]/g, "").toLowerCase();

function getTicketCategory(ticket) {
  const orderStatus = normalize(ticket.orderStatus);
  const ticketStatus = normalize(ticket.status);

  const processingStatuses = [
    "returnpending",
    "refundpending",
    "refundrequested",
    "pendingrefund",
    "processingrefund",
  ];

  const refundedStatuses = [
    "returned",
    "refunded",
    "refundcompleted",
    "cancelled",
    "canceled",
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
  if (normalize(ticket.status) === "returnpending") return "Return pending";
  if (normalize(ticket.status) === "returned") return "Returned";
  if (category === "success") return "Successful";
  if (category === "processing") return "Refund processing";
  if (category === "cancelled") return "Refunded";

  return ticket.orderStatus || ticket.status || "Status unavailable";
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

const requestBadge = (status) => {
  if (status === "Approved" || status === "Refunded") return "rp-badge rp-badge-active";
  if (status === "RefundFailed") return "rp-badge rp-badge-pending";
  if (status === "Pending") return "rp-badge rp-badge-pending";
  if (status === "Rejected" || status === "Cancelled") return "rp-badge rp-badge-bad";
  return "rp-badge";
};

const requestLabel = (status) =>
  ({ Pending: "Waiting for review", Approved: "Approved", Refunded: "Refunded", RefundFailed: "Approved · refund processing", Rejected: "Rejected", Cancelled: "Cancelled" }[status] || status);

export default function MyTicketsPage() {
  const [tickets, setTickets] = useState([]);
  const [activeTab, setActiveTab] = useState("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // UC_9: return information per ticket, plus the customer's return requests.
  const [returnInfo, setReturnInfo] = useState({});
  const [requests, setRequests] = useState([]);
  const [requestError, setRequestError] = useState("");
  const [toast, setToast] = useState(null);
  const [target, setTarget] = useState(null);
  const [reason, setReason] = useState("");
  const [reasonError, setReasonError] = useState("");
  const [busy, setBusy] = useState(false);

  // Return data is optional: a failure here must never hide the tickets themselves.
  const loadReturnData = async () => {
    setRequestError("");
    const [infoResult, listResult] = await Promise.allSettled([
      ticketReturnApi.getMyTickets(),
      ticketReturnApi.list(),
    ]);

    if (infoResult.status === "fulfilled") {
      const rows = infoResult.value.data?.data || [];
      setReturnInfo(Object.fromEntries(rows.map((row) => [row.ticketId, row])));
    } else {
      setReturnInfo({});
    }

    if (listResult.status === "fulfilled") {
      setRequests(listResult.value.data?.data || []);
    } else {
      setRequests([]);
      setRequestError(apiErrorMessage(listResult.reason, "Could not load return requests."));
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

      if (!cancelled) await loadReturnData();
    }

    loadTickets();

    return () => {
      cancelled = true;
    };
  }, []);

  const showToast = (message, type = "ok") => {
    setToast({ message, type });
    window.setTimeout(() => setToast(null), 4000);
  };

  const openReturn = (ticket) => {
    setTarget(ticket);
    setReason("");
    setReasonError("");
  };

  const closeReturn = () => {
    if (!busy) setTarget(null);
  };

  // Same limits as the server, so the customer gets the message before sending.
  const validateReason = (value) => {
    const text = value.trim();
    if (!text) return "Please tell us why you want to return this ticket.";
    if (text.length < MIN_REASON) return `The reason must be at least ${MIN_REASON} characters.`;
    if (text.length > MAX_REASON) return `The reason must be at most ${MAX_REASON} characters.`;
    return "";
  };

  const reloadAfterChange = async () => {
    try {
      const response = await ticketApi.getMyTickets();
      const data = response.data?.data ?? response.data;
      if (Array.isArray(data)) setTickets(data);
    } catch {
      /* keep the list we already have */
    }
    await loadReturnData();
  };

  // UC_9.1: submit a return request.
  const submitReturn = async () => {
    const problem = validateReason(reason);
    setReasonError(problem);
    if (problem) return;

    setBusy(true);
    try {
      await ticketReturnApi.submit(target.ticketId, reason.trim());
      setTarget(null);
      showToast("Return request submitted. We will review it soon.");
      setActiveTab("returns");
      await reloadAfterChange();
    } catch (err) {
      setReasonError(apiErrorMessage(err, "Could not submit the request."));
    } finally {
      setBusy(false);
    }
  };

  // UC_9.3: withdraw a pending request; the ticket becomes valid again.
  const cancelRequest = async (request) => {
    if (!window.confirm("Cancel this return request? Your ticket will be valid again.")) return;

    setBusy(true);
    try {
      await ticketReturnApi.cancel(request.ticketReturnRequestId);
      showToast("Return request cancelled.");
      await reloadAfterChange();
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not cancel the request."), "error");
    } finally {
      setBusy(false);
    }
  };

  const visibleTickets = useMemo(() => {
    if (activeTab === "all") return tickets;
    if (activeTab === "returns") return [];

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
    if (tabId === "returns") return requests.length;

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

          {!loading && activeTab === "returns" ? (
            requestError ? (
              <div className="mt-state is-error" role="alert">
                {requestError}
              </div>
            ) : requests.length === 0 ? (
              <div className="mt-state">
                <strong>You have not requested any returns.</strong>
                <p>Return requests you send will be tracked here.</p>
              </div>
            ) : (
              <section className="mt-orders" aria-label="Return requests">
                {requests.map((request) => (
                  <article className="mt-order" key={request.ticketReturnRequestId}>
                    <header className="mt-order-header">
                      <div>
                        <p className="mt-order-label">Return request</p>
                        <h2>{request.eventName || `Event #${request.eventId}`}</h2>
                        <p className="mt-order-dates">
                          <span>{request.ticketTypeName}</span>
                          <span aria-hidden="true">·</span>
                          <span>Requested: {formatDate(request.createdAt)}</span>
                          <span aria-hidden="true">·</span>
                          <span>Refund: {formatPrice(request.refundAmount)}</span>
                        </p>
                        <p className="mt-order-dates">
                          <span>Reason: {request.reason}</span>
                        </p>
                        {request.reviewNote && (
                          <p className="mt-order-dates">
                            <span>Review note: {request.reviewNote}</span>
                          </p>
                        )}
                      </div>
                      <div className="mt-ticket-actions">
                        <span className={requestBadge(request.status)}>
                          {requestLabel(request.status)}
                        </span>
                        {request.canCancel && (
                          <button
                            type="button"
                            className="mt-details-link"
                            disabled={busy}
                            onClick={() => cancelRequest(request)}
                          >
                            Cancel request
                          </button>
                        )}
                      </div>
                    </header>
                  </article>
                ))}
              </section>
            )
          ) : loading ? (
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

                    <span className="mt-order-count">
                      {order.tickets.length}{" "}
                      {order.tickets.length === 1 ? "ticket" : "tickets"}
                    </span>
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

                            {returnInfo[ticket.ticketId]?.canReturn && (
                              <button
                                type="button"
                                className="mt-details-link"
                                onClick={() =>
                                  openReturn({ ...ticket, ...returnInfo[ticket.ticketId] })
                                }
                              >
                                Return ticket
                              </button>
                            )}

                            {returnInfo[ticket.ticketId] &&
                              !returnInfo[ticket.ticketId].canReturn &&
                              returnInfo[ticket.ticketId].status === "Active" &&
                              !returnInfo[ticket.ticketId].latestReturn?.canCancel && (
                                <span className="mt-return-hint">
                                  Cannot return: {returnInfo[ticket.ticketId].returnMessage}
                                </span>
                              )}

                            {returnInfo[ticket.ticketId]?.latestReturn?.canCancel && (
                              <button
                                type="button"
                                className="mt-details-link"
                                disabled={busy}
                                onClick={() =>
                                  cancelRequest(returnInfo[ticket.ticketId].latestReturn)
                                }
                              >
                                Cancel return request
                              </button>
                            )}
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

      {target && (
        <div className="rp-modal-backdrop" onClick={closeReturn}>
          <div className="rp-modal" onClick={(event) => event.stopPropagation()}>
            <h3>Return this ticket</h3>
            <p className="rp-muted">
              {target.eventName} · {target.ticketTypeName}
              <br />
              Estimated refund: <strong>{formatPrice(target.refundEstimate)}</strong> (
              {Number(target.refundPercent ?? 100)}% of the amount you paid, per the
              organizer's refund policy). The ticket stops working as soon as you send
              the request, and works again if you cancel it.
            </p>
            <label className="ow-field">
              <span>Why do you want to return it?</span>
              <textarea
                rows={4}
                maxLength={MAX_REASON}
                value={reason}
                aria-invalid={reasonError ? "true" : "false"}
                onChange={(event) => setReason(event.target.value)}
              />
            </label>
            <div className="rp-counter">{reason.trim().length} / {MAX_REASON}</div>
            {reasonError && <div className="ow-field-error">{reasonError}</div>}
            <div className="rp-modal-actions">
              <button type="button" className="tb-btn tb-btn-outline" disabled={busy} onClick={closeReturn}>
                Keep ticket
              </button>
              <button type="button" className="tb-btn tb-btn-primary" disabled={busy} onClick={submitReturn}>
                {busy ? "Sending..." : "Send request"}
              </button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className={"rp-toast " + (toast.type === "error" ? "rp-toast-error" : "rp-toast-ok")}>
          {toast.message}
        </div>
      )}

      <Footer />
    </>
  );
}