import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import "./OrderDetailsPage.css";

function formatDate(value) {
  if (!value) return "";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";

  return date.toLocaleString("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function formatPrice(value) {
  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(value) || 0);
}

function normalizeStatus(status) {
  return String(status || "")
    .replace(/[\s_-]/g, "")
    .toLowerCase();
}

function getStatusCategory(status) {
  const normalized = normalizeStatus(status);

  if (["paid", "completed", "success", "succeeded"].includes(normalized)) {
    return "paid";
  }

  if (["pending", "processing", "awaitingpayment"].includes(normalized)) {
    return "pending";
  }

  if (
    ["cancelled", "canceled", "expired", "failed", "refunded"].includes(
      normalized
    )
  ) {
    return "cancelled";
  }

  return "other";
}

function parseSeatIds(value) {
  if (!value) return [];

  if (Array.isArray(value)) return value;

  try {
    const parsed = JSON.parse(value);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

export default function OrderDetailsPage() {
  const { orderId } = useParams();
  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadOrder() {
      try {
        setLoading(true);
        setError("");

        const response = await ticketApi.getOrderById(orderId);
        const data = response.data?.data ?? response.data;

        if (!data) {
          throw new Error("The order response is invalid.");
        }

        if (!cancelled) setOrder(data);
      } catch (err) {
        if (!cancelled) {
          setError(
            err.response?.data?.message || "Could not load this order."
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadOrder();

    return () => {
      cancelled = true;
    };
  }, [orderId]);

  const orderDetails = Array.isArray(order?.orderDetails)
    ? order.orderDetails
    : [];

  return (
    <>
      <Header />

      <main className="order-details-page">
        <div className="order-details-container">

          {loading && (
            <div className="order-details-state">Loading order details...</div>
          )}

          {!loading && error && (
            <div className="order-details-state order-details-error" role="alert">
              {error}
            </div>
          )}

          {!loading && !error && order && (
            <>
              <section className="order-details-hero">
                {order.posterUrl && (
                  <img
                    className="order-details-poster"
                    src={order.posterUrl}
                    alt=""
                  />
                )}

                <div className="order-details-event">
                  <p className="order-details-eyebrow">Order details</p>
                  <h1>{order.eventName || "Event"}</h1>

                  <span
                    className={`order-details-status ${getStatusCategory(
                      order.status
                    )}`}
                  >
                    {order.status}
                  </span>
                </div>
              </section>

              <section className="order-details-card">
                <header className="order-details-section-heading">
                  <div>
                    <p className="order-details-eyebrow">Your purchase</p>
                    <h2>Order summary</h2>
                  </div>
                </header>

                <div className="order-details-meta">
                  {formatDate(order.orderDate) && (
                    <div>
                      <span>Order date</span>
                      <strong>{formatDate(order.orderDate)}</strong>
                    </div>
                  )}

                  {order.paymentMethod && (
                    <div>
                      <span>Payment method</span>
                      <strong>{order.paymentMethod}</strong>
                    </div>
                  )}

                  {normalizeStatus(order.status) === "pending" &&
                    formatDate(order.expiresAt) && (
                      <div>
                        <span>Payment expires</span>
                        <strong>{formatDate(order.expiresAt)}</strong>
                      </div>
                    )}
                </div>

                <div className="order-details-ticket-list">
                  {orderDetails.map((detail, index) => {
                    const seats = parseSeatIds(detail.seatIds);

                    return (
                      <article
                        className="order-details-ticket"
                        key={detail.orderDetailId ?? `${detail.ticketTypeId}-${index}`}
                      >
                        <div className="order-details-ticket-main">
                          <div>
                            <h3>{detail.ticketTypeName || "Ticket"}</h3>
                            <p>
                              {detail.quantity}{" "}
                              {detail.quantity === 1 ? "ticket" : "tickets"}
                            </p>
                          </div>

                          <strong>
                            {formatPrice(
                              Number(detail.unitPrice) * Number(detail.quantity)
                            )}
                          </strong>
                        </div>

                        {seats.length > 0 && (
                          <div className="order-details-seats">
                            <span>Seats</span>
                            <div>
                              {seats.map((seatId) => (
                                <span className="order-details-seat" key={seatId}>
                                  {seatId}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}

                        <p className="order-details-unit-price">
                          {formatPrice(detail.unitPrice)} per ticket
                        </p>
                      </article>
                    );
                  })}
                </div>

                <div className="order-details-totals">
                  <div>
                    <span>Subtotal</span>
                    <strong>{formatPrice(order.totalAmount)}</strong>
                  </div>

                  {Number(order.discountAmount) > 0 && (
                    <div>
                      <span>Discount</span>
                      <strong className="order-details-discount">
                        −{formatPrice(order.discountAmount)}
                      </strong>
                    </div>
                  )}

                  <div className="order-details-grand-total">
                    <span>Total</span>
                    <strong>{formatPrice(order.finalAmount)}</strong>
                  </div>
                </div>
              </section>

              <div className="order-details-actions">
                <Link to="/my-tickets">Go to My Tickets</Link>
              </div>
            </>
          )}
        </div>
      </main>

      <Footer />
    </>
  );
}