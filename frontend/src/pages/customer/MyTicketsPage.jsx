import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import "./MyTicketsPage.css";

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
  ];

  const refundedStatuses = [
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

export default function MyTicketsPage() {
  const [tickets, setTickets] = useState([]);
  const [activeTab, setActiveTab] = useState("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

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

      <Footer />
    </>
  );
}