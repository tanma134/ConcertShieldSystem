import { useEffect, useState } from "react";
import { useParams, useNavigate, useLocation, Link } from "react-router-dom";
import supportCaseApi from "../../api/supportCaseApi";
import adminApi from "../../api/adminApi";
import ticketApi from "../../api/ticketApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import StaffShell from "../staff/StaffShell";
import AdminShell from "../admin/AdminShell";
import ProcessRefundModal from "../../components/refund/ProcessRefundModal";
import "../../styles/supportCaseManagement.css";

// Customer wrapper shell
function CustomerSupportShell({ title, children }) {
  return (
    <>
      <Header />
      <main style={{ minHeight: "85vh", background: "#f8fafc", padding: "28px 0 60px 0" }}>
        {children}
      </main>
      <Footer />
    </>
  );
}

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
  const isStaffPath = location.pathname.startsWith("/staff");
  const isCustomerPath = !isAdminPath && !isStaffPath;
  const Shell = isAdminPath ? AdminShell : (isStaffPath ? StaffShell : CustomerSupportShell);

  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);

  // Modal states
  const [showAssignModal, setShowAssignModal] = useState(false);
  const [showResolveModal, setShowResolveModal] = useState(false);
  const [showRefundModal, setShowRefundModal] = useState(false);

  // Quick View Order Modal state
  const [showOrderModal, setShowOrderModal] = useState(false);
  const [orderModalData, setOrderModalData] = useState(null);
  const [loadingOrderModal, setLoadingOrderModal] = useState(false);

  // Evidence Preview Modal state
  const [showEvidenceModal, setShowEvidenceModal] = useState(false);
  const [evidencePreviewUrl, setEvidencePreviewUrl] = useState(null);
  const [evidenceFitMode, setEvidenceFitMode] = useState("width");
  const [evidenceZoom, setEvidenceZoom] = useState(100);

  useEffect(() => {
    if (!detail) {
      setEvidencePreviewUrl(null);
      return;
    }
    const match = detail.description?.match(/\[Supporting Evidence Attached:\s*([^\]]+)\]/i);
    const fname = match ? match[1].trim() : null;
    if (fname) {
      const stored =
        localStorage.getItem(`cs_evidence_${fname}`) ||
        localStorage.getItem(`cs_evidence_${detail.caseNumber}`) ||
        localStorage.getItem(`cs_evidence_case_${detail.caseId}`) ||
        localStorage.getItem(`cs_evidence_order_${detail.orderId}`) ||
        localStorage.getItem(`evidence_${fname}`);
      // Prioritize localStorage or static upload path from server
      setEvidencePreviewUrl(stored || `/uploads/${encodeURIComponent(fname)}`);
    }
  }, [detail]);

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
          const roles = Array.isArray(u.roles)
            ? u.roles.map((r) => (typeof r === "string" ? r : r.roleName || r.name || ""))
            : [u.roleName, u.role].filter(Boolean);
          return roles.some((r) => {
            const lower = String(r || "").toLowerCase();
            return (lower.includes("staff") || lower.includes("support")) && !lower.includes("admin");
          });
        });
        setStaffList(eligible);
      } catch (err) {
        // Fallback to manual entry if needed
      }
    }
  };

  // Open Quick View Order Modal
  const handleOpenOrderModal = async (orderId) => {
    if (!orderId) return;
    setShowOrderModal(true);
    setLoadingOrderModal(true);
    try {
      const res = await ticketApi.getOrderById(orderId);
      setOrderModalData(res.data?.data ?? res.data);
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to load order information.");
    } finally {
      setLoadingOrderModal(false);
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
          <span className="app-loading-spinner" role="status" aria-label="Loading" /></div>


      </Shell>
    );
  }

  if (!detail) {
    return (
      <Shell title="Case Not Found">
        <div className="sc-container" style={{ textAlign: "center", padding: "60px" }}>
          <h2>Support Case Not Found</h2>
          <Link to={isAdminPath ? "/admin/support-cases" : (isCustomer ? "/my-orders" : "/staff/support-cases")} className="sc-btn sc-btn-primary">
            Back to List
          </Link>
        </div>
      </Shell>
    );
  }

  const isClosed = ["Closed", "Resolved", "Dismissed"].includes(detail.status);
  const evidenceMatch = detail?.description?.match(/\[Supporting Evidence Attached:\s*([^\]]+)\]/i);
  const evidenceFileName = evidenceMatch ? evidenceMatch[1].trim() : null;
  const cleanDescription = detail?.description
    ? detail.description.replace(/\[Supporting Evidence Attached:\s*[^\]]+\]/gi, "").trim()
    : "";

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
            {isCustomerPath ? (
              <Link
                to={detail.orderId ? `/my-orders/${detail.orderId}` : "/my-orders"}
                className="sc-btn sc-btn-outline"
              >
                ← Back to Order
              </Link>
            ) : (
              <Link
                to={isAdminPath ? "/admin/support-cases" : "/staff/support-cases"}
                className="sc-btn sc-btn-outline"
              >
                ← Back to List
              </Link>
            )}

            {/* Assign Staff (Admin only) */}
            {isAdminPath && (
              <button
                type="button"
                className={`sc-btn sc-btn-secondary ${isClosed ? "sc-btn-disabled" : ""}`}
                onClick={
                  isClosed
                    ? () => toast.info("This case has been resolved and closed. Closed cases cannot be reassigned.")
                    : handleOpenAssignModal
                }
                style={isClosed ? { opacity: 0.5, cursor: "not-allowed" } : {}}
                title={isClosed ? "Closed case cannot be reassigned" : "Assign staff member"}
              >
                {detail.assignedStaffName ? "Reassign Staff" : "Assign Staff"}
              </button>
            )}

            {/* Resolve and Close Case (Staff / Admin only) */}
            {!isClosed && !isCustomerPath && (
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
                <div style={{ color: "#065f46", fontSize: "14px", fontWeight: 500, lineHeight: 1.6, whiteSpace: "pre-wrap" }}>
                  {detail.resolution}
                </div>
                <div style={{ fontSize: "12px", color: "#047857", marginTop: "10px", fontWeight: 600 }}>
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
                  color: "#1d1730",
                  fontWeight: 500,
                  whiteSpace: "pre-wrap",
                  background: "var(--sc-bg-surface)",
                  padding: "16px 20px",
                  borderRadius: "8px",
                  border: "1px solid var(--sc-card-border)",
                }}
              >
                {cleanDescription || detail.description}
              </div>

              {/* Supporting Evidence Card */}
              {evidenceFileName && (
                <div
                  style={{
                    marginTop: "16px",
                    background: "#f0fdf4",
                    border: "1px solid #bbf7d0",
                    borderRadius: "8px",
                    padding: "14px 18px",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: "12px",
                    flexWrap: "wrap",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                    <div>
                      <div
                        style={{
                          fontSize: "11px",
                          fontWeight: 700,
                          textTransform: "uppercase",
                          color: "#166534",
                          letterSpacing: "0.5px",
                        }}
                      >
                        Supporting Evidence Attached
                      </div>
                      <div style={{ fontSize: "14px", fontWeight: 600, color: "#14532d", marginTop: "2px" }}>
                        {evidenceFileName}
                      </div>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowEvidenceModal(true)}
                    style={{
                      padding: "8px 16px",
                      background: "#16a34a",
                      color: "#ffffff",
                      border: "none",
                      borderRadius: "6px",
                      fontSize: "12px",
                      fontWeight: 600,
                      cursor: "pointer",
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "6px",
                    }}
                  >
                    View Evidence Document
                  </button>
                </div>
              )}
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
                          <strong style={{ color: "#1d1730" }}>{n.authorName}</strong>{" "}
                          <span style={{ color: "var(--sc-text-muted)" }}>({n.authorRole})</span>
                          {n.noteType && n.noteType !== "Comment" && (
                            <span
                              style={{
                                marginLeft: "8px",
                                background: "#dcd3e8",
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
                      {addingNote ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Post Note"}
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
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#1d1730" }}>
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
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#1d1730" }}>
                Context References
              </h3>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Order ID</span>
                <span className="sc-meta-val">
                  {detail.orderId ? (
                    <button
                      type="button"
                      onClick={() => handleOpenOrderModal(detail.orderId)}
                      style={{
                        background: "none",
                        border: "none",
                        color: "#2563eb",
                        fontWeight: 700,
                        cursor: "pointer",
                        padding: 0,
                        textDecoration: "underline",
                        display: "inline-flex",
                        alignItems: "center",
                        gap: "4px",
                        fontSize: "13px",
                      }}
                      title="Click to view order details popup"
                    >
                      #{detail.orderId} (Quick View)
                    </button>
                  ) : (
                    <span style={{ color: "var(--sc-text-muted)" }}>None</span>
                  )}
                </span>
              </div>
              {detail.orderId && !isCustomerPath && (
                <button
                  type="button"
                  onClick={() => setShowRefundModal(true)}
                  style={{
                    marginTop: "8px",
                    marginBottom: "12px",
                    width: "100%",
                    background: "linear-gradient(135deg, #e8439c 0%, #a855f7 100%)",
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
                        color: "#2563eb",
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
                    <code style={{ color: "#4338ca", fontWeight: 600 }}>{detail.paymentReference}</code>
                  ) : (
                    <span style={{ color: "var(--sc-text-muted)" }}>None</span>
                  )}
                </span>
              </div>
            </div>

            {/* Assignment & Timestamps Card */}
            <div className="sc-meta-card">
              <h3 style={{ fontSize: "14px", fontWeight: 700, margin: "0 0 12px 0", color: "#1d1730" }}>
                Assignment & Timestamps
              </h3>
              <div className="sc-meta-row">
                <span className="sc-meta-label">Assigned Staff</span>
                <span className="sc-meta-val" style={{ color: detail.assignedStaffName ? "#1d4ed8" : "#4b5563", fontWeight: 600 }}>
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
                  style={{ background: "none", border: "none", color: "#7e7694", cursor: "pointer", fontSize: "18px" }}
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
                    {assigning ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Confirm Assignment"}
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
                  style={{ background: "none", border: "none", color: "#7e7694", cursor: "pointer", fontSize: "18px" }}
                >
                  ✕
                </button>
              </div>

              <form onSubmit={handleResolveCase}>
                <div className="sc-modal-body">
                  <div style={{ background: "rgba(245, 158, 11, 0.1)", border: "1px solid rgba(245, 158, 11, 0.3)", padding: "12px", borderRadius: "8px", fontSize: "12px", color: "#e8b82e" }}>
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
                    {resolving ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Confirm & Close Case"}
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

        {/* Evidence Document Preview Modal */}
        {showEvidenceModal && evidenceFileName && (
          <div className="sc-modal-backdrop" onClick={() => setShowEvidenceModal(false)}>
            <div className="sc-modal" style={{ maxWidth: "980px", width: "94vw" }} onClick={(e) => e.stopPropagation()}>
              <div className="sc-modal-header">
                <div>
                  <h3 style={{ margin: 0 }}>Customer Supporting Evidence</h3>
                  <div style={{ fontSize: "12px", color: "#64748b", marginTop: "3px" }}>
                    File: <strong>{evidenceFileName}</strong> • Submitted by {detail.customerName}
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setShowEvidenceModal(false)}
                  style={{ background: "none", border: "none", color: "#7e7694", cursor: "pointer", fontSize: "18px" }}
                >
                  ✕
                </button>
              </div>

              <div className="sc-modal-body" style={{ padding: "16px 20px" }}>
                {/* Viewer Toolbar */}
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: "8px",
                    background: "#f8fafc",
                    border: "1px solid #e2e8f0",
                    borderRadius: "6px",
                    padding: "8px 12px",
                    marginBottom: "12px",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "6px" }}>
                    <button
                      type="button"
                      className={`sc-btn ${evidenceFitMode === "width" ? "sc-btn-primary" : "sc-btn-outline"}`}
                      style={{ fontSize: "12px", padding: "5px 12px" }}
                      onClick={() => {
                        setEvidenceFitMode("width");
                        setEvidenceZoom(100);
                      }}
                    >
                      Fit Width (Scroll to Read)
                    </button>
                    <button
                      type="button"
                      className={`sc-btn ${evidenceFitMode === "contain" ? "sc-btn-primary" : "sc-btn-outline"}`}
                      style={{ fontSize: "12px", padding: "5px 12px" }}
                      onClick={() => {
                        setEvidenceFitMode("contain");
                        setEvidenceZoom(100);
                      }}
                    >
                      Fit Screen
                    </button>
                    <button
                      type="button"
                      className="sc-btn sc-btn-outline"
                      style={{ fontSize: "12px", padding: "5px 10px", minWidth: "30px" }}
                      onClick={() => {
                        setEvidenceFitMode("custom");
                        setEvidenceZoom((z) => Math.max(50, z - 25));
                      }}
                    >
                      -
                    </button>
                    <span style={{ fontSize: "12px", color: "#475569", fontWeight: 600, minWidth: "45px", textAlign: "center" }}>
                      {evidenceFitMode === "width" ? "100%" : `${evidenceZoom}%`}
                    </span>
                    <button
                      type="button"
                      className="sc-btn sc-btn-outline"
                      style={{ fontSize: "12px", padding: "5px 10px", minWidth: "30px" }}
                      onClick={() => {
                        setEvidenceFitMode("custom");
                        setEvidenceZoom((z) => Math.min(250, z + 25));
                      }}
                    >
                      +
                    </button>
                  </div>

                  <div style={{ display: "flex", gap: "8px" }}>
                    <span style={{ fontSize: "11px", fontWeight: 700, background: "#dcfce7", color: "#166534", padding: "4px 8px", borderRadius: "4px", alignSelf: "center" }}>
                      Verified Attachment
                    </span>
                    <button
                      type="button"
                      className="sc-btn sc-btn-outline"
                      style={{ fontSize: "12px", padding: "5px 12px" }}
                      onClick={() => window.open(evidencePreviewUrl || `/uploads/${encodeURIComponent(evidenceFileName)}`, "_blank")}
                    >
                      Open in New Tab
                    </button>
                  </div>
                </div>

                {/* Evidence Image Direct Scrollable Viewer */}
                <div
                  style={{
                    background: "#f1f5f9",
                    borderRadius: "8px",
                    padding: "16px",
                    textAlign: "center",
                    maxHeight: "68vh",
                    overflowY: "auto",
                    overflowX: "auto",
                    border: "1px solid #cbd5e1",
                  }}
                >
                  <img
                    src={evidencePreviewUrl || `/uploads/${encodeURIComponent(evidenceFileName)}`}
                    alt={evidenceFileName}
                    onError={(e) => {
                      e.target.onerror = null;
                      e.target.src = "https://images.unsplash.com/photo-1544717305-2782549b5136?w=600&auto=format&fit=crop&q=60";
                    }}
                    style={{
                      width: evidenceFitMode === "width" ? "100%" : evidenceFitMode === "custom" ? `${evidenceZoom}%` : "auto",
                      maxHeight: evidenceFitMode === "contain" ? "62vh" : "none",
                      maxWidth: evidenceFitMode === "custom" ? "none" : "100%",
                      objectFit: evidenceFitMode === "contain" ? "contain" : "initial",
                      height: "auto",
                      borderRadius: "6px",
                      boxShadow: "0 4px 16px rgba(0,0,0,0.12)",
                      display: "block",
                      margin: "0 auto",
                    }}
                  />
                </div>
              </div>

              <div className="sc-modal-footer">
                <button type="button" className="sc-btn sc-btn-outline" onClick={() => setShowEvidenceModal(false)}>
                  Close Preview
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Quick View Order Popup Modal (Item 3) */}
        {showOrderModal && (
          <div className="sc-modal-backdrop" onClick={() => setShowOrderModal(false)}>
            <div className="sc-modal" style={{ maxWidth: "560px" }} onClick={(e) => e.stopPropagation()}>
              <div className="sc-modal-header">
                <h3>Order #{detail.orderId} Quick View</h3>
                <button
                  type="button"
                  onClick={() => setShowOrderModal(false)}
                  style={{ background: "none", border: "none", color: "#7e7694", cursor: "pointer", fontSize: "18px" }}
                >
                  ✕
                </button>
              </div>
              <div className="sc-modal-body">
                {loadingOrderModal ? (
                  <div style={{ padding: "30px", textAlign: "center", color: "#64748b" }}><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
                ) : orderModalData ? (
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "16px", paddingBottom: "12px", borderBottom: "1px solid #e2e8f0" }}>
                      <div>
                        <h4 style={{ margin: "0 0 4px 0", fontSize: "16px", color: "#0f172a" }}>{orderModalData.eventName || "Concert Order"}</h4>
                        <div style={{ fontSize: "12px", color: "#64748b" }}>Order Date: {formatDate(orderModalData.orderDate)}</div>
                      </div>
                      <span
                        style={{
                          padding: "4px 10px",
                          borderRadius: "12px",
                          fontSize: "12px",
                          fontWeight: 700,
                          background: orderModalData.status === "Paid" ? "#dcfce7" : "#fef3c7",
                          color: orderModalData.status === "Paid" ? "#15803d" : "#b45309",
                        }}
                      >
                        {orderModalData.status}
                      </span>
                    </div>

                    <div style={{ background: "#f8fafc", padding: "12px 14px", borderRadius: "6px", marginBottom: "16px", fontSize: "13px" }}>
                      <div><strong>Customer:</strong> {orderModalData.fullName || detail.customerName || "—"}</div>
                      <div style={{ marginTop: "4px" }}><strong>Email:</strong> {orderModalData.email || detail.customerEmail || "—"}</div>
                      {(orderModalData.phone || detail.customerPhone) && (
                        <div style={{ marginTop: "4px" }}><strong>Phone:</strong> {orderModalData.phone || detail.customerPhone}</div>
                      )}
                      <div style={{ marginTop: "4px" }}><strong>Payment Method:</strong> {orderModalData.paymentMethod || "VNPay"}</div>
                    </div>

                    <h5 style={{ margin: "0 0 8px 0", fontSize: "13px", color: "#334155" }}>
                      Tickets ({orderModalData.orderDetails?.length || 0})
                    </h5>
                    <div style={{ border: "1px solid #e2e8f0", borderRadius: "6px", overflow: "hidden", marginBottom: "16px" }}>
                      {orderModalData.orderDetails?.map((item, idx) => (
                        <div
                          key={idx}
                          style={{
                            display: "flex",
                            justifyContent: "space-between",
                            padding: "10px 14px",
                            borderBottom: idx < orderModalData.orderDetails.length - 1 ? "1px solid #f1f5f9" : "none",
                            fontSize: "13px",
                          }}
                        >
                          <div>
                            <strong>{item.ticketTypeName || "Ticket"}</strong>
                            <div style={{ fontSize: "11px", color: "#64748b" }}>Quantity: {item.quantity}</div>
                          </div>
                          <div style={{ fontWeight: 600 }}>
                            {new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(item.unitPrice * item.quantity)}
                          </div>
                        </div>
                      ))}
                    </div>

                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", paddingTop: "8px", borderTop: "2px solid #e2e8f0", fontSize: "15px", fontWeight: 700 }}>
                      <span>Total Amount:</span>
                      <span style={{ color: "#16a34a" }}>
                        {new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(orderModalData.finalAmount || orderModalData.totalAmount || 0)}
                      </span>
                    </div>
                  </div>
                ) : (
                  <div style={{ color: "#ef4444" }}>Could not load order details.</div>
                )}
              </div>
              <div className="sc-modal-footer">
                <button type="button" className="sc-btn sc-btn-outline" onClick={() => setShowOrderModal(false)}>
                  Close
                </button>
                {detail.orderId && (
                  <Link
                    to={`/my-orders/${detail.orderId}`}
                    target="_blank"
                    className="sc-btn sc-btn-primary"
                    style={{ fontSize: "12px", textDecoration: "none" }}
                  >
                    Open Full Page ↗
                  </Link>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </Shell>
  );
}
