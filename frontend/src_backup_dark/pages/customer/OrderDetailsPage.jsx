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
  const { user } = useAuth();
  const toast = useToast();

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const [supportCase, setSupportCase] = useState(null);
  const [loadingSupportCase, setLoadingSupportCase] = useState(false);

  const [showSupportModal, setShowSupportModal] = useState(false);
  const [supportCategory, setSupportCategory] = useState("Ticketing");
  const [supportTitle, setSupportTitle] = useState("");
  const [supportDescription, setSupportDescription] = useState("");
  const [submittingSupport, setSubmittingSupport] = useState(false);

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
    setSupportCategory("Ticketing");
    setSupportDescription("");
    setShowSupportModal(true);
  };

  const handleSubmitSupport = async (e) => {
    e.preventDefault();
    if (!supportTitle.trim() || !supportDescription.trim()) {
      toast.error("Please enter both title and detailed description.");
      return;
    }

    setSubmittingSupport(true);
    try {
      const payload = {
        orderId: parseInt(orderId, 10),
        title: supportTitle.trim(),
        description: supportDescription.trim(),
        category: supportCategory,
        priority: "Medium",
        customerId: user?.id || null,
        customerName: user?.fullName || user?.name || user?.email || "Customer",
        customerEmail: user?.email || "",
        customerPhone: user?.phoneNumber || null,
      };

      const res = await supportCaseApi.createCase(payload);
      toast.success(`Your support case #${res.data.caseNumber} has been submitted! Support staff will review shortly.`);
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
                  className={`order-support-banner ${
                    supportCase.status === "Resolved" || supportCase.status === "Closed"
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
                        className={`order-support-badge ${
                          supportCase.status === "Resolved" || supportCase.status === "Closed"
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
                {supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned") ? (
                  <button
                    type="button"
                    className="order-details-support-btn"
                    onClick={handleOpenSupport}
                    style={{ background: "#1e3a8a", borderColor: "#3b82f6" }}
                  >
                    Support Request Active (#{supportCase.caseNumber})
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
            </>
          )}

          {/* Ticketmaster-style Customer Support Modal */}
          {showSupportModal && (
            <div className="sc-modal-backdrop" onClick={() => setShowSupportModal(false)}>
              <div className="sc-modal" onClick={(e) => e.stopPropagation()}>
                <div className="sc-modal-header">
                  <h3>
                    {supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned")
                      ? `Active Support Case #${supportCase.caseNumber}`
                      : `Contact Support for Order #${orderId}`}
                  </h3>
                  <button
                    type="button"
                    onClick={() => setShowSupportModal(false)}
                    style={{ background: "none", border: "none", color: "#9ca3af", cursor: "pointer", fontSize: "18px" }}
                  >
                    ✕
                  </button>
                </div>

                <form onSubmit={handleSubmitSupport}>
                  <div className="sc-modal-body">
                    {supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned") ? (
                      <div
                        style={{
                          padding: "14px 16px",
                          borderRadius: "8px",
                          background: "rgba(59, 130, 246, 0.15)",
                          border: "1px solid rgba(59, 130, 246, 0.4)",
                          color: "#bfdbfe",
                          fontSize: "13px",
                          lineHeight: 1.5,
                          marginBottom: "16px",
                        }}
                      >
                        <strong>Case In Progress:</strong> You already have an active support request (
                        <strong>#{supportCase.caseNumber}</strong>) open for this order. Our support team is actively reviewing your request. Duplicate submissions for the same order are blocked to prevent delays.
                      </div>
                    ) : (
                      <p style={{ margin: 0, fontSize: "13px", color: "var(--sc-text-secondary)" }}>
                        Having issues with your tickets, payment, or check-in? Submit your inquiry directly to our support desk.
                      </p>
                    )}

                    <div className="sc-form-group">
                      <label className="sc-form-label">Category</label>
                      <select
                        className="sc-form-input"
                        value={supportCategory}
                        onChange={(e) => setSupportCategory(e.target.value)}
                        disabled={supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned")}
                      >
                        <option value="Ticketing">Ticketing (Missing ticket / Barcode error)</option>
                        <option value="Payment & Billing">Payment & Billing (Charge issue / Refund inquiry)</option>
                        <option value="Event Dispute">Event Dispute (Rescheduled / Cancelled event)</option>
                        <option value="General Inquiry">General Inquiry / Other</option>
                      </select>
                    </div>

                    <div className="sc-form-group">
                      <label className="sc-form-label">Subject</label>
                      <input
                        type="text"
                        className="sc-form-input"
                        value={supportTitle}
                        onChange={(e) => setSupportTitle(e.target.value)}
                        disabled={supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned")}
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
                        disabled={supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned")}
                        required
                      />
                    </div>
                  </div>

                  <div className="sc-modal-footer">
                    <button
                      type="button"
                      className="sc-btn sc-btn-outline"
                      onClick={() => setShowSupportModal(false)}
                      disabled={submittingSupport}
                    >
                      {supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned") ? "Close" : "Cancel"}
                    </button>
                    {!(supportCase && (supportCase.status === "Open" || supportCase.status === "Assigned")) && (
                      <button type="submit" className="sc-btn sc-btn-primary" disabled={submittingSupport}>
                        {submittingSupport ? "Submitting..." : "Submit Support Request"}
                      </button>
                    )}
                  </div>
                </form>
              </div>
            </div>
          )}
        </div>
      </main>

      <Footer />
    </>
  );
}