import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import ticketApi from "../../api/ticketApi";
import "./PaymentResult.css";
import Header from "../../components/Header";
import Footer from "../../components/Footer";

const formatCurrency = (amount) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
  }).format(Number(amount) || 0);

export default function PaymentResult() {
  const [searchParams] = useSearchParams();
  const orderId = searchParams.get("orderId");

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadOrder() {
      if (!orderId) {
        setError("Order information is missing.");
        setLoading(false);
        return;
      }

      try {
        setLoading(true);
        setError("");

        const response = await ticketApi.getOrderById(orderId);
        const data = response.data?.data ?? response.data;

        if (!cancelled) setOrder(data);
      } catch (err) {
        if (!cancelled) {
          setError(
            err.response?.data?.message ||
              "Could not retrieve your order status."
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

  if (loading) {
    return (
      <main className="payment-result-page">
        <section className="payment-result-card">
          <div className="payment-result-spinner" />
          <h1>Checking your payment</h1>
          <p>Please wait while we retrieve your order status.</p>
        </section>
      </main>
    );
  }

  if (error) {
    return (
      <main className="payment-result-page">
        <section className="payment-result-card is-error">
          <div className="payment-result-icon">!</div>
          <p className="payment-result-eyebrow">CONCERTSHIELD CHECKOUT</p>
          <h1>Could not check payment</h1>
          <p>{error}</p>

          {orderId && <p className="payment-result-order-id">Order #{orderId}</p>}

          <div className="payment-result-actions">
            <Link to="/" className="payment-result-button">
              Back to events
            </Link>
          </div>
        </section>
      </main>
    );
  }

  const status = String(order?.status || "").toLowerCase();
  const isPaid = ["paid", "completed", "success", "succeeded"].includes(status);
  const isFailed = ["failed", "cancelled", "canceled", "expired"].includes(
    status
  );

  const result = isPaid
    ? {
        className: "is-success",
        icon: "✓",
        title: "Payment successful",
        message: "Your payment has been confirmed and your order is complete.",
      }
    : isFailed
      ? {
          className: "is-failed",
          icon: "×",
          title: "Payment not completed",
          message: "This order was not marked as paid.",
        }
      : {
          className: "is-pending",
          icon: "…",
          title: "Payment is pending",
          message: "Your payment has not been confirmed yet. Please check again shortly.",
        };

  return (
    <>
    <Header />
    <main className="payment-result-page">
      <section className={`payment-result-card ${result.className}`}>
        <div className="payment-result-icon" aria-hidden="true">
          {result.icon}
        </div>

        <p className="payment-result-eyebrow">CONCERTSHIELD CHECKOUT</p>
        <h1>{result.title}</h1>
        <p className="payment-result-message">{result.message}</p>

        <dl className="payment-result-details">
          <div>
            <dt>Order ID</dt>
            <dd>{order?.orderId ?? orderId}</dd>
          </div>

          {order?.eventName && (
            <div>
              <dt>Event</dt>
              <dd>{order.eventName}</dd>
            </div>
          )}

          {order?.finalAmount != null && (
            <div>
              <dt>Total</dt>
              <dd>{formatCurrency(order.finalAmount)}</dd>
            </div>
          )}

          {order?.status && (
            <div>
              <dt>Order status</dt>
              <dd className="payment-result-status">{order.status}</dd>
            </div>
          )}
        </dl>

        <div className="payment-result-actions">
          {!isPaid && (
            <button
              type="button"
              className="payment-result-button is-secondary"
              onClick={() => window.location.reload()}
            >
              Check again
            </button>
          )}

          <Link to="/" className="payment-result-button">
            Back to events
          </Link>
        </div>
      </section>
    </main>
    <Footer />
  </>
  );
}