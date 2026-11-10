import { useState } from "react";
import refundApi from "../../api/refundApi";
import { formatPrice } from "../../utils/format";
import "./refundModals.css";

export default function RetryRefundModal({
  refund,
  onClose,
  onSuccess,
}) {
  const [strategy, setStrategy] = useState("Immediate"); // "Immediate" | "Scheduled"
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!refund?.paymentRefundId) return;

    try {
      setSubmitting(true);
      setError("");
      const res = await refundApi.retryRefund(refund.paymentRefundId, {
        strategy,
      });
      const data = res.data?.data || res.data;
      if (onSuccess) onSuccess(data);
      if (onClose) onClose();
    } catch (err) {
      // EX-01: VNPay Retry Fails
      const msg =
        err.response?.data?.message ||
        "Retry failed. The refund remains available for permitted follow-up handling.";
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const refundId = refund?.paymentRefundId || 0;
  const originalRefundRef =
    refund?.refundReference || `#RFD-${refundId.toString().padStart(4, "0")}-FAIL`;
  const orderId = refund?.orderId || 0;
  const customerName = refund?.customerName || "Customer";
  const amount = refund?.amount || 0;
  const errorCode = refund?.gatewayErrorCode || "E5003";
  const errorMessage =
    refund?.errorMessage ||
    "Transaction timeout - bank gateway unavailable. Please retry after 5 minutes.";

  return (
    <div className="rf-backdrop" onClick={() => !submitting && onClose()}>
      <div className="rf-modal" onClick={(e) => e.stopPropagation()}>
        <div className="rf-header">
          <div className="rf-header-title">
            <span className="rf-icon">⟲</span>
            <div>
              <h2>Retry Failed Refund</h2>
              <p>Retry the failed refund without creating a duplicate transaction.</p>
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

        <form onSubmit={handleSubmit} className="rf-form">
          {/* Card 1: Refund Failed Details */}
          <div className="rf-card">
            <div className="rf-card-badge-failed">Refund Failed</div>
            <div className="rf-card-row">
              <span className="rf-label">Original Refund ID</span>
              <span className="rf-value mono">{originalRefundRef}</span>
            </div>
            <div className="rf-card-row">
              <span className="rf-label">Order</span>
              <span className="rf-value mono">#ORD-{orderId}</span>
            </div>
            <div className="rf-card-row">
              <span className="rf-label">Customer</span>
              <span className="rf-value">{customerName}</span>
            </div>
            <div className="rf-card-row">
              <span className="rf-label">Original Amount</span>
              <span className="rf-value highlight">{formatPrice(amount)}</span>
            </div>
          </div>

          {/* Card 2: Error Details */}
          <div className="rf-card rf-error-box">
            <div className="rf-error-header">Error Details</div>
            <div className="rf-error-code">
              VNPay Gateway Error Code: <strong>{errorCode}</strong>
            </div>
            <div className="rf-error-message">Message: "{errorMessage}"</div>
          </div>

          {/* Retry Amount */}
          <div className="rf-group">
            <label>Retry Amount</label>
            <input
              type="text"
              value={formatPrice(amount)}
              disabled
              className="rf-input locked"
            />
          </div>

          {/* Retry Strategy */}
          <div className="rf-group">
            <label>Retry Strategy *</label>
            <div className="rf-strategy-group">
              <button
                type="button"
                className={`rf-strategy-btn ${strategy === "Immediate" ? "active" : ""}`}
                onClick={() => setStrategy("Immediate")}
              >
                Retry Immediately
              </button>
              <button
                type="button"
                className={`rf-strategy-btn ${strategy === "Scheduled" ? "active" : ""}`}
                onClick={() => setStrategy("Scheduled")}
              >
                Schedule Retry
              </button>
            </div>
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
            <button type="submit" className="rf-btn-submit" disabled={submitting}>
              {submitting ? "Retrying..." : "Retry Refund"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
