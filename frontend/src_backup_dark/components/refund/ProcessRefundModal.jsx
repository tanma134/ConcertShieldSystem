import { useState, useEffect } from "react";
import refundApi from "../../api/refundApi";
import { formatPrice } from "../../utils/format";
import "./refundModals.css";

export default function ProcessRefundModal({
  orderId,
  initialCustomerName,
  onClose,
  onSuccess,
}) {
  const [loadingOrder, setLoadingOrder] = useState(true);
  const [orderSummary, setOrderSummary] = useState(null);
  const [refundType, setRefundType] = useState("Full"); // "Full" | "Partial"
  const [amount, setAmount] = useState("");
  const [reason, setReason] = useState("Customer Request");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    async function fetchOrder() {
      if (!orderId) return;
      try {
        setLoadingOrder(true);
        setError("");
        const res = await refundApi.getOrderSummary(orderId);
        const data = res.data?.data || res.data;
        if (active) {
          setOrderSummary(data);
          const max = data.maxRefundableAmount || data.originalAmount || 0;
          setAmount(max.toString());
        }
      } catch (err) {
        if (active) {
          setError(
            err.response?.data?.message ||
              "Could not load order payment details."
          );
        }
      } finally {
        if (active) setLoadingOrder(false);
      }
    }
    fetchOrder();
    return () => {
      active = false;
    };
  }, [orderId]);

  const handleTypeChange = (type) => {
    setRefundType(type);
    setError("");
    if (type === "Full" && orderSummary) {
      setAmount((orderSummary.maxRefundableAmount || 0).toString());
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    const numericAmount = parseInt(amount, 10);
    if (isNaN(numericAmount) || numericAmount <= 0) {
      setError("Please enter a valid refund amount.");
      return;
    }

    const max = orderSummary?.maxRefundableAmount || 0;
    if (numericAmount > max) {
      // EX-01: Refund Amount Exceeds Limit
      setError("Refund amount exceeds the maximum refundable amount.");
      return;
    }

    try {
      setSubmitting(true);
      const payload = {
        orderId: parseInt(orderId, 10),
        refundType,
        amount: numericAmount,
        reason,
      };

      const res = await refundApi.processRefund(payload);
      const data = res.data?.data || res.data;
      if (onSuccess) onSuccess(data);
      if (onClose) onClose();
    } catch (err) {
      // EX-02: VNPay Refund Failure/Timeout
      const msg =
        err.response?.data?.message ||
        "Refund request failed or timed out at the payment gateway. The attempt is logged for review/retry.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const maxRefundable = orderSummary?.maxRefundableAmount || 0;
  const originalAmount = orderSummary?.originalAmount || 0;
  const customerName =
    orderSummary?.customerName || initialCustomerName || "Customer";
  const transactionId = orderSummary?.transactionId || `#PAY-${orderId}`;
  const purchaseDate = orderSummary?.purchaseDate
    ? new Date(orderSummary.purchaseDate).toLocaleString("en-US", {
        month: "short",
        day: "numeric",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
      })
    : "Sep 15, 2026 • 14:32";

  return (
    <div className="rf-backdrop" onClick={() => !submitting && onClose()}>
      <div className="rf-modal" onClick={(e) => e.stopPropagation()}>
        <div className="rf-header">
          <div className="rf-header-title">
            <span className="rf-icon">⟲</span>
            <div>
              <h2>Process Refund</h2>
              <p>Issue a full or partial refund via VNPay gateway.</p>
            </div>
          </div>
          <button
            type="button"
            className="rf-close"
            disabled={submitting}
            onClick={onClose}
          >
            ✕
          </button>
        </div>

        {loadingOrder ? (
          <div className="rf-loading">Loading order transaction details...</div>
        ) : (
          <form onSubmit={handleSubmit} className="rf-form">
            {/* Figure 142 Summary Card */}
            <div className="rf-card">
              <div className="rf-card-row rf-order-id">
                <span>Order #ORD-{orderId}</span>
              </div>
              <div className="rf-card-row">
                <span className="rf-label">Customer</span>
                <span className="rf-value">{customerName}</span>
              </div>
              <div className="rf-card-row">
                <span className="rf-label">Transaction ID</span>
                <span className="rf-value">{transactionId}</span>
              </div>
              <div className="rf-card-row">
                <span className="rf-label">Purchase Date</span>
                <span className="rf-value">{purchaseDate}</span>
              </div>
              <div className="rf-card-row">
                <span className="rf-label">Original Amount</span>
                <span className="rf-value highlight">
                  {formatPrice(originalAmount)}
                </span>
              </div>
              {maxRefundable < originalAmount && (
                <div className="rf-card-row">
                  <span className="rf-label">Max Refundable Balance</span>
                  <span className="rf-value" style={{ color: "#38bdf8" }}>
                    {formatPrice(maxRefundable)}
                  </span>
                </div>
              )}
            </div>

            {/* Refund Type Selector */}
            <div className="rf-group">
              <label>Refund Type *</label>
              <div className="rf-tab-group">
                <button
                  type="button"
                  className={`rf-tab-btn ${refundType === "Full" ? "active" : ""}`}
                  onClick={() => handleTypeChange("Full")}
                >
                  Full Refund
                </button>
                <button
                  type="button"
                  className={`rf-tab-btn ${refundType === "Partial" ? "active" : ""}`}
                  onClick={() => handleTypeChange("Partial")}
                >
                  Partial Refund
                </button>
              </div>
            </div>

            {/* Refund Amount Input */}
            <div className="rf-group">
              <label>Refund Amount *</label>
              <div className="rf-input-wrap">
                <input
                  type="number"
                  min="1"
                  max={maxRefundable || originalAmount}
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  disabled={refundType === "Full"}
                  className="rf-input"
                  placeholder="Enter refund amount in VND"
                  required
                />
              </div>
              {maxRefundable > 0 && (
                <p className="rf-hint">
                  Maximum refundable: <strong>{formatPrice(maxRefundable)}</strong>
                </p>
              )}
            </div>

            {/* Refund Reason Dropdown */}
            <div className="rf-group">
              <label>Refund Reason *</label>
              <select
                className="rf-select"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                required
              >
                <option value="Customer Request">Customer Request</option>
                <option value="Event Rescheduled">Event Rescheduled</option>
                <option value="Overcharge / Billing Issue">
                  Overcharge / Billing Issue
                </option>
                <option value="Support Case Resolution">
                  Support Case Resolution
                </option>
                <option value="Duplicate Transaction">
                  Duplicate Transaction
                </option>
                <option value="Other">Other</option>
              </select>
            </div>

            {error && <div className="rf-error" role="alert">{error}</div>}

            <div className="rf-actions">
              <button
                type="button"
                className="rf-btn-cancel"
                disabled={submitting}
                onClick={onClose}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="rf-btn-submit"
                disabled={submitting || maxRefundable <= 0}
              >
                {submitting ? "Processing..." : "Process Refund"}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
