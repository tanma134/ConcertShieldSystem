import { useEffect, useState, useCallback } from "react";
import OrganizerShell from "./OrganizerShell";
import staffApi from "../../api/staffApi";
import { formatDateTime } from "../../utils/format";
import "./OrganizerStaffPage.css";

export default function OrganizerStaffPage() {
  const [staffList, setStaffList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [toast, setToast] = useState(null);
  const [confirmModal, setConfirmModal] = useState(null);

  // Modal State
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingStaff, setEditingStaff] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});

  const [formData, setFormData] = useState({
    fullName: "",
    email: "",
    phoneNumber: "",
    password: "",
    isActive: true,
  });

  const showToast = (message, type = "success") => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 4000);
  };

  const loadStaff = useCallback(async (query = "") => {
    setLoading(true);
    setError("");
    try {
      const res = await staffApi.getOrganizerStaff(query);
      setStaffList(res.data?.data || []);
    } catch (err) {
      console.error("Failed to load staff list:", err);
      setError(err.response?.data?.message || "Could not load staff list.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadStaff();
  }, [loadStaff]);

  const handleSearch = (e) => {
    e.preventDefault();
    loadStaff(searchQuery.trim());
  };

  const validateForm = (isEdit = false) => {
    const errors = {};
    if (!formData.fullName || !formData.fullName.trim()) {
      errors.fullName = "Full name is required.";
    }
    if (!formData.email || !formData.email.trim()) {
      errors.email = "Email address is required.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.email.trim())) {
      errors.email = "Please enter a valid email address.";
    }
    if (formData.phoneNumber && formData.phoneNumber.trim()) {
      const cleanPhone = formData.phoneNumber.trim();
      if (!/^(0|\+84)[0-9]{9,10}$/.test(cleanPhone)) {
        errors.phoneNumber = "Phone number must be 10 digits starting with 0 or +84.";
      } else {
        const dup = staffList.find(
          (s) => s.phoneNumber === cleanPhone && (!isEdit || s.userId !== editingStaff?.userId)
        );
        if (dup) {
          errors.phoneNumber = "This phone number is already registered to another staff member.";
        }
      }
    }
    if (!isEdit) {
      if (!formData.password || formData.password.length < 6) {
        errors.password = "Password must be at least 6 characters.";
      }
    } else if (formData.password && formData.password.length < 6) {
      errors.password = "New password must be at least 6 characters.";
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const openCreateModal = () => {
    setFormData({
      fullName: "",
      email: "",
      phoneNumber: "",
      password: "",
      isActive: true,
    });
    setFormError("");
    setFieldErrors({});
    setIsCreateModalOpen(true);
  };

  const openEditModal = (staff) => {
    setEditingStaff(staff);
    setFormData({
      fullName: staff.fullName,
      email: staff.email,
      phoneNumber: staff.phoneNumber || "",
      password: "",
      isActive: staff.isActive,
    });
    setFormError("");
    setFieldErrors({});
  };

  const closeModal = () => {
    setIsCreateModalOpen(false);
    setEditingStaff(null);
    setFormError("");
    setFieldErrors({});
  };

  const handleSubmitCreate = async (e) => {
    e.preventDefault();
    setFormError("");

    if (!validateForm(false)) {
      return;
    }

    setSubmitting(true);
    try {
      await staffApi.createOrganizerStaff({
        fullName: formData.fullName.trim(),
        email: formData.email.trim(),
        phoneNumber: formData.phoneNumber.trim() || null,
        password: formData.password,
      });

      showToast(`Staff member "${formData.fullName}" created successfully!`);
      closeModal();
      loadStaff(searchQuery.trim());
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to create staff account.";
      if (msg.toLowerCase().includes("phone")) {
        setFieldErrors((prev) => ({ ...prev, phoneNumber: msg }));
      } else if (msg.toLowerCase().includes("email")) {
        setFieldErrors((prev) => ({ ...prev, email: msg }));
      } else {
        setFormError(msg);
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleSubmitEdit = async (e) => {
    e.preventDefault();
    if (!editingStaff) return;
    setFormError("");

    if (!validateForm(true)) {
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        fullName: formData.fullName.trim(),
        phoneNumber: formData.phoneNumber.trim() || null,
        isActive: formData.isActive,
      };
      if (formData.password) {
        payload.password = formData.password;
      }

      await staffApi.updateOrganizerStaff(editingStaff.userId, payload);
      showToast(`Updated staff member "${formData.fullName}" successfully!`);
      closeModal();
      loadStaff(searchQuery.trim());
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to update staff member.";
      if (msg.toLowerCase().includes("phone")) {
        setFieldErrors((prev) => ({ ...prev, phoneNumber: msg }));
      } else {
        setFormError(msg);
      }
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleStatus = (staff) => {
    setConfirmModal({
      title: staff.isActive ? "Deactivate Staff Account" : "Activate Staff Account",
      message: `Are you sure you want to ${staff.isActive ? "deactivate (lock)" : "activate"} the account of "${staff.fullName}"? ${staff.isActive ? "This staff member will no longer be able to log in or scan admission tickets." : "This staff member will be granted check-in access."}`,
      confirmText: staff.isActive ? "Deactivate Account" : "Activate Account",
      confirmBtnStyle: staff.isActive
        ? { background: "linear-gradient(135deg, #d97706, #b45309)", color: "#ffffff" }
        : { background: "linear-gradient(135deg, #059669, #047857)", color: "#ffffff" },
      onConfirm: async () => {
        try {
          await staffApi.updateOrganizerStaff(staff.userId, {
            isActive: !staff.isActive,
          });
          showToast(`Account "${staff.fullName}" is now ${staff.isActive ? "deactivated" : "active"}.`);
          loadStaff(searchQuery.trim());
        } catch (err) {
          showToast(err.response?.data?.message || "Could not change account status.", "error");
        } finally {
          setConfirmModal(null);
        }
      },
    });
  };

  const handleDeleteStaff = (staff) => {
    setConfirmModal({
      title: "Delete Staff Member",
      message: `Are you sure you want to permanently delete staff member "${staff.fullName}" (${staff.email})? This action cannot be undone.`,
      confirmText: "Delete Staff Member",
      confirmBtnStyle: { background: "linear-gradient(135deg, #dc2626, #b91c1c)", color: "#ffffff" },
      onConfirm: async () => {
        try {
          await staffApi.deleteOrganizerStaff(staff.userId);
          showToast(`Staff account "${staff.fullName}" deleted successfully.`);
          loadStaff(searchQuery.trim());
        } catch (err) {
          showToast(err.response?.data?.message || "Could not delete staff member.", "error");
        } finally {
          setConfirmModal(null);
        }
      },
    });
  };

  // Map each staff member to their sequential creation index for this organizer (#1, #2, ...)
  const staffOrderMap = new Map();
  const sortedStaffChronological = [...staffList].sort(
    (a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0) || a.userId - b.userId
  );
  sortedStaffChronological.forEach((s, idx) => {
    staffOrderMap.set(s.userId, idx + 1);
  });

  return (
    <OrganizerShell title="Staff Management" theme="light">
      <div className="tb-container osp-wrap">
        <div className="osp-header">
          <div className="osp-title-group">
            <h1>Staff Accounts Management</h1>
            <p className="osp-subtitle">
              Create, configure, and manage event check-in staff accounts and admission access credentials.
            </p>
          </div>
          <button
            type="button"
            onClick={openCreateModal}
            style={{
              padding: "10px 20px",
              borderRadius: "8px",
              fontWeight: 600,
              fontSize: "0.92rem",
              background: "linear-gradient(135deg, #d61f6e, #be185d)",
              color: "#ffffff",
              border: "none",
              cursor: "pointer",
              boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)",
              whiteSpace: "nowrap"
            }}
          >
            + Add New Staff Member
          </button>
        </div>

        {/* Toolbar & Filter */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "12px", marginBottom: "20px" }}>
          {/* Status Filter Pills (like Organizer requests image) */}
          <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => setStatusFilter("ALL")}
              style={{
                padding: "6px 18px",
                borderRadius: "20px",
                fontSize: "0.88rem",
                fontWeight: 600,
                cursor: "pointer",
                border: statusFilter === "ALL" ? "1px solid #be185d" : "1px solid #e5e7eb",
                background: statusFilter === "ALL" ? "#be185d" : "#f3f4f6",
                color: statusFilter === "ALL" ? "#ffffff" : "#374151",
                transition: "all 0.15s ease",
              }}
            >
              All ({staffList.length})
            </button>
            <button
              type="button"
              onClick={() => setStatusFilter("ACTIVE")}
              style={{
                padding: "6px 18px",
                borderRadius: "20px",
                fontSize: "0.88rem",
                fontWeight: 600,
                cursor: "pointer",
                border: statusFilter === "ACTIVE" ? "1px solid #be185d" : "1px solid #e5e7eb",
                background: statusFilter === "ACTIVE" ? "#be185d" : "#f3f4f6",
                color: statusFilter === "ACTIVE" ? "#ffffff" : "#374151",
                transition: "all 0.15s ease",
              }}
            >
              Active ({staffList.filter((s) => s.isActive).length})
            </button>
            <button
              type="button"
              onClick={() => setStatusFilter("INACTIVE")}
              style={{
                padding: "6px 18px",
                borderRadius: "20px",
                fontSize: "0.88rem",
                fontWeight: 600,
                cursor: "pointer",
                border: statusFilter === "INACTIVE" ? "1px solid #be185d" : "1px solid #e5e7eb",
                background: statusFilter === "INACTIVE" ? "#be185d" : "#f3f4f6",
                color: statusFilter === "INACTIVE" ? "#ffffff" : "#374151",
                transition: "all 0.15s ease",
              }}
            >
              Deactivated ({staffList.filter((s) => !s.isActive).length})
            </button>
          </div>

          <form className="osp-toolbar" onSubmit={handleSearch} style={{ margin: 0, padding: "8px 12px", borderRadius: "10px" }}>
            <input
              type="text"
              className="osp-search-input"
              placeholder="Search staff by name, email, or phone..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
            <button type="submit" className="tb-btn tb-btn-outline" style={{ padding: "6px 14px" }}>
              Search
            </button>
            {searchQuery && (
              <button
                type="button"
                className="tb-btn tb-btn-ghost"
                style={{ padding: "6px 10px" }}
                onClick={() => {
                  setSearchQuery("");
                  loadStaff("");
                }}
              >
                Clear
              </button>
            )}
          </form>
        </div>

        {loading && <div className="tb-loading">Loading staff directory...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && (
          <div className="osp-table-card">
            {staffList.filter((s) => {
              if (statusFilter === "ACTIVE") return s.isActive;
              if (statusFilter === "INACTIVE") return !s.isActive;
              return true;
            }).length === 0 ? (
              <div style={{ padding: "48px 24px", textAlign: "center", color: "#6b7280" }}>
                No staff members found matching filter.
              </div>
            ) : (
              <table className="osp-table">
                <thead>
                  <tr>
                    <th>Staff Member</th>
                    <th>Email Address</th>
                    <th>Phone Number</th>
                    <th>Status</th>
                    <th>Created At</th>
                    <th style={{ textAlign: "right" }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {staffList
                    .filter((s) => {
                      if (statusFilter === "ACTIVE") return s.isActive;
                      if (statusFilter === "INACTIVE") return !s.isActive;
                      return true;
                    })
                    .map((staff) => (
                      <tr key={staff.userId}>
                        <td>
                          <div className="osp-name">{staff.fullName}</div>
                        </td>
                        <td>{staff.email}</td>
                        <td>{staff.phoneNumber || "—"}</td>
                        <td>
                          <span
                            className={`osp-badge ${
                              staff.isActive ? "osp-badge-active" : "osp-badge-inactive"
                            }`}
                          >
                            {staff.isActive ? "Active" : "Deactivated"}
                          </span>
                        </td>
                        <td>{formatDateTime(staff.createdAt)}</td>
                        <td>
                          <div className="osp-actions">
                            <button
                              type="button"
                              className="osp-btn osp-btn-edit"
                              onClick={() => openEditModal(staff)}
                              title="Edit profile & password"
                            >
                              Edit
                            </button>
                            <button
                              type="button"
                              className={`osp-btn ${
                                staff.isActive ? "osp-btn-lock" : "osp-btn-unlock"
                              }`}
                              onClick={() => handleToggleStatus(staff)}
                              title={staff.isActive ? "Lock staff account" : "Activate staff account"}
                            >
                              {staff.isActive ? "Lock" : "Activate"}
                            </button>
                            <button
                              type="button"
                              className="osp-btn osp-btn-danger"
                              onClick={() => handleDeleteStaff(staff)}
                              title="Remove staff member"
                            >
                              Delete
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            )}
          </div>
        )}
      </div>

      {/* Modal: Create Staff Member */}
      {isCreateModalOpen && (
        <div className="osp-modal-overlay" onClick={closeModal}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()}>
            <div className="osp-modal-header">
              <h2>Add New Staff Account</h2>
              <button type="button" className="osp-modal-close" onClick={closeModal}>
                ×
              </button>
            </div>
            <form onSubmit={handleSubmitCreate}>
              <div className="osp-modal-body">
                {formError && <div className="tb-error" style={{ marginBottom: "16px" }}>{formError}</div>}
                <div className="osp-form-group">
                  <label>Full Name *</label>
                  <input
                    type="text"
                    required
                    placeholder="Enter full name"
                    value={formData.fullName}
                    onChange={(e) => {
                      setFormData({ ...formData, fullName: e.target.value });
                      if (fieldErrors.fullName) setFieldErrors({ ...fieldErrors, fullName: "" });
                    }}
                  />
                  {fieldErrors.fullName && <div className="osp-field-error">{fieldErrors.fullName}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Login Email Address *</label>
                  <input
                    type="email"
                    required
                    placeholder="Enter email address"
                    value={formData.email}
                    onChange={(e) => {
                      setFormData({ ...formData, email: e.target.value });
                      if (fieldErrors.email) setFieldErrors({ ...fieldErrors, email: "" });
                    }}
                  />
                  {fieldErrors.email && <div className="osp-field-error">{fieldErrors.email}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Phone Number</label>
                  <input
                    type="text"
                    placeholder="Enter phone number"
                    value={formData.phoneNumber}
                    onChange={(e) => {
                      setFormData({ ...formData, phoneNumber: e.target.value });
                      if (fieldErrors.phoneNumber) setFieldErrors({ ...fieldErrors, phoneNumber: "" });
                    }}
                  />
                  {fieldErrors.phoneNumber && <div className="osp-field-error">{fieldErrors.phoneNumber}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Initial Password * (minimum 6 characters)</label>
                  <input
                    type="password"
                    required
                    minLength={6}
                    placeholder="Enter password"
                    value={formData.password}
                    onChange={(e) => {
                      setFormData({ ...formData, password: e.target.value });
                      if (fieldErrors.password) setFieldErrors({ ...fieldErrors, password: "" });
                    }}
                  />
                  {fieldErrors.password && <div className="osp-field-error">{fieldErrors.password}</div>}
                </div>
              </div>
              <div className="osp-modal-footer">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={closeModal}
                  disabled={submitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  style={{
                    padding: "8px 20px",
                    borderRadius: "8px",
                    fontWeight: 600,
                    fontSize: "0.9rem",
                    background: "linear-gradient(135deg, #d61f6e, #be185d)",
                    color: "#ffffff",
                    border: "none",
                    cursor: "pointer",
                    boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)"
                  }}
                >
                  {submitting ? "Creating..." : "Create Account"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Edit Staff Member */}
      {editingStaff && (
        <div className="osp-modal-overlay" onClick={closeModal}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()}>
            <div className="osp-modal-header">
              <h2>Edit Staff Member: {editingStaff.fullName}</h2>
              <button type="button" className="osp-modal-close" onClick={closeModal}>
                ×
              </button>
            </div>
            <form onSubmit={handleSubmitEdit}>
              <div className="osp-modal-body">
                {formError && <div className="tb-error" style={{ marginBottom: "16px" }}>{formError}</div>}
                <div className="osp-form-group">
                  <label>Email Address (Read-only)</label>
                  <input type="text" disabled value={editingStaff.email} style={{ background: "#f3f4f6" }} />
                </div>
                <div className="osp-form-group">
                  <label>Full Name *</label>
                  <input
                    type="text"
                    required
                    placeholder="Enter full name"
                    value={formData.fullName}
                    onChange={(e) => {
                      setFormData({ ...formData, fullName: e.target.value });
                      if (fieldErrors.fullName) setFieldErrors({ ...fieldErrors, fullName: "" });
                    }}
                  />
                  {fieldErrors.fullName && <div className="osp-field-error">{fieldErrors.fullName}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Phone Number</label>
                  <input
                    type="text"
                    placeholder="Enter phone number"
                    value={formData.phoneNumber}
                    onChange={(e) => {
                      setFormData({ ...formData, phoneNumber: e.target.value });
                      if (fieldErrors.phoneNumber) setFieldErrors({ ...fieldErrors, phoneNumber: "" });
                    }}
                  />
                  {fieldErrors.phoneNumber && <div className="osp-field-error">{fieldErrors.phoneNumber}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Reset Password (leave empty to keep current password)</label>
                  <input
                    type="password"
                    minLength={6}
                    placeholder="Enter new password if changing"
                    value={formData.password}
                    onChange={(e) => {
                      setFormData({ ...formData, password: e.target.value });
                      if (fieldErrors.password) setFieldErrors({ ...fieldErrors, password: "" });
                    }}
                  />
                  {fieldErrors.password && <div className="osp-field-error">{fieldErrors.password}</div>}
                </div>
                <div className="osp-form-group">
                  <label>Account Status</label>
                  <select
                    value={formData.isActive ? "1" : "0"}
                    onChange={(e) => setFormData({ ...formData, isActive: e.target.value === "1" })}
                  >
                    <option value="1">Active</option>
                    <option value="0">Deactivated / Locked</option>
                  </select>
                </div>
              </div>
              <div className="osp-modal-footer">
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={closeModal}
                  disabled={submitting}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  style={{
                    padding: "8px 20px",
                    borderRadius: "8px",
                    fontWeight: 600,
                    fontSize: "0.9rem",
                    background: "linear-gradient(135deg, #d61f6e, #be185d)",
                    color: "#ffffff",
                    border: "none",
                    cursor: "pointer",
                    boxShadow: "0 2px 6px rgba(190, 24, 93, 0.35)"
                  }}
                >
                  {submitting ? "Saving..." : "Save Changes"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Confirmation Popup Modal (replaces browser window.confirm) */}
      {confirmModal && (
        <div className="osp-modal-overlay" onClick={() => setConfirmModal(null)} style={{ zIndex: 9999 }}>
          <div className="osp-modal-box" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "460px" }}>
            <div className="osp-modal-header">
              <h2 style={{ fontSize: "1.2rem", fontWeight: 700, color: "#111827", margin: 0 }}>
                {confirmModal.title}
              </h2>
              <button type="button" className="osp-modal-close" onClick={() => setConfirmModal(null)}>
                ×
              </button>
            </div>
            <div className="osp-modal-body" style={{ padding: "18px 20px" }}>
              <p style={{ margin: 0, color: "#4b5563", fontSize: "0.95rem", lineHeight: "1.55" }}>
                {confirmModal.message}
              </p>
            </div>
            <div className="osp-modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px", padding: "14px 20px" }}>
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                onClick={() => setConfirmModal(null)}
                style={{ padding: "8px 18px", borderRadius: "8px", border: "1px solid #d1d5db", background: "#fff", color: "#374151", cursor: "pointer", fontWeight: 600 }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={confirmModal.onConfirm}
                style={{
                  padding: "8px 20px",
                  borderRadius: "8px",
                  fontWeight: 600,
                  fontSize: "0.9rem",
                  border: "none",
                  cursor: "pointer",
                  boxShadow: "0 2px 4px rgba(0,0,0,0.15)",
                  ...(confirmModal.confirmBtnStyle || { background: "linear-gradient(135deg, #d61f6e, #be185d)", color: "#ffffff" })
                }}
              >
                {confirmModal.confirmText || "Confirm"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Floating Action Feedback Popup / Toast */}
      {toast && (
        <div
          style={{
            position: "fixed",
            bottom: "28px",
            right: "28px",
            background: toast.type === "error" ? "#dc2626" : "#059669",
            color: "#ffffff",
            padding: "14px 20px",
            borderRadius: "10px",
            boxShadow: "0 10px 25px -5px rgba(0,0,0,0.25), 0 8px 10px -6px rgba(0,0,0,0.15)",
            zIndex: 1200,
            fontSize: "0.95rem",
            fontWeight: 600,
            display: "flex",
            alignItems: "center",
            gap: "10px",
            maxWidth: "420px",
          }}
        >
          <span style={{ flex: 1 }}>{toast.message}</span>
          <button
            type="button"
            onClick={() => setToast(null)}
            style={{ background: "none", border: "none", color: "#fff", cursor: "pointer", fontSize: "1.2rem", padding: "0 4px", opacity: 0.8 }}
          >
            ×
          </button>
        </div>
      )}
    </OrganizerShell>
  );
}
