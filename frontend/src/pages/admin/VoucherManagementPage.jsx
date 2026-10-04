import { useEffect, useState, useCallback } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import voucherApi from "../../api/voucherApi";
import { useAuth } from "../../context/AuthContext";
import AdminShell from "./AdminShell";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
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
  usedQuantity: v.used_quantity ?? v.usedQuantity ?? v.used_count ?? v.usedCount ?? v.usages_count ?? v.usagesCount ?? 0,
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
  const location = useLocation();
  const { user, isAuthenticated, isAdmin, isOrganizer } = useAuth();
  const isAdminView = location.pathname.startsWith("/admin");
  const currentUserId = user?.userId ?? user?.UserId ?? user?.id ?? user?.Id;

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
  const [submitting, setSubmitting] = useState(false);

  // Helper date formatter for HTML datetime-local input
  const formatDateForInput = (dateObj) => {
    if (!dateObj) return "";
    const d = new Date(dateObj);
    if (isNaN(d.getTime())) return "";
    const pad = (n) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  };

  // Form State
  const [formData, setFormData] = useState({
    code: "",
    scope: isAdminView ? "SYSTEM" : "ORGANIZER",
    organizer_id: "",
    event_id: "",
    discount_type: "PERCENT",
    discount_amount: "",
    discount_percent: "10",
    max_discount_amount: "",
    min_order_amount: "0",
    total_quantity: "100",
    max_usage_per_user: "1",
    starts_at: "",
    ends_at: "",
  });

  // Real-time Field Validation Errors
  const [fieldErrors, setFieldErrors] = useState({});

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
        organizer_id: !isAdminView && currentUserId ? currentUserId : undefined,
      };

      const response = await voucherApi.getVouchers(params);
      const data = response.data?.data;
      if (data) {
        const rawItems = data.items || data.Items || [];
        setVouchers(rawItems.map(normalizeVoucher));
        setTotalPages(data.total_pages ?? data.totalPages ?? 1);
        setTotalCount(data.total_count ?? data.totalCount ?? 0);
      }
    } catch (err) {
      const msg = err.response?.data?.message || err.response?.data?.errors?.[0] || "Failed to load vouchers.";
      setError(msg);
    } finally {
      setLoading(false);
    }
  }, [page, limit, selectedScope, statusFilter, searchCode, isAdminView, currentUserId]);

  useEffect(() => {
    loadVouchers();
  }, [loadVouchers]);

  // Real-time validator
  const validateForm = (data, isEdit = false, voucher = null) => {
    const errors = {};

    // Voucher Code: 3-20 uppercase alphanumeric characters, _ or -
    if (!data.code || !data.code.trim()) {
      errors.code = "Voucher code is required.";
    } else if (/\s/.test(data.code)) {
      errors.code = "Voucher code cannot contain spaces.";
    } else if (!/^[A-Z0-9_-]{3,20}$/i.test(data.code.trim())) {
      errors.code = "Voucher code must be 3-20 uppercase alphanumeric characters and unique.";
    }

    // Scope & Scope specifics
    if (data.scope === "ORGANIZER") {
      if (isAdminView && (!data.organizer_id || parseInt(data.organizer_id) <= 0)) {
        errors.organizer_id = "Please enter a valid Organizer User ID.";
      }
    }
    if (data.scope === "EVENT") {
      if (!data.event_id || parseInt(data.event_id) <= 0) {
        errors.event_id = "Organizers can only create vouchers for their own events.";
      }
    }

    // Discount Type & Values
    const minOrderVal = data.min_order_amount ? parseInt(data.min_order_amount) : 0;
    if (data.discount_type === "PERCENT") {
      const val = parseFloat(data.discount_percent);
      if (isNaN(val) || val < 1 || val > 100) {
        errors.discount_percent = "Percentage discount must be between 1% and 100%.";
      }
      if (data.max_discount_amount) {
        const cap = parseInt(data.max_discount_amount);
        if (isNaN(cap) || cap < 0) {
          errors.max_discount_amount = "Max discount amount must be greater than or equal to 0.";
        }
      }
    } else if (data.discount_type === "FIXED") {
      const val = parseInt(data.discount_amount);
      if (isNaN(val) || val <= 0) {
        errors.discount_amount = "Fixed discount amount must be greater than 0 VNĐ.";
      } else if (minOrderVal > 0 && val > minOrderVal) {
        errors.discount_amount = "Fixed discount amount cannot exceed minimum order amount.";
      }
    }

    // Minimum Order Amount
    if (data.min_order_amount) {
      if (isNaN(minOrderVal) || minOrderVal < 0) {
        errors.min_order_amount = "Minimum order amount cannot be negative.";
      }
    }

    // Total Issue Quantity
    const qty = parseInt(data.total_quantity);
    const usedCount = isEdit && voucher ? voucher.usedQuantity : 0;
    if (isNaN(qty) || qty < 1) {
      errors.total_quantity = "Total quantity must be at least 1.";
    } else if (isEdit && qty < usedCount) {
      errors.total_quantity = "Usage limit cannot be less than the number of already used vouchers.";
    }

    // Max usage per user
    const maxUser = parseInt(data.max_usage_per_user);
    if (isNaN(maxUser) || maxUser < 1) {
      errors.max_usage_per_user = "Max usage per user must be at least 1.";
    } else if (!isNaN(qty) && maxUser > qty) {
      errors.max_usage_per_user = "Max usage per user cannot exceed total usage limit.";
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

      if (isNaN(start.getTime())) {
        errors.starts_at = "Invalid start date format.";
      }
      if (isNaN(end.getTime())) {
        errors.ends_at = "Invalid end date format.";
      }

      if (!isNaN(start.getTime()) && !isNaN(end.getTime())) {
        if (end <= start) {
          errors.ends_at = "End date must be after start date.";
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
      setError(err.response?.data?.message || "Failed to load usage history.");
    } finally {
      setUsagesLoading(false);
    }
  };

  const handleOpenEdit = (voucher) => {
    const norm = normalizeVoucher(voucher);
    setSelectedVoucher(norm);
    const initialData = {
      code: norm.code || "",
      scope: norm.scope || (isAdminView ? "SYSTEM" : "ORGANIZER"),
      organizer_id: norm.organizerId ? String(norm.organizerId) : (isAdminView ? "" : String(currentUserId || "")),
      event_id: norm.eventId ? String(norm.eventId) : "",
      discount_type: norm.discountType || "PERCENT",
      discount_amount: norm.discountAmount ? String(norm.discountAmount) : "",
      discount_percent: norm.discountPercent ? String(norm.discountPercent) : "10",
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
      setError("Please fix all errors before submitting.");
      return;
    }

    try {
      setSubmitting(true);
      setError("");
      const startsDate = new Date(formData.starts_at);
      const endsDate = new Date(formData.ends_at);

      const payload = {
        code: formData.code.trim().toUpperCase(),
        scope: formData.scope,
        organizer_id: !isAdminView
          ? (currentUserId ? parseInt(currentUserId) : null)
          : (formData.scope === "ORGANIZER" || formData.scope === "EVENT") && formData.organizer_id
            ? parseInt(formData.organizer_id)
            : null,
        event_id: formData.scope === "EVENT" && formData.event_id ? parseInt(formData.event_id) : null,
        discount_type: formData.discount_type,
        discount_amount: formData.discount_type === "FIXED" ? parseInt(formData.discount_amount || 0) : null,
        discount_percent: formData.discount_type === "PERCENT" ? parseFloat(formData.discount_percent || 0) : null,
        max_discount_amount: formData.discount_type === "PERCENT" && formData.max_discount_amount ? parseInt(formData.max_discount_amount) : null,
        min_order_amount: parseInt(formData.min_order_amount || 0),
        total_quantity: parseInt(formData.total_quantity || 1),
        max_usage_per_user: parseInt(formData.max_usage_per_user || 1),
        starts_at: startsDate.toISOString(),
        ends_at: endsDate.toISOString(),
      };

      if (isEdit) {
        await voucherApi.updateVoucher(selectedVoucher.voucherId, payload);
        setSuccess(`Voucher '${payload.code}' updated successfully!`);
      } else {
        await voucherApi.createVoucher(payload);
        setSuccess(`Voucher '${payload.code}' created successfully!`);

        const notifPayload = {
          title: `New Voucher: ${payload.code}`,
          message: `Special discount voucher '${payload.code}' is now available! Apply it at checkout to get discount.`,
          category: "voucher_new",
          targetUrl: "/events",
          timestamp: Date.now()
        };

        // 1. Storage event for cross-window / cross-tab immediate wakeup
        try {
          localStorage.setItem("cs_latest_notification", JSON.stringify(notifPayload));
        } catch (e) {}

        // 2. BroadcastChannel for instant messaging across tabs
        try {
          const bc = new BroadcastChannel("concertshield_notifications");
          bc.postMessage(notifPayload);
          bc.close();
        } catch (e) {}

        // 3. Local window events (always — toast guard is in NotificationToast.jsx)
        window.dispatchEvent(new CustomEvent("notification-updated"));
        window.dispatchEvent(new CustomEvent("show-notification-toast", {
          detail: notifPayload
        }));
      }

      closeModal();
      loadVouchers();
    } catch (err) {
      const msg = err.response?.data?.message || err.response?.data?.errors?.[0] || `Failed to ${isEdit ? "update" : "create"} voucher.`;
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const resetForm = () => {
    const now = new Date();
    const nextMonth = new Date();
    nextMonth.setDate(now.getDate() + 30);

    setFormData({
      code: "",
      scope: isAdminView ? "SYSTEM" : "ORGANIZER",
      organizer_id: isAdminView ? "" : String(currentUserId || ""),
      event_id: "",
      discount_type: "PERCENT",
      discount_amount: "",
      discount_percent: "10",
      max_discount_amount: "",
      min_order_amount: "0",
      total_quantity: "100",
      max_usage_per_user: "1",
      starts_at: formatDateForInput(now),
      ends_at: formatDateForInput(nextMonth),
    });
    setFieldErrors({});
  };

  const closeModal = () => {
    setModal(null);
    setSelectedVoucher(null);
    setUsages([]);
    setFieldErrors({});
  };

  const isIssued = modal === "edit";

  const mainContent = (
    <div>
      {error && (
        <div className="alert alert-error" style={{ marginBottom: "16px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span>{error}</span>
          <button type="button" onClick={() => setError("")} style={{ background: "none", border: "none", cursor: "pointer", color: "inherit", fontWeight: 700 }}>✕</button>
        </div>
      )}
      {success && (
        <div className="alert alert-success" style={{ marginBottom: "16px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span>{success}</span>
          <button type="button" onClick={() => setSuccess("")} style={{ background: "none", border: "none", cursor: "pointer", color: "inherit", fontWeight: 700 }}>✕</button>
        </div>
      )}

      <section className={isAdminView ? "admin-panel" : "organizer-voucher-card"}>
        <div className="panel-header" style={{ marginBottom: "20px" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "22px", fontWeight: 800, color: isAdminView ? "var(--admin-ink)" : "#0f172a" }}>
              {isAdminView ? "System & Event Voucher Management" : "Voucher Management"}
            </h2>
            <p style={{ margin: "6px 0 0", fontSize: "13.5px", color: isAdminView ? "var(--admin-muted)" : "#475569" }}>
              {isAdminView
                ? "Configure and manage platform-wide (SYSTEM), organizer-specific, and event vouchers."
                : "Create and manage promotional discount vouchers for your events."}
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

        {/* Filter Tabs */}
        <div className="filter-tabs">
          <button
            type="button"
            className={`filter-tab ${selectedScope === "" ? "active" : ""}`}
            onClick={() => { setSelectedScope(""); setPage(1); }}
          >
            {isAdminView ? "All Vouchers" : "All My Vouchers"}
          </button>
          {isAdminView && (
            <button
              type="button"
              className={`filter-tab ${selectedScope === "SYSTEM" ? "active" : ""}`}
              onClick={() => { setSelectedScope("SYSTEM"); setPage(1); }}
            >
              System Vouchers
            </button>
          )}
          <button
            type="button"
            className={`filter-tab ${selectedScope === "ORGANIZER" ? "active" : ""}`}
            onClick={() => { setSelectedScope("ORGANIZER"); setPage(1); }}
          >
            {isAdminView ? "Organizer Vouchers" : "All Events Vouchers"}
          </button>
          <button
            type="button"
            className={`filter-tab ${selectedScope === "EVENT" ? "active" : ""}`}
            onClick={() => { setSelectedScope("EVENT"); setPage(1); }}
          >
            {isAdminView ? "Event Vouchers" : "Specific Event Vouchers"}
          </button>
        </div>

        {/* Toolbar: Search & Status Filter */}
        {isAdminView ? (
          <div className="toolbar-row" style={{ display: "flex", gap: "12px", alignItems: "center", marginBottom: "16px" }}>
            <div className="search-box" style={{ flex: 1 }}>
              <span>Search Code</span>
              <input
                type="search"
                value={searchCode}
                onChange={(e) => {
                  setSearchCode(e.target.value);
                  setPage(1);
                }}
                placeholder="e.g. SUMMER2026, PROMO10..."
              />
            </div>
            <div className="search-box" style={{ width: "200px" }}>
              <span>Status</span>
              <select
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All Statuses</option>
                <option value="true">Active</option>
                <option value="false">Paused / Inactive</option>
              </select>
            </div>
          </div>
        ) : (
          <div className="organizer-filter-row">
            <div className="organizer-search-field">
              <span className="organizer-field-label">Search Code</span>
              <div className="organizer-input-box">
                <svg width="15" height="15" viewBox="0 0 20 20" fill="none" style={{ color: "#64748b" }}>
                  <path d="M9 3.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM17 17l-3.5-3.5" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
                </svg>
                <input
                  type="search"
                  className="organizer-filter-input"
                  value={searchCode}
                  onChange={(e) => {
                    setSearchCode(e.target.value);
                    setPage(1);
                  }}
                  placeholder="Search by voucher code (e.g. SUMMER2026)..."
                />
              </div>
            </div>

            <div className="organizer-status-field">
              <span className="organizer-field-label">Status</span>
              <select
                className="organizer-filter-select"
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setPage(1);
                }}
              >
                <option value="">All Statuses</option>
                <option value="true">Active Only</option>
                <option value="false">Paused / Inactive</option>
              </select>
            </div>
          </div>
        )}

        {loading ? (
          <div className="empty-state">Loading vouchers...</div>
        ) : vouchers.length === 0 ? (
          <div className="empty-state">No vouchers found matching the filter criteria.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Voucher Code</th>
                  <th>Scope</th>
                  <th>Discount</th>
                  <th>Min Order</th>
                  <th>Usage & Limit</th>
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
                          {v.scope === "SYSTEM"
                            ? "System-wide"
                            : v.scope === "ORGANIZER"
                              ? (isAdminView ? `Organizer #${v.organizerId}` : "All My Events")
                              : `Event #${v.eventId}`}
                        </span>
                      </td>
                      <td>
                        {v.discountType === "PERCENT" ? (
                          <div>
                            <strong style={{ color: "#7c3aed" }}>{v.discountPercent}% OFF</strong>
                            {v.maxDiscountAmount ? (
                              <div style={{ fontSize: "11px", color: "var(--admin-muted, #6b7280)" }}>
                                Max {formatPrice(v.maxDiscountAmount)}
                              </div>
                            ) : null}
                          </div>
                        ) : (
                          <strong style={{ color: "#059669" }}>{formatPrice(v.discountAmount)} OFF</strong>
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
                        <div style={{ color: "var(--admin-muted, #6b7280)" }}>to {formatDateTime(v.endsAt)}</div>
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
                            title={v.isActive ? "Click to pause voucher" : "Click to activate voucher"}
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

            <div className="pagination-controls" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "16px" }}>
              <div>
                Showing <strong>{vouchers.length}</strong> of <strong>{totalCount}</strong> vouchers (Page {page} of {totalPages})
              </div>
              <div className="pagination-btns" style={{ display: "flex", gap: "8px" }}>
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
    </div>
  );

  const renderModals = () => (
    <>
      {/* MODAL: CREATE OR EDIT VOUCHER */}
      {(modal === "create" || modal === "edit") && (
        <div className="modal-overlay" onClick={closeModal}>
          <div className="modal-card large" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>
                {modal === "edit"
                  ? `Edit Voucher #${selectedVoucher?.voucherId} (${selectedVoucher?.code})`
                  : !isAdminView
                    ? "Create New Event Voucher"
                    : "Create New Voucher"}
              </h3>
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
                  disabled={modal === "edit"}
                  className={fieldErrors.code ? "input-error" : ""}
                  placeholder="e.g. SUMMER2026, PROMO10..."
                  value={formData.code}
                  onChange={(e) => handleFieldChange("code", e.target.value.replace(/\s+/g, "").toUpperCase())}
                />
                {modal === "edit" ? (
                  <span className="form-hint" style={{ color: "#d97706" }}>Immutable: Voucher code cannot be edited after creation.</span>
                ) : (
                  <span className="form-hint">Uppercase letters and numbers, no spaces (e.g. VIP2026).</span>
                )}
                {fieldErrors.code && <span className="field-error-text">{fieldErrors.code}</span>}
              </label>

              <label>
                <span>Voucher Scope *</span>
                <select
                  disabled={isIssued}
                  value={formData.scope}
                  onChange={(e) => handleFieldChange("scope", e.target.value)}
                  style={{
                    padding: "9px 10px",
                    border: "1px solid var(--admin-line, #d1d5db)",
                    borderRadius: "6px",
                    fontSize: "13px",
                  }}
                >
                  {isAdminView && <option value="SYSTEM">System-wide (All events on platform)</option>}
                  <option value="ORGANIZER">
                    {isAdminView ? "Organizer-specific (All events of organizer)" : "All My Events (Valid for all your events)"}
                  </option>
                  <option value="EVENT">
                    {isAdminView ? "Event-specific (Single event)" : "Specific Event (Valid for one event only)"}
                  </option>
                </select>
                {isIssued && <span className="form-hint" style={{ color: "#d97706" }}>Scope cannot be changed once voucher has been used.</span>}
                {fieldErrors.scope && <span className="field-error-text">{fieldErrors.scope}</span>}
              </label>

              {/* Scope ORGANIZER: Admin inputs Organizer ID, Organizer sees informational text */}
              {formData.scope === "ORGANIZER" && (
                isAdminView ? (
                  <label>
                    <span>Organizer User ID *</span>
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
                ) : (
                  <div className="full-width" style={{ padding: "10px 14px", background: "#f8fafc", borderRadius: "8px", border: "1px solid #e2e8f0", fontSize: "13px", color: "#475569" }}>
                    ✓ This voucher will automatically apply to <strong>all events</strong> hosted under your organizer account (User ID: #{currentUserId || "..."}).
                  </div>
                )
              )}

              {/* Scope EVENT: Event ID input */}
              {formData.scope === "EVENT" && (
                <label className={!isAdminView ? "full-width" : ""}>
                  <span>Event ID *</span>
                  <input
                    type="number"
                    required
                    disabled={isIssued}
                    className={fieldErrors.event_id ? "input-error" : ""}
                    placeholder="Enter the Event ID to apply this voucher"
                    value={formData.event_id}
                    onChange={(e) => handleFieldChange("event_id", e.target.value)}
                  />
                  <span className="form-hint">Enter your numeric Event ID (e.g. 1).</span>
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
                    border: "1px solid var(--admin-line, #d1d5db)",
                    borderRadius: "6px",
                    fontSize: "13px",
                  }}
                >
                  <option value="PERCENT">Percentage Discount (%)</option>
                  <option value="FIXED">Fixed Amount Discount (VNĐ)</option>
                </select>
                {isIssued && <span className="form-hint" style={{ color: "#d97706" }}>Discount type cannot be changed once used.</span>}
              </label>

              {formData.discount_type === "PERCENT" ? (
                <>
                  <label>
                    <span>Discount Percentage (%) *</span>
                    <input
                      type="number"
                      step="0.01"
                      min="1"
                      max="100"
                      required
                      disabled={isIssued}
                      className={fieldErrors.discount_percent ? "input-error" : ""}
                      placeholder="e.g. 15"
                      value={formData.discount_percent}
                      onChange={(e) => handleFieldChange("discount_percent", e.target.value)}
                    />
                    {isIssued && <span className="form-hint" style={{ color: "#d97706" }}>Discount percentage cannot be changed once used.</span>}
                    {fieldErrors.discount_percent && <span className="field-error-text">{fieldErrors.discount_percent}</span>}
                  </label>
                  <label>
                    <span>Max Discount Cap (VNĐ)</span>
                    <input
                      type="number"
                      min="0"
                      step="1000"
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
                    step="1000"
                    required
                    disabled={isIssued}
                    className={fieldErrors.discount_amount ? "input-error" : ""}
                    placeholder="e.g. 50000"
                    value={formData.discount_amount}
                    onChange={(e) => handleFieldChange("discount_amount", e.target.value)}
                  />
                  {isIssued && <span className="form-hint" style={{ color: "#d97706" }}>Fixed discount amount cannot be changed once used.</span>}
                  {fieldErrors.discount_amount && <span className="field-error-text">{fieldErrors.discount_amount}</span>}
                </label>
              )}

              <label>
                <span>Minimum Order Value (VNĐ)</span>
                <input
                  type="number"
                  min="0"
                  step="1000"
                  value={formData.min_order_amount}
                  onChange={(e) => handleFieldChange("min_order_amount", e.target.value)}
                />
                <span className="form-hint">Minimum purchase total required (0 = no minimum).</span>
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
                  <span className="form-hint">Must be greater than or equal to used count ({selectedVoucher.usedQuantity}).</span>
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
                <span className="form-hint">Maximum times one customer can use this voucher.</span>
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
                <button type="button" className="admin-button-secondary" onClick={closeModal} disabled={submitting}>
                  Cancel
                </button>
                <button type="submit" className="admin-button" disabled={submitting}>
                  {submitting ? "Processing..." : modal === "edit" ? "Save Voucher Changes" : "Confirm Create Voucher"}
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
                  Scope: <strong>{selectedVoucher.scope}</strong>
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
                        <th>Discount Amount</th>
                        <th>Redeemed At</th>
                      </tr>
                    </thead>
                    <tbody>
                      {usages.map((u) => (
                        <tr key={u.voucherUsageId ?? u.voucher_usage_id}>
                          <td>#{u.voucherUsageId ?? u.voucher_usage_id}</td>
                          <td>
                            <strong>#{u.orderId ?? u.order_id}</strong>
                          </td>
                          <td>#{u.userId ?? u.user_id}</td>
                          <td>
                            <span style={{ color: "#16a34a", fontWeight: 600 }}>
                              -{formatPrice(u.discountAmount ?? u.discount_amount)}
                            </span>
                          </td>
                          <td>{formatDateTime(u.usedAt ?? u.used_at)}</td>
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
              <p style={{ fontSize: "12px", color: "var(--admin-muted, #6b7280)", margin: "8px 0 0" }}>
                This action will deactivate the voucher. Customers will no longer be able to apply this code at checkout.
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
    </>
  );

  if (!isAdminView) {
    return (
      <div className="tb-app">
        <Header />
        <div className="tb-container" style={{ padding: "30px 20px", minHeight: "75vh" }}>
          {mainContent}
        </div>
        {renderModals()}
        <Footer />
      </div>
    );
  }

  return (
    <AdminShell title="Voucher Management">
      {mainContent}
      {renderModals()}
    </AdminShell>
  );
}
