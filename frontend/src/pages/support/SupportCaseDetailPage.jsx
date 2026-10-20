import { useEffect, useState } from "react";
import { useParams, useNavigate, useLocation, Link } from "react-router-dom";
import supportCaseApi from "../../api/supportCaseApi";
import adminApi from "../../api/adminApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "../staff/StaffShell";
import AdminShell from "../admin/AdminShell";
import ProcessRefundModal from "../../components/refund/ProcessRefundModal";
import "../../styles/supportCaseManagement.css";

// Format helper
const formatDate = (dateStr) => {
  if (!dateStr) return "—";
  const d = new Date(dateStr);
  return d.toLocaleString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

export default function SupportCaseDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();
  const toast = useToast();

  const isAdminPath = location.pathname.startsWith("/admin");
  const Shell = isAdminPath ? AdminShell : StaffShell;

  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);

  // Modal states
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [showResolveModal, setShowResolveModal] = useState(false);
  const [showRefundModal, setShowRefundModal] = useState(false);

  // Assign staff form state
  const [staffList, setStaffList] = useState([]);
  const [selectedStaffId, setSelectedStaffId] = useState("");
  const [customStaffName, setCustomStaffName] = useState("");
  const [assigning, setAssigning] = useState(false);

  // Resolve case form state
  const [resolutionText, setResolutionText] = useState("");
  const [resolving, setResolving] = useState(false);

  // Add note state
  const [noteContent, setNoteContent] = useState("");
  const [addingNote, setAddingNote] = useState(false);

  // Load support case details
  const fetchDetail = async () => {
    try {
      const res = await supportCaseApi.getCaseDetail(id);
      setDetail(res.data);
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to load support case details.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchDetail();
  }, [id]);

  // Load staff candidates when opening assign modal (Admin)
  const handleOpenAssignModal = async () => {
    setShowAssignModal(true);
    if (staffList.length === 0 && isAdminPath) {
      try {
        const res = await adminApi.getUsers();
        const users = res.data || [];
        const eligible = users.filter((u) => {
          const role = (u.role || u.roleName || "").toLowerCase();
          return role.includes("staff") || role.includes("admin");
        });
        setStaffList(eligible.length > 0 ? eligible : users);
      } catch (err) {
        // Fallback to manual entry if needed
      }
    }
  };

  // Assign staff
  const handleAssignStaff = async (e) => {
    e.preventDefault();
    if (!selectedStaffId) {
      toast.error("Please select a staff member.");
      return;
    }

    const staffObj = staffList.find((s) => String(s.id || s.userId) === String(selectedStaffId));
    const staffName = staffObj ? (staffObj.fullName || staffObj.userName || staffObj.name) : customStaffName;
    const staffEmail = staffObj?.email || "";

    if (!staffName) {
      toast.error("Please enter the staff member name.");
      return;
    }

    setAssigning(true);
    try {
      const res = await supportCaseApi.assignStaff(id, {
        staffUserId: parseInt(selectedStaffId, 10),
        staffName,
        staffEmail,
      });
      toast.success(`Case assigned to ${staffName}! Staff has been notified.`);
      setDetail(res.data);
      setShowAssignModal(false);
    } catch (err) {
      // Cannot reassign closed cases
      toast.error(err.response?.data?.message || "Failed to assign case.");
    } finally {
      setAssigning(false);
    }
  };

  // Resolve and close case
  const handleResolveCase = async (e) => {
    e.preventDefault();
    if (!resolutionText.trim()) {
      toast.error("Resolution summary is strictly required to close a case.");
      return;
    }

    setResolving(true);
    try {
      const res = await supportCaseApi.resolveCase(id, {
        resolution: resolutionText.trim(),
      });
      toast.success("Support case has been resolved and closed successfully!");
      setDetail(res.data);
      setShowResolveModal(false);
      setResolutionText("");
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to close support case.");
    } finally {
      setResolving(false);
    }
  };

  // Add internal note
  const handleAddNote = async (e) => {
    e.preventDefault();
    if (!noteContent.trim()) return;

    setAddingNote(true);
    try {
      await supportCaseApi.addNote(id, {
        content: noteContent.trim(),
        noteType: "Comment",
      });
      toast.success("Internal note added.");
      setNoteContent("");
      await fetchDetail();
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to add note.");
    } finally {
      setAddingNote(false);
    }
  };

  if (loading) {
    return (
      <Shell title="Support Case Details">
        <div className="sc-container" style={{ textAlign: "center", padding: "60px" }}>
          Loading case information...
        </div>
      </Shell>
    );
  }

  if (!detail) {
    return (
      <Shell title="Case Not Found">
        <div className="sc-container" style={{ textAlign: "center", padding: "60px" }}>
          <h2>Support Case Not Found</h2>
          <Link to={isAdminPath ? "/admin/support-cases" : "/staff/support-cases"} className="sc-btn sc-btn-primary">
            Back to List
          </Link>
        </div>
      </Shell>
    );
  }

  const isClosed = detail.status === "Closed" || detail.status === "Resolved";

  return (
    <Shell title={`Case #${detail.caseNumber} - ${detail.title}`}>
      <div className="sc-container">
        {/* Header */}
        <div className="sc-header">
          <div className="sc-header-title">
            <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
              <span className="sc-badge-pill">#{detail.caseNumber}</span>
              <span className={`sc-priority-badge sc-priority-${(detail.priority || "").toLowerCase()}`}>
                {detail.priority} Priority
              </span>
              <span className={`sc-status-badge sc-status-${(detail.status || "").toLowerCase()}`}>
                {detail.status}
              </span>
            </div>
            <h1>{detail.title}</h1>
            <p>
              Created by <strong>{detail.createdByName}</strong> on {formatDate(detail.createdAt)} • Category:{" "}
              <strong>{detail.category}</strong>
            </p>
          </div>

          <div className="sc-header-actions">
            <Link
              to={isAdminPath ? "/admin/support-cases" : "/staff/support-cases"}
              className="sc-btn sc-btn-outline"
            >
              ← Back to List
            </Link>

            {/* Assign Staff (Admin only, disabled if Closed) */}
            {isAdminPath && (
              <button
                type="button"
                className="sc-btn sc-btn-secondary"
                onClick={handleOpenAssignModal}
                disabled={isClosed}
                title={isClosed ? "Closed case cannot be reassigned" : "Assign staff member"}
              >
                {detail.assignedStaffName ? "Reassign Staff" : "Assign Staff"}
              </button>
            )}

            {/* Resolve and Close Case */}
            {!isClosed && (
              <button
                type="button"
                className="sc-btn sc-btn-success"
                onClick={() => setShowResolveModal(true)}
              >
                Resolve & Close Case
              </button>
            )}
          </div>
        </div>

        {/* Layout: Main column + Sidebar meta */}
        <div className="sc-detail-layout">
          {/* Main content */}
          <div>
            {/* Resolution Banner if resolved or closed */}
            {detail.resolution && (
              <div className="sc-resolution-box">
                <div className="sc-resolution-title">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  Recorded Resolution
                </div>
                <div style={{ color: "#d1fae5", fontSize: "14px", lineHeight: 1.6, whiteSpace: "pre-wrap" }}>
                  {detail.resolution}
                </div>
                <div style={{ fontSize: "12px", color: "#6ee7b7", marginTop: "10px" }}>
                  Resolved by <strong>{detail.resolvedByName || "Staff"}</strong> on{" "}
                  {formatDate(detail.resolvedAt)}
                </div>
              </div>
            )}

            {/* Case description */}
            <div className="sc-main-card">
              <h2 className="sc-card-title">Case Description</h2>
              <div
                style={{
                  fontSize: "14px",
                  lineHeight: 1.7,
                  color: "#f3f4f6",
                  whiteSpace: "pre-wrap",
                  background: "var(--sc-bg-surface)",
                  padding: "16px 20px",
                  borderRadius: "8px",
                  border: "1px solid var(--sc-card-border)",
                }}
              >
                {detail.description}
              </div>
            </div>

            {/* Activity & Notes Timeline */}
            <div className="sc-main-card">
              <h2 className="sc-card-title">
                <span>Activity & Notes Timeline ({detail.notes?.length || 0})</span>
              </h2>

              {detail.notes?.length === 0 ? (
                <p style={{ color: "var(--sc-text-muted)", fontSize: "13px" }}>
                  No activity notes recorded yet for this case.
                </p>
              ) : (
                <div className="sc-timeline">
                  {detail.notes?.map((n) => (
                    <div key={n.noteId} className="sc-timeline-item">
                      <div className="sc-timeline-dot" />
                      <div className="sc-timeline-meta">
                        <div>
                          <strong style={{ color: "#fff" }}>{n.authorName}</strong>{" "}
                          <span style={{ color: "var(--sc-text-muted)" }}>({n.authorRole})</span>
                          {n.noteType && n.noteType !== "Comment" && (
                            <span
                              style={{
                                marginLeft: "8px",
                                background: "#334155",
                                padding: "2px 6px",
                                borderRadius: "4px",
                                fontSize: "11px",
                              }}
                            >
                              {n.noteType}
                            </span>
                          )}
                        </div>
                        <div>{formatDate(n.createdAt)}</div>
                      </div>
                      <div className="sc-timeline-content">{n.content}</div>
                    </div>
                  ))}
                </div>
              )}

              {/* Add internal note form */}
              {!isClosed && (
                <form onSubmit={handleAddNote} style={{ marginTop: "24px" }}>
                  <div className="sc-form-group">
                    <label className="sc-form-label">Add Internal Note / Case Progress</label>
                    <textarea
                      className="sc-form-textarea"
                      rows={3}
                      placeholder="Add an internal investigation update, call summary, or handover note..."
                      value={noteContent}
                      onChange={(e) => setNoteContent(e.target.value)}
                    />
                  </div>
                  <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "10px" }}>
                    <button
                      type="submit"
                      className="sc-btn sc-btn-primary sc-btn-sm"
                      disabled={addingNote || !noteContent.trim()}
                    >
                      {addingNote ? "Saving..." : "Post Note"}
                    </button>
                  </div>
                </form>
              )}
            </div>
          </div>

          {/* Sidebar Metadata */}
          <div>
            {/* Customer Information Card */}
            <div className="sc-meta-card">
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#fff" }}>
                Customer Information
              </h3>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Full Name</span>
                <span className="sc-meta-val">{detail.customerName}</span>
              </div>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Email</span>
                <span className="sc-meta-val">{detail.customerEmail}</span>
              </div>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Phone</span>
                <span className="sc-meta-val">{detail.customerPhone || "Not provided"}</span>
              </div>
              {detail.customerId && (
                <div className="sc-meta-row">
                  <span className="sc-meta-label">User ID</span>
                  <span className="sc-meta-val">#{detail.customerId}</span>
                </div>
              )}
            </div>

            {/* Related Context Card */}
            <div className="sc-meta-card">
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#fff" }}>
                Context References
              </h3>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Order ID</span>
                <span className="sc-meta-val">
                  {detail.orderId ? (
                    <Link
                      to={`/my-orders/${detail.orderId}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      title="Open order details in a new tab"
                      style={{
                        color: "#60a5fa",
                        textDecoration: "underline",
                        display: "inline-flex",
                        alignItems: "center",
                        gap: "4px",
                        fontWeight: 600,
                      }}
                    >
                      #{detail.orderId}
                      <span style={{ fontSize: "11px" }}>↗</span>
                    </Link>
                  ) : (
                    <span style={{ color: "var(--sc-text-muted)" }}>None</span>
                  )}
                </span>
              </div>
              {detail.orderId && (
                <button
                  type="button"
                  onClick={() => setShowRefundModal(true)}
                  style={{
                    marginTop: "8px",
                    marginBottom: "12px",
                    width: "100%",
                    background: "linear-gradient(135deg, #ec4899 0%, #a855f7 100%)",
                    border: "none",
                    color: "#fff",
                    padding: "7px 12px",
                    borderRadius: "6px",
                    fontSize: "12px",
                    fontWeight: 700,
                    cursor: "pointer",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    gap: "6px",
                    boxShadow: "0 2px 8px rgba(236, 72, 153, 0.3)",
                  }}
                >
                  <span>⟲</span> Process Refund
                </button>
              )}
              <div className="sc-meta-row">
                <span className="sc-meta-label">Ticket ID</span>
                <span className="sc-meta-val">
                  {detail.ticketId ? (
                    <Link
                      to={`/my-tickets/${detail.ticketId}`}
                      target="_blank"
                      rel="noopener noreferrer"
                      title="Open ticket details in a new tab"
                      style={{
                        color: "#60a5fa",
                        textDecoration: "underline",
                        display: "inline-flex",
                        alignItems: "center",
                        gap: "4px",
                        fontWeight: 600,
                      }}
                    >
                      #{detail.ticketId}
                      <span style={{ fontSize: "11px" }}>↗</span>
                    </Link>
                  ) : (
                    <span style={{ color: "var(--sc-text-muted)" }}>None</span>
                  )}
                </span>
              </div>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Payment Reference</span>
                <span className="sc-meta-val">
                  {detail.paymentReference ? (
                    <code style={{ color: "#a5b4fc" }}>{detail.paymentReference}</code>
                  ) : (
                    <span style={{ color: "var(--sc-text-muted)" }}>None</span>
                  )}
                </span>
              </div>
            </div>

            {/* Assignment & Timestamps Card */}
            <div className="sc-meta-card">
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#fff" }}>
                Assignment & Timestamps
              </h3>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Assigned Staff</span>
                <span className="sc-meta-val" style={{ color: detail.assignedStaffName ? "#93c5fd" : "#9ca3af" }}>
                  {detail.assignedStaffName || "Unassigned"}
                </span>
              </div>
              {detail.assignedAt && (
                <div className="sc-meta-row">
                  <span className="sc-meta-label">Assigned At</span>
                  <span className="sc-meta-val">{formatDate(detail.assignedAt)}</span>
                </div>
              )}
              <div className="sc-meta-row">
                <span className="sc-meta-label">Created At</span>
                <span className="sc-meta-val">{formatDate(detail.createdAt)}</span>
              </div>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Last Updated</span>
                <span className="sc-meta-val">{formatDate(detail.updatedAt)}</span>
              </div>
            </div>
          </div>
        </div>

        {/* MODAL: Assign Staff */}
        {showAssignModal && (
          <div className="sc-modal-backdrop" onClick={() => setShowAssignModal(false)}>
            <div className="sc-modal" onClick={(e) => e.stopPropagation()}>
              <div className="sc-modal-header">
                <h3>Assign Support Staff</h3>
                <button
                  type="button"
                  onClick={() => setShowAssignModal(false)}
                  style={{ background: "none", border: "none", color: "#9ca3af", cursor: "pointer", fontSize: "18px" }}
                >
                  ✕
                </button>
              </div>

              <form onSubmit={handleAssignStaff}>
                <div className="sc-modal-body">
                  <p style={{ margin: 0, fontSize: "13px", color: "var(--sc-text-secondary)" }}>
                    Select an eligible staff member to take ownership of case #{detail.caseNumber}.
                  </p>

                  <div className="sc-form-group">
                    <label className="sc-form-label">Eligible Staff Member</label>
                    <select
                      className="sc-form-input"
                      value={selectedStaffId}
                      onChange={(e) => {
                        setSelectedStaffId(e.target.value);
                        const found = staffList.find((s) => String(s.id || s.userId) === e.target.value);
                        if (found) {
                          setCustomStaffName(found.fullName || found.userName || found.name);
                        }
                      }}
                      required
                    >
                      <option value="">-- Select Staff Member --</option>
                      {staffList.map((st) => (
                        <option key={st.id || st.userId} value={st.id || st.userId}>
                          {st.fullName || st.userName || st.name} ({st.email})
                        </option>
                      ))}
                    </select>
                  </div>

                  {staffList.length === 0 && (
                    <div className="sc-form-group">
                      <label className="sc-form-label">Staff Member Name</label>
                      <input
                        type="text"
                        className="sc-form-input"
                        placeholder="Enter staff member name"
                        value={customStaffName}
                        onChange={(e) => setCustomStaffName(e.target.value)}
                        required
                      />
                    </div>
                  )}
                </div>

                <div className="sc-modal-footer">
                  <button
                    type="button"
                    className="sc-btn sc-btn-outline"
                    onClick={() => setShowAssignModal(false)}
                    disabled={assigning}
                  >
                    Cancel
                  </button>
                  <button type="submit" className="sc-btn sc-btn-primary" disabled={assigning}>
                    {assigning ? "Saving..." : "Confirm Assignment"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL: Resolve & Close Case */}
        {showResolveModal && (
          <div className="sc-modal-backdrop" onClick={() => setShowResolveModal(false)}>
            <div className="sc-modal" onClick={(e) => e.stopPropagation()}>
              <div className="sc-modal-header">
                <h3>Resolve & Close Support Case</h3>
                <button
                  type="button"
                  onClick={() => setShowResolveModal(false)}
                  style={{ background: "none", border: "none", color: "#9ca3af", cursor: "pointer", fontSize: "18px" }}
                >
                  ✕
                </button>
              </div>

              <form onSubmit={handleResolveCase}>
                <div className="sc-modal-body">
                  <div style={{ background: "rgba(245, 158, 11, 0.1)", border: "1px solid rgba(245, 158, 11, 0.3)", padding: "12px", borderRadius: "8px", fontSize: "12px", color: "#fcd34d" }}>
                    <strong>Notice:</strong> Closing this case strictly requires a detailed resolution to be recorded. Closed cases cannot be reopened or reassigned.
                  </div>

                  <div className="sc-form-group">
                    <label className="sc-form-label">
                      Resolution Summary <span style={{ color: "var(--sc-danger)" }}>*</span>
                    </label>
                    <textarea
                      className="sc-form-textarea"
                      rows={5}
                      placeholder="Detail the resolution provided to the customer (e.g., refund processed via payment gateway, new QR ticket issued, or policy explanation provided)..."
                      value={resolutionText}
                      onChange={(e) => setResolutionText(e.target.value)}
                      required
                    />
                  </div>
                </div>

                <div className="sc-modal-footer">
                  <button
                    type="button"
                    className="sc-btn sc-btn-outline"
                    onClick={() => setShowResolveModal(false)}
                    disabled={resolving}
                  >
                    Cancel
                  </button>
                  <button type="submit" className="sc-btn sc-btn-success" disabled={resolving}>
                    {resolving ? "Closing..." : "Confirm & Close Case"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {showRefundModal && detail.orderId && (
          <ProcessRefundModal
            orderId={detail.orderId}
            initialCustomerName={detail.customerName}
            onClose={() => setShowRefundModal(false)}
            onSuccess={(res) => {
              toast.success(`Refund processed successfully (${res.reference || "Confirmed"}).`);
              setResolutionText(
                (prev) => (prev ? prev + "\n" : "") + `[Refund Issued] Refund reference: ${res.reference || "Completed"}`
              );
              fetchDetail();
            }}
          />
        )}
      </div>
    </Shell>
  );
}
