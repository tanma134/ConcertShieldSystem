import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import ticketApi from "../../api/ticketApi";
import supportCaseApi from "../../api/supportCaseApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import "../../styles/supportCaseManagement.css";
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
  const { user, isAdmin, isStaff } = useAuth();
  const isStaffOrAdmin = Boolean(isAdmin || isStaff);
  const toast = useToast();

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [supportCase, setSupportCase] = useState(null);
  const [loadingSupportCase, setLoadingSupportCase] = useState(false);

  const [showSupportModal, setShowSupportModal] = useState(false);
  const [supportCategory, setSupportCategory] = useState("General Inquiry");
  const [supportTitle, setSupportTitle] = useState("");
  const [supportDescription, setSupportDescription] = useState("");
  const [evidenceFile, setEvidenceFile] = useState(null);
  const [evidenceFileName, setEvidenceFileName] = useState("");
  const [evidenceDataUrl, setEvidenceDataUrl] = useState("");
  const [submittingSupport, setSubmittingSupport] = useState(false);

  const isDispute = ["Invalid Ticket", "Payment Dispute"].includes(supportCategory);

  const fetchSupportCase = async () => {
    if (!orderId) return;
    try {
      setLoadingSupportCase(true);
      const res = await supportCaseApi.getCaseByOrder(orderId);
      setSupportCase(res.data);
    } catch {
      setSupportCase(null);
    } finally {
      setLoadingSupportCase(false);
    }
  };

  const handleOpenSupport = () => {
    setSupportTitle(`Support request for Order #${orderId}`);
    setSupportCategory("General Inquiry");
    setSupportDescription("");
    setEvidenceFile(null);
    setEvidenceFileName("");
    setEvidenceDataUrl("");
    setShowSupportModal(true);
  };

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (file) {
      setEvidenceFile(file);
      setEvidenceFileName(file.name);
      if (file.type.startsWith("image/")) {
        const reader = new FileReader();
        reader.onload = (evt) => {
          const dataUrl = evt.target?.result;
          if (dataUrl) {
            setEvidenceDataUrl(dataUrl);
            try {
              localStorage.setItem(`cs_evidence_${file.name}`, dataUrl);
              localStorage.setItem(`cs_evidence_order_${orderId}`, dataUrl);
            } catch (err) {}
          }
        };
        reader.readAsDataURL(file);
      }
    } else {
      setEvidenceFile(null);
      setEvidenceFileName("");
      setEvidenceDataUrl("");
    }
  };

  const handleSubmitSupport = async (e) => {
    e.preventDefault();
    if (!supportTitle.trim() || !supportDescription.trim()) {
      toast.error("Please enter both title and detailed description.");
      return;
    }

    setSubmittingSupport(true);
    try {
      const fullDescription = evidenceFileName
        ? `${supportDescription.trim()}\n\n[Supporting Evidence Attached: ${evidenceFileName}]`
        : supportDescription.trim();

      const payload = {
        orderId: parseInt(orderId, 10),
        title: supportTitle.trim(),
        description: fullDescription,
        category: supportCategory,
        priority: isDispute ? "High" : "Medium",
        customerId: user?.id || null,
        customerName: user?.fullName || user?.name || user?.email || "Customer",
        customerEmail: user?.email || "",
        customerPhone: user?.phoneNumber || null,
      };

      const res = await supportCaseApi.createCase(payload);
      if (evidenceDataUrl) {
        try {
          if (res.data?.caseNumber) localStorage.setItem(`cs_evidence_${res.data.caseNumber}`, evidenceDataUrl);
          if (res.data?.caseId) localStorage.setItem(`cs_evidence_case_${res.data.caseId}`, evidenceDataUrl);
          if (evidenceFileName) localStorage.setItem(`cs_evidence_${evidenceFileName}`, evidenceDataUrl);
          localStorage.setItem(`cs_evidence_order_${orderId}`, evidenceDataUrl);
        } catch (err) {}
      }
      toast.success(
        isDispute
          ? `Dispute inquiry #${res.data.caseNumber} has been logged for ticket dispute investigation!`
          : `Your support case #${res.data.caseNumber} has been submitted! Support staff will review shortly.`
      );
      setShowSupportModal(false);
      fetchSupportCase();
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to submit support request.");
    } finally {
      setSubmittingSupport(false);
    }
  };

  useEffect(() => {
    fetchSupportCase();
  }, [orderId]);

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
          {/* Admin / Staff Inspection Top Banner */}
          {isStaffOrAdmin && order && (
            <div
              style={{
                background: "#eff6ff",
                border: "1px solid #bfdbfe",
                borderRadius: "10px",
                padding: "14px 18px",
                marginBottom: "20px",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
                flexWrap: "wrap",
                gap: "12px",
                boxShadow: "0 1px 3px rgba(0,0,0,0.05)",
              }}
            >
              <div>
                <div style={{ display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" }}>
                  <span
                    style={{
                      fontSize: "11px",
                      fontWeight: 700,
                      textTransform: "uppercase",
                      color: "#1d4ed8",
                      background: "#dbeafe",
                      padding: "2px 8px",
                      borderRadius: "4px",
                      letterSpacing: "0.5px",
                    }}
                  >
                    Admin & Staff Inspection Mode
                  </span>
                  <span style={{ fontSize: "14px", fontWeight: 700, color: "#1e3a8a" }}>
                    Customer Order #{order.orderId} (User ID: {order.customerId || "N/A"})
                  </span>
                </div>
                <div style={{ fontSize: "13px", color: "#2563eb", marginTop: "4px" }}>
                  Customer: <strong>{order.fullName || "Customer"}</strong> • Email:{" "}
                  <strong>{order.email || "N/A"}</strong>
                  {order.phone ? ` • Phone: ${order.phone}` : ""}
                </div>
              </div>
              <button
                type="button"
                onClick={() => {
                  if (window.history.length > 1) window.history.back();
                  else window.close();
                }}
                style={{
                  padding: "8px 14px",
                  borderRadius: "6px",
                  border: "1px solid #93c5fd",
                  background: "#ffffff",
                  color: "#1d4ed8",
                  fontSize: "13px",
                  fontWeight: 600,
                  cursor: "pointer",
                }}
              >
                ← Return to Previous Page
              </button>
            </div>
          )}

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

              {/* Active / Resolved Support Case Status Banner */}
              {supportCase && (
                <div
                  className={`order-support-banner ${supportCase.status === "Resolved" || supportCase.status === "Closed"
                      ? "resolved"
                      : "in-progress"
                    }`}
                >
                  <div className="order-support-icon">
                    {supportCase.status === "Resolved" || supportCase.status === "Closed" ? (
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    ) : (
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <circle cx="12" cy="12" r="10" />
                        <polyline points="12 6 12 12 16 14" />
                      </svg>
                    )}
                  </div>
                  <div className="order-support-content">
                    <div className="order-support-header">
                      <h4 className="order-support-title">
                        {supportCase.status === "Resolved" || supportCase.status === "Closed"
                          ? `Support Request Resolved (#${supportCase.caseNumber})`
                          : `Support Request In Progress (#${supportCase.caseNumber})`}
                      </h4>
                      <span
                        className={`order-support-badge ${supportCase.status === "Resolved" || supportCase.status === "Closed"
                            ? "resolved"
                            : "in-progress"
                          }`}
                      >
                        {supportCase.status}
                      </span>
                    </div>

                    <p className="order-support-desc">
                      <strong>Subject:</strong> {supportCase.title}
                    </p>

                    {supportCase.resolution && (
                      <div className="order-support-resolution">
                        <strong>Resolution Outcome:</strong>
                        <div style={{ marginTop: "4px", whiteSpace: "pre-wrap" }}>
                          {supportCase.resolution}
                        </div>
                      </div>
                    )}

                    <div className="order-support-meta">
                      {supportCase.status === "Resolved" || supportCase.status === "Closed" ? (
                        <span>
                          Resolved by <strong>{supportCase.resolvedByName || "Support Team"}</strong> on{" "}
                          {formatDate(supportCase.resolvedAt || supportCase.updatedAt)}
                        </span>
                      ) : (
                        <span>
                          Submitted on {formatDate(supportCase.createdAt)} • Staff assigned:{" "}
                          <strong>{supportCase.assignedStaffName || "Support Desk Queue"}</strong>
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              )}

              <section className="order-details-card">
                <header className="order-details-section-heading">
                  <div>
                    <p className="order-details-eyebrow">Your purchase</p>
                    <h2>Order summary</h2>
                  </div>
                </header>

                <div className="order-details-meta">
                  {isStaffOrAdmin && (
                    <>
                      <div>
                        <span>Customer name</span>
                        <strong>{order.fullName || "—"}</strong>
                      </div>
                      <div>
                        <span>Customer email</span>
                        <strong>{order.email || "—"}</strong>
                      </div>
                      {order.phone && (
                        <div>
                          <span>Customer phone</span>
                          <strong>{order.phone}</strong>
                        </div>
                      )}
                    </>
                  )}

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

              {isStaffOrAdmin ? (
                <div
                  className="order-details-actions"
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: "12px",
                    background: "#f8fafc",
                    border: "1px solid #e2e8f0",
                    borderRadius: "8px",
                    padding: "14px 20px",
                  }}
                >
                  <div style={{ fontSize: "13px", color: "#475569" }}>
                    <strong>Staff / Admin Inspection View:</strong> Customer Order <strong>#{order.orderId}</strong>.
                  </div>
                  <button
                    type="button"
                    className="order-details-support-btn"
                    onClick={() => {
                      if (window.history.length > 1) window.history.back();
                      else window.close();
                    }}
                    style={{ background: "#334155", borderColor: "#1e293b", color: "#ffffff" }}
                  >
                    ← Return to Dashboard
                  </button>
                </div>
              ) : (
                <div className="order-details-actions">
                  {supportCase ? (
                    <button
                      type="button"
                      className="order-details-support-btn"
                      onClick={() => setShowSupportModal(true)}
                      style={{
                        background: supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#15803d" : "#1d4ed8",
                        borderColor: supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#166534" : "#1e40af",
                        color: "#ffffff",
                      }}
                    >
                      {supportCase.status === "Resolved" || supportCase.status === "Closed"
                        ? `Support Request Resolved (#${supportCase.caseNumber})`
                        : `Support Request Active (#${supportCase.caseNumber})`}
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="order-details-support-btn"
                      onClick={handleOpenSupport}
                    >
                      Need Help / Dispute Order?
                    </button>
                  )}
                  <Link to="/my-tickets">Go to My Tickets</Link>
                </div>
              )}
            </>
          )}

          {/* Customer Support Modal */}
          {showSupportModal && (
            <div className="sc-modal-backdrop" onClick={() => setShowSupportModal(false)}>
              <div className="sc-modal" onClick={(e) => e.stopPropagation()}>
                <div className="sc-modal-header">
                  <h3>
                    {supportCase
                      ? `Support Case #${supportCase.caseNumber}`
                      : `Contact Support for Order #${orderId}`}
                  </h3>
                  <button
                    type="button"
                    onClick={() => setShowSupportModal(false)}
                    style={{ background: "none", border: "none", color: "#7e7694", cursor: "pointer", fontSize: "18px" }}
                  >
                    Close
                  </button>
                </div>

                {supportCase ? (
                  <div>
                    <div className="sc-modal-body">
                      <div
                        style={{
                          padding: "16px",
                          borderRadius: "8px",
                          background: supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#f0fdf4" : "#eff6ff",
                          border: `1px solid ${supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#bbf7d0" : "#bfdbfe"}`,
                          marginBottom: "16px",
                        }}
                      >
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
                          <span style={{ fontSize: "14px", fontWeight: 700, color: supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#166534" : "#1e40af" }}>
                            Case #{supportCase.caseNumber}
                          </span>
                          <span
                            style={{
                              padding: "3px 10px",
                              borderRadius: "12px",
                              fontSize: "12px",
                              fontWeight: 700,
                              background: supportCase.status === "Resolved" || supportCase.status === "Closed" ? "#22c55e" : "#3b82f6",
                              color: "#ffffff",
                            }}
                          >
                            {supportCase.status}
                          </span>
                        </div>
                        <div style={{ fontSize: "13px", color: "#334155", marginBottom: "4px" }}>
                          <strong>Subject:</strong> {supportCase.title}
                        </div>
                        <div style={{ fontSize: "13px", color: "#334155" }}>
                          <strong>Category:</strong> {supportCase.category}
                        </div>
                        {supportCase.assignedStaffName && (
                          <div style={{ fontSize: "12px", color: "#64748b", marginTop: "6px" }}>
                            Assigned Staff: <strong>{supportCase.assignedStaffName}</strong>
                          </div>
                        )}
                      </div>

                      <div style={{ marginBottom: "16px" }}>
                        <label style={{ fontSize: "12px", fontWeight: 700, color: "#64748b", textTransform: "uppercase" }}>
                          Your Submitted Inquiry
                        </label>
                        <div
                          style={{
                            background: "#f8fafc",
                            padding: "12px 14px",
                            borderRadius: "6px",
                            fontSize: "13px",
                            lineHeight: 1.6,
                            marginTop: "4px",
                            border: "1px solid #e2e8f0",
                            whiteSpace: "pre-wrap",
                          }}
                        >
                          {supportCase.description}
                        </div>
                      </div>

                      {supportCase.resolution && (
                        <div style={{ marginBottom: "16px", background: "#f0fdf4", border: "1px solid #86efac", borderRadius: "8px", padding: "14px 16px" }}>
                          <div style={{ fontSize: "13px", fontWeight: 700, color: "#166534" }}>
                            Official Resolution Outcome:
                          </div>
                          <div style={{ marginTop: "6px", fontSize: "13px", color: "#15803d", whiteSpace: "pre-wrap", lineHeight: 1.6 }}>
                            {supportCase.resolution}
                          </div>
                          <div style={{ marginTop: "8px", fontSize: "11px", color: "#166534" }}>
                            Resolved by <strong>{supportCase.resolvedByName || "Support Desk"}</strong> on {formatDate(supportCase.resolvedAt)}
                          </div>
                        </div>
                      )}
                    </div>

                    <div className="sc-modal-footer" style={{ justifyContent: "space-between" }}>
                      <Link
                        to={`/support-cases/${supportCase.caseId}`}
                        className="sc-btn sc-btn-primary"
                        style={{ textDecoration: "none", fontSize: "13px" }}
                      >
                        View Full Case & Timeline Notes →
                      </Link>
                      <button
                        type="button"
                        className="sc-btn sc-btn-outline"
                        onClick={() => setShowSupportModal(false)}
                      >
                        Close
                      </button>
                    </div>
                  </div>
                ) : (
                  <form onSubmit={handleSubmitSupport}>
                    <div className="sc-modal-body">
                      <p style={{ margin: "0 0 16px 0", fontSize: "13px", color: "var(--sc-text-secondary)" }}>
                        Having issues with your tickets, payment, or check-in? Submit your inquiry directly to our support desk.
                      </p>

                      <div className="sc-form-group">
                        <label className="sc-form-label">Category</label>
                        <select
                          className="sc-form-input"
                          value={supportCategory}
                          onChange={(e) => setSupportCategory(e.target.value)}
                        >
                          <optgroup label="Ticket Dispute (Investigation & Resolution Required)">
                            <option value="Invalid Ticket">Invalid ticket (Reported invalid despite legitimate purchase)</option>
                            <option value="Payment Dispute">Payment dispute (Charged twice / Debited without receiving ticket)</option>
                          </optgroup>
                          <optgroup label="Support Case (General Helpdesk & Customer Inquiries)">
                            <option value="General Inquiry">General inquiry / Other</option>
                            <option value="Refund Complaint">Refund inquiry / Issue</option>
                            <option value="Ticketing">Ticketing assistance (Missing ticket / Barcode error)</option>
                            <option value="Payment & Billing">Payment & Billing (Charge issue / Transaction query)</option>
                            <option value="Event Dispute">Event inquiry (Rescheduled, venue change, or cancellation)</option>
                          </optgroup>
                        </select>
                      </div>

                      <div className="sc-form-group">
                        <label className="sc-form-label">Subject</label>
                        <input
                          type="text"
                          className="sc-form-input"
                          value={supportTitle}
                          onChange={(e) => setSupportTitle(e.target.value)}
                          required
                        />
                      </div>

                      <div className="sc-form-group">
                        <label className="sc-form-label">Description of the issue</label>
                        <textarea
                          className="sc-form-textarea"
                          rows={4}
                          placeholder="Please explain what happened so our team can resolve it as quickly as possible..."
                          value={supportDescription}
                          onChange={(e) => setSupportDescription(e.target.value)}
                          required
                        />
                      </div>

                      <div className="sc-form-group">
                        <label className="sc-form-label">
                          Attachment / Screenshot (Optional: receipt, bank statement, or error image)
                        </label>
                        <div style={{ display: "flex", alignItems: "center", gap: "10px", marginTop: "4px", flexWrap: "wrap" }}>
                          <label
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: "6px",
                              padding: "8px 14px",
                              background: "#f1f5f9",
                              border: "1px solid #cbd5e1",
                              borderRadius: "6px",
                              fontSize: "13px",
                              fontWeight: 600,
                              color: "#334155",
                              cursor: "pointer",
                            }}
                          >
                            <span>Choose File</span>
                            <input
                              type="file"
                              accept="image/png,image/jpeg,image/webp,application/pdf"
                              style={{ display: "none" }}
                              onChange={handleFileChange}
                            />
                          </label>
                          <span style={{ fontSize: "13px", color: evidenceFileName ? "#16a34a" : "#64748b", fontWeight: evidenceFileName ? 600 : 400 }}>
                            {evidenceFileName ? evidenceFileName : "No file chosen (PNG, JPG, PDF up to 10MB)"}
                          </span>
                          {evidenceFileName && (
                            <button
                              type="button"
                              onClick={() => {
                                setEvidenceFile(null);
                                setEvidenceFileName("");
                              }}
                              style={{
                                background: "none",
                                border: "none",
                                color: "#ef4444",
                                fontSize: "12px",
                                fontWeight: 600,
                                cursor: "pointer",
                                padding: "2px 6px",
                              }}
                            >
                              Remove
                            </button>
                          )}
                        </div>
                      </div>
                    </div>

                    <div className="sc-modal-footer">
                      <button
                        type="button"
                        className="sc-btn sc-btn-outline"
                        onClick={() => setShowSupportModal(false)}
                        disabled={submittingSupport}
                      >
                        Cancel
                      </button>
                      <button type="submit" className="sc-btn sc-btn-primary" disabled={submittingSupport}>
                        {submittingSupport ? "Submitting..." : "Submit Support Request"}
                      </button>
                    </div>
                  </form>
                )}
              </div>
            </div>
          )}
        </div>
      </main>

      <Footer />
    </>
  );
}