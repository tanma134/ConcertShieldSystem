import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import supportCaseApi from "../../api/supportCaseApi";
import "./MyOrdersPage.css";

const TABS = [
  { id: "all", label: "All orders" },
  { id: "pending", label: "Pending" },
  { id: "paid", label: "Paid" },
  { id: "cancelled", label: "Cancelled" },
  { id: "reported", label: "Reported" },
];

function normalizeStatus(status) {
  return String(status || "")
    .replace(/[\s_-]/g, "")
    .toLowerCase();
}

function getOrderCategory(order) {
  const status = normalizeStatus(order.status);

  if (["pending", "processing", "awaitingpayment"].includes(status)) {
    return "pending";
  }

  if (["paid", "completed", "success", "succeeded"].includes(status)) {
    return "paid";
  }

  if (["cancelled", "canceled", "expired", "failed", "refunded"].includes(status)) {
    return "cancelled";
  }

  return "other";
}

function getStatusLabel(order) {
  const category = getOrderCategory(order);

  if (category === "pending") return "Pending";
  if (category === "paid") return "Paid";
  if (category === "cancelled") return "Cancelled";

  return order.status || "Other";
}

function formatDate(value) {
  if (!value) return "";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

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
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
}

export default function MyOrdersPage() {
  const [orders, setOrders] = useState([]);
  const [reportedOrderIds, setReportedOrderIds] = useState([]);
  const [activeTab, setActiveTab] = useState("all");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reportedLoadError, setReportedLoadError] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadOrders() {
      try {
        setLoading(true);
        setError("");

        const [ordersResult, reportsResult] = await Promise.allSettled([
          ticketApi.getMyOrders(),
          supportCaseApi.getMyReportedOrders(),
        ]);
        if (ordersResult.status === "rejected") throw ordersResult.reason;

        const response = ordersResult.value;
        const data = response.data?.data ?? response.data;

        if (!Array.isArray(data)) {
          throw new Error("The order response is invalid.");
        }

        if (!cancelled) {
          setOrders(data);
          if (reportsResult.status === "fulfilled") {
            const reportedIds = reportsResult.value.data?.data ?? reportsResult.value.data;
            if (Array.isArray(reportedIds)) {
              setReportedOrderIds(reportedIds.map(Number));
              setReportedLoadError(false);
            } else {
              setReportedLoadError(true);
            }
          } else {
            setReportedLoadError(true);
          }
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err.response?.data?.message || "Could not load your orders."
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadOrders();

    return () => {
      cancelled = true;
    };
  }, []);

  const dateFilteredOrders = useMemo(() => {
    let filteredOrders = orders;
    if (dateFrom || dateTo) {
      filteredOrders = filteredOrders.filter((order) => {
        const orderDate = toLocalDateKey(order.orderDate);
        return orderDate && (!dateFrom || orderDate >= dateFrom) && (!dateTo || orderDate <= dateTo);
      });
    }
    return filteredOrders;
  }, [orders, dateFrom, dateTo]);

  const visibleOrders = useMemo(() => {
    if (activeTab === "all") return dateFilteredOrders;
    if (activeTab === "reported") {
      const reportedIds = new Set(reportedOrderIds);
      return dateFilteredOrders.filter((order) => reportedIds.has(Number(order.orderId)));
    }

    return dateFilteredOrders.filter((order) => getOrderCategory(order) === activeTab);
  }, [dateFilteredOrders, activeTab, reportedOrderIds]);

  function getTabCount(tabId) {
    if (tabId === "all") return dateFilteredOrders.length;
    if (tabId === "reported") {
      if (reportedLoadError) return "-";
      const reportedIds = new Set(reportedOrderIds);
      return dateFilteredOrders.filter((order) => reportedIds.has(Number(order.orderId))).length;
    }

    return dateFilteredOrders.filter((order) => getOrderCategory(order) === tabId).length;
  }

  return (
    <>
      <Header />

      <main className="my-orders-page">
        <div className="my-orders-container">
          <header className="my-orders-heading">
            <div>
              <p className="my-orders-eyebrow">My Account</p>
              <h1>Order History</h1>
              <p>View your purchases and track each order’s status.</p>
            </div>

            <span className="my-orders-total">
              {orders.length} {orders.length === 1 ? "order" : "orders"}
            </span>
          </header>

          <div className="my-orders-toolbar">
            <nav className="my-orders-tabs" aria-label="Filter orders">
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

            <div className="my-orders-date-filter" aria-label="Filter by order date">
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

          {loading && (
            <div className="my-orders-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
          )}

          {!loading && error && (
            <div className="my-orders-state my-orders-error" role="alert">
              {error}
            </div>
          )}

          {!loading && !error && activeTab === "reported" && reportedLoadError && (
            <div className="my-orders-state my-orders-error" role="alert">
              Could not load reported orders. Please try again later.
            </div>
          )}

          {!loading && !error && (activeTab !== "reported" || !reportedLoadError) && visibleOrders.length === 0 && (
            <div className="my-orders-state">
              <h2>
                {orders.length === 0
                  ? "No orders yet"
                  : activeTab === "reported"
                    ? "No reported orders"
                    : "No orders in this category"}
              </h2>
              <p>
                {orders.length === 0
                  ? "Your event purchases will appear here."
                  : activeTab === "reported"
                    ? "Orders with a support report will appear here."
                    : "Try selecting another status filter."}
              </p>
              {orders.length === 0 && (
                <Link className="my-orders-browse-link" to="/">
                  Browse events
                </Link>
              )}
            </div>
          )}

          {!loading && !error && (activeTab !== "reported" || !reportedLoadError) && visibleOrders.length > 0 && (
            <section className="my-orders-list" aria-label="Your orders">
              {visibleOrders.map((order) => {
                const category = getOrderCategory(order);
                const orderDate = formatDate(order.orderDate);
                const eventDate = formatDate(order.startsAt);

                return (
                  <article className="my-order-card" key={order.orderId}>
                    <div className="my-order-header">
                      <div className="my-order-event">
                        {order.posterUrl ? (
                          <img
                            className="my-order-poster"
                            src={order.posterUrl}
                            alt=""
                          />
                        ) : (
                          <div
                            className="my-order-poster my-order-poster-empty"
                            aria-hidden="true"
                          >
                            Event
                          </div>
                        )}

                        <div className="my-order-title-block">
                          <p className="my-order-eyebrow">Event</p>
                          <h2>{order.eventName || "Event"}</h2>
                        </div>
                      </div>

                      <span className={`my-order-status ${category}`}>
                        {getStatusLabel(order)}
                      </span>
                    </div>

                    <div className="my-order-info">
                      {eventDate && (
                        <div>
                          <span>Event date</span>
                          <strong>{eventDate}</strong>
                        </div>
                      )}

                      {orderDate && (
                        <div>
                          <span>Order date</span>
                          <strong>{orderDate}</strong>
                        </div>
                      )}

                      <div>
                        <span>Tickets</span>
                        <strong>
                          {order.ticketCount}{" "}
                          {order.ticketCount === 1 ? "ticket" : "tickets"}
                        </strong>
                      </div>

                      {order.paymentMethod && (
                        <div>
                          <span>Payment method</span>
                          <strong>{order.paymentMethod}</strong>
                        </div>
                      )}
                    </div>

                    <div className="my-order-footer">
                      <div className="my-order-amount">
                        <span>Total paid</span>
                        <strong>{formatPrice(order.finalAmount)}</strong>
                      </div>

                      {/* Add the order detail route when that page is implemented. */}
                      <Link
                        className="my-order-details-link"
                        to={`/my-orders/${order.orderId}`}
                        >
                        View order details
                    </Link>
                    </div>
                  </article>
                );
              })}
            </section>
          )}
        </div>
      </main>

      <Footer />
    </>
  );
}
