import { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import voucherApi from "../../api/voucherApi";
import { useAuth } from "../../context/AuthContext";
import { formatDateTime, formatPrice } from "../../utils/format";
import "./AdminDashboardPage.css";
import "./VoucherManagementPage.css";

const normalizeVoucher = (v) => ({
  voucherId: v.voucher_id ?? v.voucherId,
  code: v.code || "",
  scope: v.scope || "SYSTEM",
  organizerId: v.organizer_id ?? v.organizerId,
  eventId: v.event_id ?? v.eventId,
  discountType: v.discount_type ?? v.discountType ?? "PERCENT",
  discountAmount: v.discount_amount ?? v.discountAmount,
  discountPercent: v.discount_percent ?? v.discountPercent,
  maxDiscountAmount: v.max_discount_amount ?? v.maxDiscountAmount,
  minOrderAmount: v.min_order_amount ?? v.minOrderAmount ?? 0,
  totalQuantity: v.total_quantity ?? v.totalQuantity ?? 0,
  usedQuantity: v.used_quantity ?? v.usedQuantity ?? 0,
  maxUsagePerUser: v.max_usage_per_user ?? v.maxUsagePerUser ?? 1,
  startsAt: v.starts_at ?? v.startsAt,
  endsAt: v.ends_at ?? v.endsAt,
  isActive: v.is_active ?? v.isActive ?? true,
  status: v.status || (v.is_active ?? v.isActive ? "Active" : "Inactive"),
  createdBy: v.created_by ?? v.createdBy,
  createdAt: v.created_at ?? v.createdAt,
});

export default function VoucherManagementPage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const [vouchers, setVouchers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  // Filters & Pagination
  const [page, setPage] = useState(1);
  const [limit] = useState(10);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedScope, setSelectedScope] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [searchCode, setSearchCode] = useState("");

  // Modals: null | "create" | "edit" | "usages" | "delete"
  const [modal, setModal] = useState(null);
  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [usages, setUsages] = useState([]);
  const [usagesLoading, setUsagesLoading] = useState(false);

  // Form State
  const [formData, setFormData] = useState({
    code: "",
    scope: "SYSTEM",
    organizer_id: "",
    event_id: "",
    discount_type: "PERCENT",
    discount_amount: "",
    discount_percent: "",
    max_discount_amount: "",
    min_order_amount: "0",
    total_quantity: "100",
    max_usage_per_user: "1",
    starts_at: "",
    ends_at: "",
  });

  // Real-time Field Validation Errors
  const [fieldErrors, setFieldErrors] = useState({});

  const formatDateForInput = (dateStr) => {
    if (!dateStr) return "";
    const d = new Date(dateStr);
    if (isNaN(d.getTime())) return "";
    const pad = (n) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  const loadVouchers = useCallback(async () => {
    try {
      setLoading(true);
      setError("");
      const params = {
        page,
        limit,
        scope: selectedScope || undefined,
        is_active: statusFilter === "" ? undefined : statusFilter === "true",
        search_code: searchCode.trim() || undefined,
      };

      const response = await voucherApi.getVouchers(params);
      const data = response.data?.data;
      if (data) {
        const rawItems = data.items || [];
        setVouchers(rawItems.map(normalizeVoucher));
        setTotalPages(data.totalPages || 1);
        setTotalCount(data.totalCount || 0);
      }
    } catch (err) {
      setError(err.response?.data?.message || "Failed to load voucher list.");
    } finally {
      setLoading(false);
    }
  }, [page, limit, selectedScope, statusFilter, searchCode]);

  useEffect(() => {
    loadVouchers();
  }, [loadVouchers]);

  // Real-time validator
  const validateForm = (data, isEdit = false, voucher = null) => {
    const errors = {};
    const now = new Date();

    // Code
    if (!data.code || !data.code.trim()) {
      errors.code = "Voucher code is required.";
    } else if (/\s/.test(data.code)) {
      errors.code = "Voucher code cannot contain spaces.";
    }

    // Discount Type & Values
    if (data.discount_type === "PERCENT") {
      const val = parseFloat(data.discount_percent);
      if (isNaN(val) || val <= 0 || val > 100) {
        errors.discount_percent = "Discount percentage must be between 0.01% and 100%.";
      }
    } else if (data.discount_type === "FIXED") {
      const val = parseInt(data.discount_amount);
      if (isNaN(val) || val <= 0) {
        errors.discount_amount = "Fixed discount amount must be greater than 0.";
      }
    }

    // Total Quantity
    const qty = parseInt(data.total_quantity);
    const usedCount = isEdit && voucher ? voucher.usedQuantity : 0;
    if (isNaN(qty) || qty <= 0) {
      errors.total_quantity = "Total quantity must be greater than 0.";
    } else if (isEdit && qty < usedCount) {
      errors.total_quantity = `Total quantity cannot be less than already used quantity (${usedCount}).`;
    }

    // Max usage per user
    const maxUser = parseInt(data.max_usage_per_user);
    if (isNaN(maxUser) || maxUser <= 0) {
      errors.max_usage_per_user = "Max usage per user must be at least 1.";
    }

    // Scope specifics
    if (data.scope === "ORGANIZER" && !data.organizer_id) {
      errors.organizer_id = "Organizer ID is required for ORGANIZER scope.";
    }
    if (data.scope === "EVENT" && !data.event_id) {
      errors.event_id = "Event ID is required for EVENT scope.";
    }

    // Dates
    if (!data.starts_at) {
      errors.starts_at = "Start date is required.";
    }
    if (!data.ends_at) {
      errors.ends_at = "End date is required.";
    }

    if (data.starts_at && data.ends_at) {
      const start = new Date(data.starts_at);
      const end = new Date(data.ends_at);

      if (end <= start) {
        errors.ends_at = "End date must be later than start date.";
      }

      if (isEdit && voucher) {
        const origStart = new Date(voucher.startsAt);
        if (origStart < now && usedCount > 0 && start > now) {
          errors.starts_at = "Cannot postpone start date to the future for an ongoing used voucher.";
        }
        if (end < now) {
          errors.ends_at = "New end date cannot be in the past. To stop immediately, use Pause/Deactivate.";
        }
      }
    }

    return errors;
  };

  const handleFieldChange = (field, value) => {
    const updated = { ...formData, [field]: value };
    setFormData(updated);
    const errors = validateForm(updated, modal === "edit", selectedVoucher);
    setFieldErrors(errors);
  };

  const handleToggleStatus = async (voucher) => {
    try {
      const nextStatus = !voucher.isActive;
      await voucherApi.updateVoucherStatus(voucher.voucherId, nextStatus);
      setSuccess(`Voucher '${voucher.code}' has been ${nextStatus ? "activated" : "paused"}.`);
      loadVouchers();
    } catch (err) {
      setError(err.response?.data?.message || "Failed to update voucher status.");
    }
  };

  const handleOpenUsages = async (voucher) => {
    setSelectedVoucher(voucher);
    setModal("usages");
    setUsagesLoading(true);
    try {
      const res = await voucherApi.getVoucherUsages(voucher.voucherId);
      setUsages(res.data?.data || []);
    } catch (err) {
      setError(err.response?.data?.message || "Failed to load voucher usage history.");
    } finally {
      setUsagesLoading(false);
    }
  };

  const handleOpenEdit = (voucher) => {
    const norm = normalizeVoucher(voucher);
    setSelectedVoucher(norm);
    const initialData = {
      code: norm.code || "",
      scope: norm.scope || "SYSTEM",
      organizer_id: norm.organizerId || "",
      event_id: norm.eventId || "",
      discount_type: norm.discountType || "PERCENT",
      discount_amount: norm.discountAmount ? String(norm.discountAmount) : "",
      discount_percent: norm.discountPercent ? String(norm.discountPercent) : "",
      max_discount_amount: norm.maxDiscountAmount ? String(norm.maxDiscountAmount) : "",
      min_order_amount: String(norm.minOrderAmount || 0),
      total_quantity: String(norm.totalQuantity || 100),
      max_usage_per_user: String(norm.maxUsagePerUser || 1),
      starts_at: formatDateForInput(norm.startsAt),
      ends_at: formatDateForInput(norm.endsAt),
    };

    setFormData(initialData);
    setFieldErrors({});
    setModal("edit");
  };

  const handleDeleteVoucher = async () => {
    if (!selectedVoucher) return;
    try {
      await voucherApi.deleteVoucher(selectedVoucher.voucherId);
      setSuccess(`Voucher '${selectedVoucher.code}' deleted successfully.`);
      closeModal();
      loadVouchers();
    } catch (err) {
      setError(err.response?.data?.message || "Failed to delete voucher.");
    }
  };

  const handleFormSubmit = async (e) => {
    e.preventDefault();

    const isEdit = modal === "edit";
    const errors = validateForm(formData, isEdit, selectedVoucher);
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setError("Please fix all form validation errors before submitting.");
      return;
    }

    try {
      const payload = {
        code: formData.code.trim().toUpperCase(),
        scope: formData.scope,
        organizer_id: formData.scope === "ORGANIZER" && formData.organizer_id ? parseInt(formData.organizer_id) : null,
        event_id: formData.scope === "EVENT" && formData.event_id ? parseInt(formData.event_id) : null,
        discount_type: formData.discount_type,
        discount_amount: formData.discount_type === "FIXED" ? parseInt(formData.discount_amount || 0) : null,
        discount_percent: formData.discount_type === "PERCENT" ? parseFloat(formData.discount_percent || 0) : null,
        max_discount_amount: formData.discount_type === "PERCENT" && formData.max_discount_amount ? parseInt(formData.max_discount_amount) : null,
        min_order_amount: parseInt(formData.min_order_amount || 0),
        total_quantity: parseInt(formData.total_quantity || 1),
        max_usage_per_user: parseInt(formData.max_usage_per_user || 1),
        starts_at: new Date(formData.starts_at).toISOString(),
        ends_at: new Date(formData.ends_at).toISOString(),
      };

      if (isEdit) {
        await voucherApi.updateVoucher(selectedVoucher.voucherId, payload);
        setSuccess(`Voucher '${payload.code}' updated successfully!`);
      } else {
        await voucherApi.createVoucher(payload);
        setSuccess(`Voucher '${payload.code}' created successfully!`);
      }

      closeModal();
      loadVouchers();
    } catch (err) {
      setError(err.response?.data?.message || `Failed to ${isEdit ? "update" : "create"} voucher.`);
    }
  };

  const resetForm = () => {
    setFormData({
      code: "",
      scope: "SYSTEM",
      organizer_id: "",
      event_id: "",
      discount_type: "PERCENT",
      discount_amount: "",
      discount_percent: "",
      max_discount_amount: "",
      min_order_amount: "0",
      total_quantity: "100",
      max_usage_per_user: "1",
      starts_at: "",
      ends_at: "",
    });
    setFieldErrors({});
  };

  const closeModal = () => {
    setModal(null);
    setSelectedVoucher(null);
    setUsages([]);
    setFieldErrors({});
  };

  const isIssued = modal === "edit" && selectedVoucher && selectedVoucher.usedQuantity > 0;

  return (
    <div className="admin-shell">
      <aside className="admin-sidebar">
        <div className="admin-brand">
          <div className="admin-brand-mark">CS</div>
          <h2>ConcertShield</h2>
        </div>
        <nav className="admin-nav" aria-label="Sidebar navigation">
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin")}>
            Dashboard
          </button>
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/users")}>
            User Management
          </button>
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/roles")}>
            Role Management
          </button>
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/reviews")}>
            Review Management
          </button>
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/wishlists")}>
            Wishlist Management
          </button>
          <button type="button" className="admin-nav-item" onClick={() => navigate("/admin/organizer-requests")}>
            Organizer Requests
          </button>
          <button type="button" className="admin-nav-item active" onClick={() => navigate("/admin/vouchers")}>
            Voucher Management
          </button>
        </nav>
        <div className="admin-user-box">
          <span>Logged in as</span>
          <strong>{user?.fullName || user?.email || "Admin"}</strong>
        </div>
      </aside>

      <main className="admin-main">
        <header className="admin-topbar">
          <div className="topbar-title">Admin Panel</div>
          <button
            type="button"
            className="admin-button"
            onClick={() => {
              logout();
              navigate("/admin/login");
            }}
          >
            Logout
          </button>
        </header>

        {error && (
          <div className="auth-message error">
            {error}
            <button type="button" onClick={() => setError("")}>
              Dismiss
            </button>
          </div>
        )}
        {success && (
          <div className="auth-message success">
            {success}
            <button type="button" onClick={() => setSuccess("")}>
              Dismiss
            </button>
          </div>
        )}

        <section className="admin-panel">
          <div className="panel-header">
            <div>
              <h2>Voucher Management</h2>
              <p style={{ margin: "4px 0 0", fontSize: "13px", color: "var(--admin-muted)" }}>
                Create, configure, and manage system & organizer discount vouchers.
              </p>
            </div>
            <button
              type="button"
              className="admin-button"
              onClick={() => {
                resetForm();
                setModal("create");
              }}
            >
              + Create New Voucher
            </button>
          </div>

          <div className="filter-tabs">
            <button
              type="button"
              className={`filter-tab ${selectedScope === "" ? "active" : ""}`}
              onClick={() => { setSelectedScope(""); setPage(1); }}
            >
              All Vouchers
            </button>
            <button
              type="button"
              className={`filter-tab ${selectedScope === "SYSTEM" ? "active" : ""}`}
              onClick={() => { setSelectedScope("SYSTEM"); setPage(1); }}
            >
              System Vouchers
            </button>
            <button
              type="button"
              className={`filter-tab ${selectedScope === "ORGANIZER" ? "active" : ""}`}
              onClick={() => { setSelectedScope("ORGANIZER"); setPage(1); }}
            >
              Organizer Vouchers
            </button>
            <button
              type="button"
              className={`filter-tab ${selectedScope === "EVENT" ? "active" : ""}`}
              onClick={() => { setSelectedScope("EVENT"); setPage(1); }}
            >
              Event Vouchers
            </button>
          </div>

          <div className="toolbar-row">
            <div className="search-box">
              <span>Search Code</span>
              <input
                type="search"
                value={searchCode}
                onChange={(e) => {
                  setSearchCode(e.target.value);
                  setPage(1);
                }}
                placeholder="e.g. PROMO2026..."
              />
            </div>
            <div className="search-box" style={{ width: "180px" }}>
              <span>Status Filter</span>
              <select
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
                style={{
                  width: "100%",
                  padding: "9px 10px",
                  border: "1px solid var(--admin-line)",
                  borderRadius: "6px",
                  fontSize: "13px",
                }}
              >
                <option value="">All Statuses</option>
                <option value="true">Active</option>
                <option value="false">Paused / Inactive</option>
              </select>
            </div>
          </div>

          {loading ? (
            <div className="empty-state">Loading vouchers...</div>
          ) : vouchers.length === 0 ? (
            <div className="empty-state">No vouchers found.</div>
          ) : (
            <div className="table-wrap">
              <table className="user-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Voucher Code</th>
                    <th>Voucher Type</th>
                    <th>Discount</th>
                    <th>Min Order</th>
                    <th>Usage & Quantity</th>
                    <th>Validity Period</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {vouchers.map((v) => {
                    const usagePercent = v.totalQuantity > 0 ? Math.round((v.usedQuantity / v.totalQuantity) * 100) : 0;
                    const isFull = v.usedQuantity >= v.totalQuantity;

                    return (
                      <tr key={v.voucherId}>
                        <td>#{v.voucherId}</td>
                        <td>
                          <span className="voucher-code-badge">{v.code}</span>
                        </td>
                        <td>
                          <span className={`badge-scope ${v.scope.toLowerCase()}`}>
                            {v.scope === "SYSTEM" ? "System" : v.scope === "ORGANIZER" ? "Organizer" : "Event"}
                          </span>
                        </td>
                        <td>
                          {v.discountType === "PERCENT" ? (
                            <div>
                              <strong>{v.discountPercent}% OFF</strong>
                              {v.maxDiscountAmount ? (
                                <div style={{ fontSize: "11px", color: "var(--admin-muted)" }}>
                                  Max {formatPrice(v.maxDiscountAmount)}
                                </div>
                              ) : null}
                            </div>
                          ) : (
                            <strong>{formatPrice(v.discountAmount)} OFF</strong>
                          )}
                        </td>
                        <td>{formatPrice(v.minOrderAmount)}</td>
                        <td>
                          <div className="voucher-progress-wrap">
                            <div className="voucher-progress-text">
                              <span>{v.usedQuantity} / {v.totalQuantity} used</span>
                              <span>{usagePercent}%</span>
                            </div>
                            <div className="voucher-progress-bar">
                              <div
                                className={`voucher-progress-fill ${isFull ? "danger" : usagePercent > 75 ? "warning" : ""}`}
                                style={{ width: `${Math.min(usagePercent, 100)}%` }}
                              />
                            </div>
                          </div>
                        </td>
                        <td style={{ fontSize: "12px", whiteSpace: "nowrap" }}>
                          <div>{formatDateTime(v.startsAt)}</div>
                          <div style={{ color: "var(--admin-muted)" }}>to {formatDateTime(v.endsAt)}</div>
                        </td>
                        <td>
                          <span className={`badge-status ${v.status.toLowerCase()}`}>
                            {v.status === "Active" ? "Active" : v.status === "Expired" ? "Expired" : "Paused"}
                          </span>
                        </td>
                        <td>
                          <div className="table-actions">
                            <button
                              type="button"
                              className={`switch-btn ${v.isActive ? "active-btn" : "inactive-btn"}`}
                              onClick={() => handleToggleStatus(v)}
                              title={v.isActive ? "Click to pause" : "Click to activate"}
                            >
                              {v.isActive ? "Pause" : "Activate"}
                            </button>
                            <button
                              type="button"
                              className="text-button"
                              onClick={() => handleOpenEdit(v)}
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              className="text-button"
                              onClick={() => handleOpenUsages(v)}
                            >
                              History
                            </button>
                            <button
                              type="button"
                              className="danger-button"
                              onClick={() => {
                                setSelectedVoucher(v);
                                setModal("delete");
                              }}
                            >
                              Delete
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>

              <div className="pagination-controls">
                <div>
                  Showing <strong>{vouchers.length}</strong> of <strong>{totalCount}</strong> vouchers (Page {page} of {totalPages})
                </div>
                <div className="pagination-btns">
                  <button
                    type="button"
                    className="admin-button-secondary"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <button
                    type="button"
                    className="admin-button-secondary"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    Next
                  </button>
                </div>
              </div>
            </div>
          )}
        </section>
      </main>

      {/* MODAL: CREATE OR EDIT VOUCHER */}
      {(modal === "create" || modal === "edit") && (
        <div className="modal-overlay" onClick={closeModal}>
          <div className="modal-card large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{modal === "edit" ? `Edit Voucher #${selectedVoucher?.voucherId} (${selectedVoucher?.code})` : "Create New Voucher"}</h3>
              <button type="button" className="icon-button" onClick={closeModal}>
                ×
              </button>
            </div>
            <form onSubmit={handleFormSubmit} className="modal-body form-grid">
              <label>
                <span>Voucher Code *</span>
                <input
                  type="text"
                  required
                  disabled={isIssued}
                  className={fieldErrors.code ? "input-error" : ""}
                  placeholder="e.g. SUMMER2026"
                  value={formData.code}
                  onChange={(e) => handleFieldChange("code", e.target.value.replace(/\s+/g, "").toUpperCase())}
                />
                {isIssued ? (
                  <span className="form-hint">Immutable: Code cannot be changed once voucher has been used.</span>
                ) : (
                  <span className="form-hint">Uppercase string without spaces.</span>
                )}
                {fieldErrors.code && <span className="field-error-text">{fieldErrors.code}</span>}
              </label>

              <label>
                <span>Voucher Type (Scope) *</span>
                <select
                  disabled={isIssued}
                  value={formData.scope}
                  onChange={(e) => handleFieldChange("scope", e.target.value)}
                  style={{
                    padding: "9px 10px",
                    border: "1px solid var(--admin-line)",
                    borderRadius: "6px",
                    fontSize: "13px",
                  }}
                >
                  <option value="SYSTEM">SYSTEM (Platform-wide)</option>
                  <option value="ORGANIZER">ORGANIZER (Organizer-specific)</option>
                  <option value="EVENT">EVENT (Event-specific)</option>
                </select>
                {isIssued && <span className="form-hint">Immutable: Scope cannot be changed once voucher has been used.</span>}
                {fieldErrors.scope && <span className="field-error-text">{fieldErrors.scope}</span>}
              </label>

              {formData.scope === "ORGANIZER" && (
                <label>
                  <span>Organizer ID *</span>
                  <input
                    type="number"
                    required
                    disabled={isIssued}
                    className={fieldErrors.organizer_id ? "input-error" : ""}
                    placeholder="Enter Organizer User ID"
                    value={formData.organizer_id}
                    onChange={(e) => handleFieldChange("organizer_id", e.target.value)}
                  />
                  {fieldErrors.organizer_id && <span className="field-error-text">{fieldErrors.organizer_id}</span>}
                </label>
              )}

              {formData.scope === "EVENT" && (
                <label>
                  <span>Event ID *</span>
                  <input
                    type="number"
                    required
                    disabled={isIssued}
                    className={fieldErrors.event_id ? "input-error" : ""}
                    placeholder="Enter Event ID"
                    value={formData.event_id}
                    onChange={(e) => handleFieldChange("event_id", e.target.value)}
                  />
                  {fieldErrors.event_id && <span className="field-error-text">{fieldErrors.event_id}</span>}
                </label>
              )}

              <label className={formData.scope === "SYSTEM" ? "full-width" : ""}>
                <span>Discount Type *</span>
                <select
                  disabled={isIssued}
                  value={formData.discount_type}
                  onChange={(e) => handleFieldChange("discount_type", e.target.value)}
                  style={{
                    padding: "9px 10px",
                    border: "1px solid var(--admin-line)",
                    borderRadius: "6px",
                    fontSize: "13px",
                  }}
                >
                  <option value="PERCENT">PERCENT (%)</option>
                  <option value="FIXED">FIXED Amount (VNĐ)</option>
                </select>
                {isIssued && <span className="form-hint">Immutable: Discount type cannot be switched once used.</span>}
              </label>

              {formData.discount_type === "PERCENT" ? (
                <>
                  <label>
                    <span>Discount Percentage (%) *</span>
                    <input
                      type="number"
                      step="0.01"
                      min="0.01"
                      max="100"
                      required
                      className={fieldErrors.discount_percent ? "input-error" : ""}
                      placeholder="e.g. 15"
                      value={formData.discount_percent}
                      onChange={(e) => handleFieldChange("discount_percent", e.target.value)}
                    />
                    {fieldErrors.discount_percent && <span className="field-error-text">{fieldErrors.discount_percent}</span>}
                  </label>
                  <label>
                    <span>Max Discount Cap (VNĐ)</span>
                    <input
                      type="number"
                      min="0"
                      placeholder="e.g. 50000"
                      value={formData.max_discount_amount}
                      onChange={(e) => handleFieldChange("max_discount_amount", e.target.value)}
                    />
                    <span className="form-hint">Leave blank for no upper limit cap.</span>
                  </label>
                </>
              ) : (
                <label className="full-width">
                  <span>Fixed Discount Amount (VNĐ) *</span>
                  <input
                    type="number"
                    min="1000"
                    required
                    className={fieldErrors.discount_amount ? "input-error" : ""}
                    placeholder="e.g. 50000"
                    value={formData.discount_amount}
                    onChange={(e) => handleFieldChange("discount_amount", e.target.value)}
                  />
                  {fieldErrors.discount_amount && <span className="field-error-text">{fieldErrors.discount_amount}</span>}
                </label>
              )}

              <label>
                <span>Minimum Order Value (VNĐ)</span>
                <input
                  type="number"
                  min="0"
                  value={formData.min_order_amount}
                  onChange={(e) => handleFieldChange("min_order_amount", e.target.value)}
                />
              </label>

              <label>
                <span>Total Issue Quantity *</span>
                <input
                  type="number"
                  min={modal === "edit" && selectedVoucher ? selectedVoucher.usedQuantity : 1}
                  required
                  className={fieldErrors.total_quantity ? "input-error" : ""}
                  value={formData.total_quantity}
                  onChange={(e) => handleFieldChange("total_quantity", e.target.value)}
                />
                {modal === "edit" && selectedVoucher && selectedVoucher.usedQuantity > 0 && (
                  <span className="form-hint">Constraint: Must be &ge; used count ({selectedVoucher.usedQuantity}).</span>
                )}
                {fieldErrors.total_quantity && <span className="field-error-text">{fieldErrors.total_quantity}</span>}
              </label>

              <label>
                <span>Max Usage Per User *</span>
                <input
                  type="number"
                  min="1"
                  required
                  className={fieldErrors.max_usage_per_user ? "input-error" : ""}
                  value={formData.max_usage_per_user}
                  onChange={(e) => handleFieldChange("max_usage_per_user", e.target.value)}
                />
                {fieldErrors.max_usage_per_user && <span className="field-error-text">{fieldErrors.max_usage_per_user}</span>}
              </label>

              <div className="full-width form-row">
                <label>
                  <span>Starts At *</span>
                  <input
                    type="datetime-local"
                    required
                    className={fieldErrors.starts_at ? "input-error" : ""}
                    value={formData.starts_at}
                    onChange={(e) => handleFieldChange("starts_at", e.target.value)}
                  />
                  {fieldErrors.starts_at && <span className="field-error-text">{fieldErrors.starts_at}</span>}
                </label>
                <label>
                  <span>Ends At *</span>
                  <input
                    type="datetime-local"
                    required
                    className={fieldErrors.ends_at ? "input-error" : ""}
                    value={formData.ends_at}
                    onChange={(e) => handleFieldChange("ends_at", e.target.value)}
                  />
                  {fieldErrors.ends_at && <span className="field-error-text">{fieldErrors.ends_at}</span>}
                </label>
              </div>

              <div className="modal-footer full-width">
                <button type="button" className="admin-button-secondary" onClick={closeModal}>
                  Cancel
                </button>
                <button type="submit" className="admin-button">
                  {modal === "edit" ? "Save Voucher Changes" : "Confirm Create Voucher"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: VOUCHER USAGE HISTORY */}
      {modal === "usages" && selectedVoucher && (
        <div className="modal-overlay" onClick={closeModal}>
          <div className="modal-card large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>
                Usage History for Voucher: <span className="voucher-code-badge">{selectedVoucher.code}</span>
              </h3>
              <button type="button" className="icon-button" onClick={closeModal}>
                ×
              </button>
            </div>
            <div className="modal-body">
              <div style={{ marginBottom: "16px", display: "flex", gap: "20px", fontSize: "13px" }}>
                <div>
                  Used: <strong>{selectedVoucher.usedQuantity} / {selectedVoucher.totalQuantity}</strong>
                </div>
                <div>
                  Type: <strong>{selectedVoucher.scope}</strong>
                </div>
              </div>

              {usagesLoading ? (
                <div className="empty-state">Loading history...</div>
              ) : usages.length === 0 ? (
                <div className="empty-state">No orders have redeemed this voucher yet.</div>
              ) : (
                <div className="table-wrap">
                  <table className="user-table">
                    <thead>
                      <tr>
                        <th>Transaction ID</th>
                        <th>Order ID</th>
                        <th>User ID</th>
                        <th>Discounted Amount</th>
                        <th>Redeemed At</th>
                      </tr>
                    </thead>
                    <tbody>
                      {usages.map((u) => (
                        <tr key={u.voucherUsageId}>
                          <td>#{u.voucherUsageId}</td>
                          <td>
                            <strong>#{u.orderId}</strong>
                          </td>
                          <td>#{u.userId}</td>
                          <td>
                            <span style={{ color: "#16a34a", fontWeight: 600 }}>
                              -{formatPrice(u.discountAmount)}
                            </span>
                          </td>
                          <td>{formatDateTime(u.usedAt)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
            <div className="modal-footer">
              <button type="button" className="admin-button" onClick={closeModal}>
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL: DELETE CONFIRMATION */}
      {modal === "delete" && selectedVoucher && (
        <div className="modal-overlay" onClick={closeModal}>
          <div className="modal-card small" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Confirm Voucher Deletion</h3>
              <button type="button" className="icon-button" onClick={closeModal}>
                ×
              </button>
            </div>
            <div className="modal-body">
              <p>
                Are you sure you want to delete voucher <strong>{selectedVoucher.code}</strong>?
              </p>
              <p style={{ fontSize: "12px", color: "var(--admin-muted)", margin: "8px 0 0" }}>
                This action soft-deletes the voucher. Customers will no longer be able to apply it.
              </p>
            </div>
            <div className="modal-footer">
              <button type="button" className="admin-button-secondary" onClick={closeModal}>
                Cancel
              </button>
              <button type="button" className="admin-button danger" onClick={handleDeleteVoucher}>
                Confirm Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
