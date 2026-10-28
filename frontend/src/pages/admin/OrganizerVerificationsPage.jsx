import React, { useEffect, useState } from "react";
import organizerVerificationApi from "../../api/organizerVerificationApi";
import AdminShell from "./AdminShell";
import "./OrganizerVerificationsPage.css";

export default function OrganizerVerificationsPage() {
  const [requestsList, setRequestsList] = useState([]);
  const [statusFilter, setStatusFilter] = useState("Pending");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Review & Inspect Modal State
  const [selectedRequest, setSelectedRequest] = useState(null);
  const [showDetailModal, setShowDetailModal] = useState(false);
  const [showRejectModal, setShowRejectModal] = useState(false);
  const [rejectionReason, setRejectionReason] = useState("");
  const [reviewNote, setReviewNote] = useState("");
  const [submittingDecision, setSubmittingDecision] = useState(false);

  // File Preview Modal State
  const [previewDoc, setPreviewDoc] = useState(null);

  // Dedicated Pop-up Modals (replacing all window.alert & window.confirm)
  const [confirmDialog, setConfirmDialog] = useState(null); // { type, title, message, onConfirm }
  const [noticeDialog, setNoticeDialog] = useState(null); // { type: "SUCCESS" | "ERROR" | "WARNING", title, message }

  useEffect(() => {
    fetchRequests(statusFilter);
  }, [statusFilter]);

  const fetchRequests = async (status) => {
    try {
      setLoading(true);
      setError("");
      const res = await organizerVerificationApi.getPendingRequests(status);
      if (res.data?.data) {
        setRequestsList(res.data.data);
      }
    } catch (err) {
      console.error("Failed to fetch verification requests:", err);
      setError("Failed to load organizer verification requests.");
    } finally {
      setLoading(false);
    }
  };

  const handleInspectRequest = (req) => {
    setSelectedRequest(req);
    setReviewNote(req.reviewNote || "");
    setRejectionReason(req.rejectionReason || "");
    setShowDetailModal(true);
  };

  const executeApprove = async () => {
    if (!selectedRequest) return;
    try {
      setSubmittingDecision(true);
      const res = await organizerVerificationApi.approveRequest(selectedRequest.requestId, {
        reviewNote: reviewNote.trim(),
      });

      if (res.data?.success) {
        setShowDetailModal(false);
        setNoticeDialog({
          type: "SUCCESS",
          title: "Organizer Approved Successfully",
          message: res.data.message || "Organizer application has been approved and in-app notification dispatched successfully!",
        });
        fetchRequests(statusFilter);
      }
    } catch (err) {
      setNoticeDialog({
        type: "ERROR",
        title: "Approval Failed",
        message: err.response?.data?.message || err.message || "Failed to approve organizer application.",
      });
    } finally {
      setSubmittingDecision(false);
    }
  };

  const handleApproveSubmit = () => {
    if (!selectedRequest) return;
    setConfirmDialog({
      type: "SUCCESS",
      title: "Confirm Organizer Approval",
      message: `Are you sure you want to APPROVE organizer verification for "${selectedRequest.companyName || selectedRequest.userEmail}"? This will grant official Event Organizer privileges and automatically dispatch an in-app & email notification to the applicant.`,
      onConfirm: executeApprove,
    });
  };

  const handleRejectSubmit = async (e) => {
    e.preventDefault();
    if (!selectedRequest) return;
    if (!rejectionReason.trim()) {
      setNoticeDialog({
        type: "WARNING",
        title: "Rejection Reason Required",
        message: "Please specify the detailed rejection reason so the organizer can fix and re-submit.",
      });
      return;
    }

    try {
      setSubmittingDecision(true);
      const res = await organizerVerificationApi.rejectRequest(selectedRequest.requestId, {
        rejectionReason: rejectionReason.trim(),
        reviewNote: reviewNote.trim(),
      });

      if (res.data?.success) {
        setShowRejectModal(false);
        setShowDetailModal(false);
        setNoticeDialog({
          type: "SUCCESS",
          title: "Application Rejected & Notified",
          message: res.data.message || "The application has been rejected and an in-app notification with the reason has been dispatched to the organizer.",
        });
        fetchRequests(statusFilter);
      }
    } catch (err) {
      setNoticeDialog({
        type: "ERROR",
        title: "Rejection Failed",
        message: err.response?.data?.message || err.message || "Failed to process rejection.",
      });
    } finally {
      setSubmittingDecision(false);
    }
  };

  const pendingCount = requestsList.filter((r) => r.status === "Pending").length;

  return (
    <AdminShell title="Organizer Verification Documents">
      <div className="verifications-admin-container">
      {/* Header */}
      <header className="verifications-header">
        <div>
          <span className="badge-admin">ORGANIZER COMPLIANCE</span>
          <h1>Review Organizer Verification Documents</h1>
          <p className="subtitle">
            Inspect legal business documents (Cloudinary storage), verify business credentials, and approve/reject organizer accounts.
          </p>
        </div>
      </header>

      {/* Filter Tabs & Summary */}
      <div className="filter-tabs-row">
        <div className="tabs-group">
          {[
            { id: "Pending", label: "⏳ Pending Verification", count: pendingCount },
            { id: "Approved", label: "✅ Approved Organizers" },
            { id: "Rejected", label: "❌ Rejected Applications" },
            { id: "ALL", label: "📋 All Requests" },
          ].map((tab) => (
            <button
              key={tab.id}
              className={`tab-btn ${statusFilter === tab.id ? "active" : ""}`}
              onClick={() => setStatusFilter(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <button className="btn-refresh" onClick={() => fetchRequests(statusFilter)}>
          🔄 Refresh List
        </button>
      </div>

      {/* Requests Table (UC_11.4) */}
      <div className="requests-section">
        {loading ? (
          <div className="loading-box">
            <div className="spinner"></div>
            <p>Loading verification applications...</p>
          </div>
        ) : error ? (
          <div className="error-banner">⚠️ {error}</div>
        ) : requestsList.length === 0 ? (
          <div className="empty-box">No organizer verification requests found matching filter "{statusFilter}".</div>
        ) : (
          <div className="table-wrapper">
            <table className="requests-table">
              <thead>
                <tr>
                  <th>Request ID</th>
                  <th>Company / Applicant Name</th>
                  <th>Tax Code</th>
                  <th>Representative & Contact</th>
                  <th>Cloudinary Docs</th>
                  <th>Submitted Date</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {requestsList.map((req) => (
                  <tr key={req.requestId}>
                    <td>
                      <span className="id-tag">#{req.requestId}</span>
                    </td>
                    <td>
                      <div className="company-info-cell">
                        <strong>{req.companyName || "N/A Business Name"}</strong>
                        <span className="email-sub">{req.userEmail || `User #${req.userId}`}</span>
                      </div>
                    </td>
                    <td>
                      <code>{req.taxCode || "N/A"}</code>
                    </td>
                    <td>
                      <div className="contact-cell">
                        <span>👤 {req.representativeName || req.userFullName || "N/A"}</span>
                        <span className="phone-sub">📞 {req.phoneNumber || "N/A"}</span>
                      </div>
                    </td>
                    <td>
                      <span className="doc-count-badge">
                        📎 {req.documents?.length || 0} File(s)
                      </span>
                    </td>
                    <td>
                      <span className="date-text">{new Date(req.createdAt).toLocaleDateString()}</span>
                    </td>
                    <td>
                      <span className={`status-pill ${req.status.toLowerCase()}`}>
                        {req.status === "Pending" ? "PENDING" : req.status === "Approved" ? "APPROVED" : "REJECTED"}
                      </span>
                    </td>
                    <td>
                      <button className="btn-inspect" onClick={() => handleInspectRequest(req)}>
                        🔍 Inspect Documents
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* MODAL 1: Request Inspection & Review Details (UC_11.4 & UC_11.5) */}
      {showDetailModal && selectedRequest && (
        <div className="modal-overlay">
          <div className="modal-card extra-wide">
            <div className="modal-header">
              <h2>🔍 Organizer Legal Document Inspection (Request #{selectedRequest.requestId})</h2>
              <button className="btn-close" onClick={() => setShowDetailModal(false)}>✕</button>
            </div>

            <div className="modal-body">
              {/* Applicant Enterprise Overview */}
              <div className="applicant-overview-card">
                <h3>🏢 Enterprise Legal Information</h3>
                <div className="overview-grid">
                  <div>
                    <span className="label">Company Name:</span>
                    <strong>{selectedRequest.companyName || "N/A"}</strong>
                  </div>
                  <div>
                    <span className="label">Tax Code (Enterprise Tax ID):</span>
                    <code>{selectedRequest.taxCode || "N/A"}</code>
                  </div>
                  <div>
                    <span className="label">Legal Representative:</span>
                    <strong>{selectedRequest.representativeName || "N/A"}</strong>
                  </div>
                  <div>
                    <span className="label">Phone Contact:</span>
                    <span>{selectedRequest.phoneNumber || "N/A"}</span>
                  </div>
                  <div>
                    <span className="label">Applicant Account:</span>
                    <span>{selectedRequest.userEmail} ({selectedRequest.userFullName})</span>
                  </div>
                  <div>
                    <span className="label">Website / Portfolio Link:</span>
                    <a href={selectedRequest.website} target="_blank" rel="noreferrer">
                      {selectedRequest.website || "N/A"}
                    </a>
                  </div>
                </div>

                {selectedRequest.experience && (
                  <div className="exp-block">
                    <span className="label">Event Management Capability & Track Record:</span>
                    <p>{selectedRequest.experience}</p>
                  </div>
                )}
              </div>

              {/* Cloudinary Documents Inspection (UC_11.3 / UC_11.4) */}
              <div className="documents-inspection-card">
                <h3>📂 Uploaded Legal Verification Documents (Cloudinary Cloud Storage)</h3>
                {selectedRequest.documents?.length === 0 ? (
                  <div className="no-docs-warning">
                    ⚠️ No verification documents uploaded for this request yet.
                  </div>
                ) : (
                  <div className="docs-grid">
                    {selectedRequest.documents.map((doc, idx) => {
                      const fileUrl = doc.fileUrl || doc.url || doc.previewUrl || "";
                      const docType = doc.documentType || doc.type || "BUSINESS_LICENSE";
                      const fileName = doc.fileName || doc.title || "Attached Legal Document";
                      const docId = doc.documentId || doc.id || idx;
                      const isImg = doc.isImage ?? (fileUrl ? /\.(jpg|jpeg|png|webp|gif|svg)(\?.*)?$/i.test(fileUrl) : false);

                      return (
                        <div key={docId} className="doc-card">
                          <div className="doc-card-top">
                            <span className="doc-type-tag">
                              {docType === "BUSINESS_LICENSE"
                                ? "📄 Business Registration License"
                                : docType === "REPRESENTATIVE_ID"
                                ? "🪪 Representative ID / Passport"
                                : "📁 Event Portfolio / Track Record"}
                            </span>
                          </div>

                          <div className="doc-preview-area">
                            {isImg && fileUrl ? (
                              <img
                                src={fileUrl}
                                alt={docType}
                                className="doc-thumbnail"
                                onClick={() => setPreviewDoc({ ...doc, fileUrl, documentType: docType })}
                              />
                            ) : (
                              <div className="doc-file-icon">
                                📄 <span>PDF / Document File</span>
                              </div>
                            )}
                          </div>

                          <div className="doc-meta">
                            <span className="file-name">{fileName}</span>
                            {fileUrl && (
                              <a
                                href={fileUrl}
                                target="_blank"
                                rel="noreferrer"
                                className="btn-view-cloud"
                              >
                                🔗 View Full File on Cloudinary ↗
                              </a>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>

              {/* Status Note & Rejection Reason Display if already reviewed */}
              {selectedRequest.status === "Rejected" && selectedRequest.rejectionReason && (
                <div className="rejection-reason-banner">
                  <strong>❌ Previous Rejection Reason:</strong>
                  <p>{selectedRequest.rejectionReason}</p>
                </div>
              )}

              {/* Reviewer Note Input */}
              <div className="form-group review-note-box">
                <label>Admin Internal Audit Notes (Optional)</label>
                <input
                  type="text"
                  value={reviewNote}
                  onChange={(e) => setReviewNote(e.target.value)}
                  placeholder="Notes regarding Seal/Signature verification, business registry check..."
                  disabled={selectedRequest.status !== "Pending"}
                />
              </div>
            </div>

            {/* Decision Footer Buttons (UC_11.5) */}
            <div className="modal-footer">
              <button className="btn-cancel" onClick={() => setShowDetailModal(false)}>
                Close
              </button>

              {selectedRequest.status === "Pending" && (
                <>
                  <button
                    className="btn-reject"
                    disabled={submittingDecision}
                    onClick={() => {
                      setRejectionReason("");
                      setShowRejectModal(true);
                    }}
                  >
                    ❌ Reject Verification
                  </button>

                  <button
                    className="btn-approve"
                    disabled={submittingDecision}
                    onClick={handleApproveSubmit}
                  >
                    {submittingDecision ? "Approving..." : "✅ Approve & Grant Organizer Role"}
                  </button>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: Mandatory Rejection Reason Input (UC_11.5) */}
      {showRejectModal && selectedRequest && (
        <div className="modal-overlay">
          <div className="modal-card">
            <div className="modal-header">
              <h2>❌ Reject Organizer Verification Request #{selectedRequest.requestId}</h2>
              <button className="btn-close" onClick={() => setShowRejectModal(false)}>✕</button>
            </div>

            <form onSubmit={handleRejectSubmit}>
              <div className="modal-body">
                <p className="reject-warning-hint">
                  Mandatory Requirement: Please provide a clear explanation for the rejection. This reason will be emailed directly to the organizer so they can upload revised documents.
                </p>

                <div className="form-group">
                  <label>Detailed Rejection Reason *</label>
                  <textarea
                    required
                    rows={4}
                    value={rejectionReason}
                    onChange={(e) => setRejectionReason(e.target.value)}
                    placeholder="e.g. Business license image is blurry, tax code does not match representative ID, expired passport..."
                  />
                </div>
              </div>

              <div className="modal-footer">
                <button type="button" className="btn-cancel" onClick={() => setShowRejectModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-confirm-reject" disabled={submittingDecision || !rejectionReason.trim()}>
                  {submittingDecision ? "Sending Notification..." : "Confirm Rejection & Send Email"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Cloudinary Full Image Lightbox Preview */}
      {previewDoc && (
        <div className="modal-overlay" onClick={() => setPreviewDoc(null)}>
          <div className="modal-card lightbox" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>📷 Document Preview: {previewDoc.documentType || previewDoc.type || "Document"}</h2>
              <button className="btn-close" onClick={() => setPreviewDoc(null)}>✕</button>
            </div>
            <div className="lightbox-body">
              <img src={previewDoc.fileUrl || previewDoc.url || previewDoc.previewUrl} alt="Document Preview" className="lightbox-img" />
            </div>
          </div>
        </div>
      )}

      {/* Universal Confirmation Modal Dialog */}
      {confirmDialog && (
        <div className="modal-overlay">
          <div className="modal-card dialog-popup">
            <div className={`modal-header ${confirmDialog.type === "SUCCESS" ? "header-success" : "header-warning"}`}>
              <h2>{confirmDialog.type === "SUCCESS" ? "✅" : "⚠️"} {confirmDialog.title}</h2>
              <button className="btn-close" onClick={() => setConfirmDialog(null)}>✕</button>
            </div>
            <div className="modal-body">
              <p className="dialog-text">{confirmDialog.message}</p>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-cancel" onClick={() => setConfirmDialog(null)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-confirm-dialog"
                onClick={() => {
                  const cb = confirmDialog.onConfirm;
                  setConfirmDialog(null);
                  if (cb) cb();
                }}
              >
                Confirm & Proceed
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Universal Notice / Alert Modal Dialog */}
      {noticeDialog && (
        <div className="modal-overlay">
          <div className="modal-card dialog-popup">
            <div className={`modal-header ${noticeDialog.type === "ERROR" ? "header-danger" : noticeDialog.type === "SUCCESS" ? "header-success" : "header-warning"}`}>
              <h2>
                {noticeDialog.type === "ERROR" ? "❌" : noticeDialog.type === "SUCCESS" ? "✅" : "⚠️"} {noticeDialog.title}
              </h2>
              <button className="btn-close" onClick={() => setNoticeDialog(null)}>✕</button>
            </div>
            <div className="modal-body">
              <p className="dialog-text">{noticeDialog.message}</p>
            </div>
            <div className="modal-footer">
              <button
                type="button"
                className="btn-confirm-dialog"
                onClick={() => setNoticeDialog(null)}
              >
                Acknowledge & Close
              </button>
            </div>
          </div>
        </div>
      )}
      </div>
    </AdminShell>
  );
}
