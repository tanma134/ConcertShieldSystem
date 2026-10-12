import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import ticketApi from "../../api/ticketApi";
import paymentApi from "../../api/paymentApi";
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
  // PaymentAPI adds these when VNPay sends the customer back.
  const returnStatus = searchParams.get("status"); // success | pending | failed | invalid
  const vnpCode = searchParams.get("code");
  const returnReason = searchParams.get("reason");

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [retrying, setRetrying] = useState(false);
  const [retryMessage, setRetryMessage] = useState("");

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

  // Ask the server to confirm the payment again, then reload the order if it worked.
  const handleCheckAgain = async () => {
    setRetrying(true);
    setRetryMessage("");
    try {
      const response = await paymentApi.reconcile(orderId);
      const result = response.data?.data ?? response.data;
      if (result?.success) {
        window.location.reload();
        return;
      }
      setRetryMessage(result?.message || "The payment could not be confirmed yet.");
    } catch (err) {
      setRetryMessage(err.response?.data?.message || "Could not reach the payment service.");
    } finally {
      setRetrying(false);
    }
  };

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

  const vnpMessages = {
    "24": "You cancelled the payment at VNPay.",
    "51": "The card or account does not have enough balance.",
    "65": "The daily transaction limit was exceeded.",
    "75": "The bank is under maintenance.",
    "11": "The payment session at VNPay timed out.",
  };

  let result;
  if (isPaid) {
    result = {
      className: "is-success",
      icon: "✓",
      title: "Payment successful",
      message: "Your payment has been confirmed and your order is complete.",
    };
  } else if (returnStatus === "invalid") {
    result = {
      className: "is-failed",
      icon: "×",
      title: "Payment could not be verified",
      message: "The response from VNPay failed our security check, so the order was not marked as paid.",
    };
  } else if (isFailed || returnStatus === "failed") {
    result = {
      className: "is-failed",
      icon: "×",
      title: "Payment not completed",
      message:
        (vnpCode && vnpMessages[vnpCode]) ||
        returnReason ||
        (vnpCode ? `VNPay did not complete the payment (code ${vnpCode}).` : "This order was not marked as paid."),
    };
  } else {
    result = {
      className: "is-pending",
      icon: "…",
      title: "Payment is pending",
      message: returnStatus === "pending"
        ? `VNPay received your payment, but we could not confirm the order yet.${returnReason ? " Reason: " + returnReason : ""}`
        : "Your payment has not been confirmed yet. Please check again shortly.",
    };
  }

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
        {retryMessage && !isPaid && (
          <p className="payment-result-message" role="alert">
            {retryMessage}
          </p>
        )}

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
              disabled={retrying}
              onClick={handleCheckAgain}
            >
              {retrying ? "Checking..." : "Check again"}
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