import { useState } from "react";
import { useNavigate, useLocation, Link } from "react-router-dom";
import supportCaseApi from "../../api/supportCaseApi";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "../staff/StaffShell";
import AdminShell from "../admin/AdminShell";
import "../../styles/supportCaseManagement.css";

const CATEGORIES = [
  "Ticketing",
  "Payment & Billing",
  "Account & Security",
  "Event Dispute",
  "Technical Issue",
  "General Inquiry",
];

const PRIORITIES = ["Low", "Medium", "High", "Critical"];

export default function SupportCaseCreatePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();

  const isAdminPath = location.pathname.startsWith("/admin");
  const Shell = isAdminPath ? AdminShell : StaffShell;

  const [submitting, setSubmitting] = useState(false);
  const [formData, setFormData] = useState({
    customerId: "",
    customerName: "",
    customerEmail: "",
    customerPhone: "",
    title: "",
    description: "",
    category: "Ticketing",
    priority: "Medium",
    orderId: "",
    ticketId: "",
    paymentReference: "",
  });

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
  };

  // Create support case
  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!formData.customerName.trim()) {
      toast.error("Customer name is required.");
      return;
    }
    if (!formData.customerEmail.trim()) {
      toast.error("Customer email is required.");
      return;
    }
    if (!formData.title.trim()) {
      toast.error("Case title is required.");
      return;
    }
    if (!formData.description.trim()) {
      toast.error("Description is required.");
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        customerId: formData.customerId ? parseInt(formData.customerId, 10) : null,
        customerName: formData.customerName.trim(),
        customerEmail: formData.customerEmail.trim(),
        customerPhone: formData.customerPhone.trim() || null,
        title: formData.title.trim(),
        description: formData.description.trim(),
        category: formData.category,
        priority: formData.priority,
        // Transaction context references
        orderId: formData.orderId ? parseInt(formData.orderId, 10) : null,
        ticketId: formData.ticketId ? parseInt(formData.ticketId, 10) : null,
        paymentReference: formData.paymentReference.trim() || null,
      };

      const res = await supportCaseApi.createCase(payload);
      toast.success(`Support case #${res.data.caseNumber} created successfully!`);
      const targetDetailUrl = isAdminPath
        ? `/admin/support-cases/${res.data.caseId}`
        : `/staff/support-cases/${res.data.caseId}`;
      navigate(targetDetailUrl);
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to create support case.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Shell title="Create Customer Support Case">
      <div className="sc-container" style={{ maxWidth: "900px" }}>
        <div className="sc-header">
          <div className="sc-header-title">
            <span className="sc-badge-pill">Customer Support</span>
            <h1>Create Customer Support Case</h1>
            <p>Record a customer grievance, transaction dispute, or on-site support request.</p>
          </div>
          <div className="sc-header-actions">
            <Link
              to={isAdminPath ? "/admin/support-cases" : "/staff/support-cases"}
              className="sc-btn sc-btn-outline"
            >
              ← Back to List
            </Link>
          </div>
        </div>

        <form onSubmit={handleSubmit}>
          {/* Customer Info */}
          <div className="sc-main-card">
            <h2 className="sc-card-title">1. Customer Information</h2>
            <div className="sc-form-grid">
              <div className="sc-form-group">
                <label className="sc-form-label">
                  Customer Full Name <span style={{ color: "var(--sc-danger)" }}>*</span>
                </label>
                <input
                  type="text"
                  name="customerName"
                  className="sc-form-input"
                  placeholder="e.g. John Doe"
                  value={formData.customerName}
                  onChange={handleChange}
                  required
                />
              </div>

              <div className="sc-form-group">
                <label className="sc-form-label">
                  Customer Email <span style={{ color: "var(--sc-danger)" }}>*</span>
                </label>
                <input
                  type="email"
                  name="customerEmail"
                  className="sc-form-input"
                  placeholder="john.doe@example.com"
                  value={formData.customerEmail}
                  onChange={handleChange}
                  required
                />
              </div>

              <div className="sc-form-group">
                <label className="sc-form-label">Contact Phone</label>
                <input
                  type="text"
                  name="customerPhone"
                  className="sc-form-input"
                  placeholder="+1 (555) 019-2834"
                  value={formData.customerPhone}
                  onChange={handleChange}
                />
              </div>

              <div className="sc-form-group">
                <label className="sc-form-label">Customer User ID (Optional)</label>
                <input
                  type="number"
                  name="customerId"
                  className="sc-form-input"
                  placeholder="e.g. 1042"
                  value={formData.customerId}
                  onChange={handleChange}
                />
              </div>
            </div>
          </div>

          {/* Case Details */}
          <div className="sc-main-card">
            <h2 className="sc-card-title">2. Case Details & Priority</h2>
            <div className="sc-form-group" style={{ marginBottom: "16px" }}>
              <label className="sc-form-label">
                Case Title <span style={{ color: "var(--sc-danger)" }}>*</span>
              </label>
              <input
                type="text"
                name="title"
                className="sc-form-input"
                placeholder="Brief summary of the issue or complaint"
                value={formData.title}
                onChange={handleChange}
                required
              />
            </div>

            <div className="sc-form-grid" style={{ marginBottom: "16px" }}>
              <div className="sc-form-group">
                <label className="sc-form-label">Category</label>
                <select
                  name="category"
                  className="sc-form-input"
                  value={formData.category}
                  onChange={handleChange}
                >
                  {CATEGORIES.map((cat) => (
                    <option key={cat} value={cat}>
                      {cat}
                    </option>
                  ))}
                </select>
              </div>

              <div className="sc-form-group">
                <label className="sc-form-label">Priority</label>
                <select
                  name="priority"
                  className="sc-form-input"
                  value={formData.priority}
                  onChange={handleChange}
                >
                  {PRIORITIES.map((pri) => (
                    <option key={pri} value={pri}>
                      {pri}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="sc-form-group">
              <label className="sc-form-label">
                Detailed Description <span style={{ color: "var(--sc-danger)" }}>*</span>
              </label>
              <textarea
                name="description"
                className="sc-form-textarea"
                rows={4}
                placeholder="Provide complete facts, error messages, user claims, or assistance requested..."
                value={formData.description}
                onChange={handleChange}
                required
              />
            </div>
          </div>

          {/* Transaction Context References */}
          <div className="sc-main-card">
            <h2 className="sc-card-title">
              3. Related Transaction References
            </h2>
            <p style={{ fontSize: "13px", color: "var(--sc-text-secondary)", marginTop: "-8px", marginBottom: "16px" }}>
              Provide valid links to Order ID, Ticket ID, or Payment Reference to verify purchase and speed up resolution.
            </p>
            <div className="sc-form-grid">
              <div className="sc-form-group">
                <label className="sc-form-label">Order ID</label>
                <input
                  type="number"
                  name="orderId"
                  className="sc-form-input"
                  placeholder="e.g. 501"
                  value={formData.orderId}
                  onChange={handleChange}
                />
              </div>

              <div className="sc-form-group">
                <label className="sc-form-label">Ticket ID</label>
                <input
                  type="number"
                  name="ticketId"
                  className="sc-form-input"
                  placeholder="e.g. 1205"
                  value={formData.ticketId}
                  onChange={handleChange}
                />
              </div>

              <div className="sc-form-group" style={{ gridColumn: "span 2" }}>
                <label className="sc-form-label">Payment Gateway Reference / Transaction Code</label>
                <input
                  type="text"
                  name="paymentReference"
                  className="sc-form-input"
                  placeholder="e.g. PAY-VN-20261016-88912"
                  value={formData.paymentReference}
                  onChange={handleChange}
                />
              </div>
            </div>
          </div>

          {/* Action buttons */}
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "12px", marginBottom: "40px" }}>
            <button
              type="button"
              className="sc-btn sc-btn-outline"
              onClick={() => navigate(isAdminPath ? "/admin/support-cases" : "/staff/support-cases")}
              disabled={submitting}
            >
              Cancel
            </button>
            <button type="submit" className="sc-btn sc-btn-primary" disabled={submitting}>
              {submitting ? "Submitting..." : "Create Support Case"}
            </button>
          </div>
        </form>
      </div>
    </Shell>
  );
}
