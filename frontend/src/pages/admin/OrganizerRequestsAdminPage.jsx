import { useEffect, useState } from "react";
import organizerRequestApi from "../../api/organizerRequestApi";
import AdminShell from "./AdminShell";
import "../../styles/organizerrequest.css";
import "./AdminPages.css";


const handleDownloadFile = async (fileUrl, fileName) => {
  if (!fileUrl) return;
  try {
    let downloadUrl = fileUrl;
    if (fileUrl.includes("res.cloudinary.com") && fileUrl.includes("/upload/")) {
      downloadUrl = fileUrl.replace("/upload/", "/upload/fl_attachment/");
    }
    const response = await fetch(downloadUrl);
    if (!response.ok) throw new Error("Fetch failed");
    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = blobUrl;
    link.download = fileName || "download";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
  } catch (err) {
    let fallbackUrl = fileUrl;
    if (fileUrl.includes("res.cloudinary.com") && fileUrl.includes("/upload/")) {
      fallbackUrl = fileUrl.replace("/upload/", "/upload/fl_attachment/");
    }
    const link = document.createElement("a");
    link.href = fallbackUrl;
    link.download = fileName || "download";
    link.target = "_blank";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
};

const FILTERS = [
  { label: "All", value: undefined },
  { label: "Pending", value: "Pending" },
  { label: "Approved", value: "Approved" },
  { label: "Rejected", value: "Rejected" },
];

function StatusBadge({ status }) {
  const cls =
    status === "Approved"
      ? "org-badge--approved"
      : status === "Rejected"
      ? "org-badge--rejected"
      : "org-badge--pending";
  return <span className={`org-badge ${cls}`}>{status}</span>;
}

function ReviewModal({ request, onClose, onDone }) {
  const isReadOnly = request.status !== "Pending";
  const [decision, setDecision] = useState("Approved");
  const [reviewNote, setReviewNote] = useState(request.reviewNote || "");
  const [rejectionReason, setRejectionReason] = useState(request.rejectionReason || "");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [previewDoc, setPreviewDoc] = useState(null);

  const handleSubmit = async () => {
    setError("");
    if (decision === "Rejected" && !rejectionReason.trim()) {
      setError("Please provide a rejection reason.");
      return;
    }
    setSubmitting(true);
    try {
      await organizerRequestApi.review(request.requestId, {
        status: decision,
        reviewNote: reviewNote || null,
        rejectionReason: decision === "Rejected" ? rejectionReason : null,
      });

      // Dispatch real-time notification across tabs and windows
      const notifPayload = {
        userId: request.userId,
        title: decision === "Approved" ? "Organizer Account Approved" : "Organizer Request Rejected",
        message:
          decision === "Approved"
            ? "Congratulations! Your organizer registration request has been approved. You now have full access to Organizer features and dashboard."
            : `Your organizer request has been rejected. Reason: ${rejectionReason || "Information did not meet verification criteria."}`,
        category: decision === "Approved" ? "organizer_approved" : "organizer_rejected",
        targetUrl: decision === "Approved" ? "/organizer/dashboard" : "/organizer/request",
      };

      // Store in user-specific key so when that customer logs in, they see it immediately
      try {
        const userNotifsKey = `cs_user_notifs_${request.userId}`;
        const existingNotifs = JSON.parse(localStorage.getItem(userNotifsKey) || "[]");
        existingNotifs.unshift({
          notificationId: Date.now(),
          ...notifPayload,
          isRead: false,
          createdAt: new Date().toISOString(),
        });
        localStorage.setItem(userNotifsKey, JSON.stringify(existingNotifs));
      } catch (_) {}

      try {
        const bc = new BroadcastChannel("concertshield_notifications");
        bc.postMessage(notifPayload);
        bc.close();
      } catch (_) {}

      try {
        localStorage.setItem("cs_latest_notification", JSON.stringify(notifPayload));
      } catch (_) {}

      window.dispatchEvent(new CustomEvent("notification-updated"));
      window.dispatchEvent(new CustomEvent("show-notification-toast", { detail: notifPayload }));

      onDone();
    } catch (err) {
      setError(err.response?.data?.message || "Failed to submit review.");
    } finally {
      setSubmitting(false);
    }
  };

  const docs = request.documents || [];

  return (
    <>
      <div className="org-modal-backdrop" onClick={onClose}>
        <div
          className="org-modal"
          style={{ maxWidth: "580px", maxHeight: "90vh", overflowY: "auto" }}
          onClick={(e) => e.stopPropagation()}
        >
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "flex-start",
              marginBottom: "8px",
            }}
          >
            <div>
              <h3 className="org-modal__title" style={{ margin: 0 }}>
                {isReadOnly ? "Request details" : "Review request"}
              </h3>
              <p className="org-modal__subtitle" style={{ margin: "4px 0 0" }}>
                {request.userFullName} ({request.userEmail})
              </p>
            </div>
            {isReadOnly && <StatusBadge status={request.status} />}
          </div>

          {error && <div className="org-alert org-alert--error">{error}</div>}

          {/* Legal Information */}
          <div
            style={{
              background: "var(--org-field-bg)",
              border: "0.5px solid var(--org-line)",
              borderRadius: "8px",
              padding: "12px",
              marginBottom: "16px",
              fontSize: "13px",
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "8px",
            }}
          >
            <div>
              <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                COMPANY
              </span>
              <strong>{request.companyName || "—"}</strong>
            </div>

            <div>
              <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                TAX CODE
              </span>
              <strong style={{ color: "var(--org-primary)" }}>{request.taxCode || "—"}</strong>
            </div>

            <div style={{ gridColumn: "1 / -1" }}>
              <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                LEGAL REPRESENTATIVE
              </span>
              <strong>{request.representativeName || "—"}</strong>
            </div>

            {request.phoneNumber && (
              <div>
                <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                  PHONE
                </span>
                <span>{request.phoneNumber}</span>
              </div>
            )}

            {request.website && (
              <div>
                <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                  WEBSITE
                </span>
                <a href={request.website} target="_blank" rel="noreferrer" style={{ color: "#60a5fa" }}>
                  {request.website} ↗
                </a>
              </div>
            )}

            <div style={{ gridColumn: "1 / -1" }}>
              <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                REASON
              </span>
              <span style={{ color: "var(--org-text-muted)" }}>{request.reason}</span>
            </div>

            {request.experience && (
              <div style={{ gridColumn: "1 / -1" }}>
                <span style={{ color: "var(--org-text-faint)", fontSize: "11px", display: "block" }}>
                  EXPERIENCE
                </span>
                <span style={{ color: "var(--org-text-muted)" }}>{request.experience}</span>
              </div>
            )}
          </div>

          {/* Attached Files & Documents */}
          <div style={{ marginBottom: "16px" }}>
            <label
              style={{
                fontSize: "12px",
                color: "var(--org-text-muted)",
                fontWeight: 600,
                display: "block",
                marginBottom: "8px",
              }}
            >
              ATTACHED DOCUMENTS ({docs.length})
            </label>

            {docs.length === 0 ? (
              <p style={{ fontSize: "12px", color: "var(--org-text-faint)", fontStyle: "italic" }}>
                No documents attached.
              </p>
            ) : (
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "repeat(auto-fill, minmax(150px, 1fr))",
                  gap: "10px",
                }}
              >
                {docs.map((doc) => (
                  <div
                    key={doc.documentId}
                    style={{
                      background: "var(--org-field-bg)",
                      border: "0.5px solid var(--org-line)",
                      borderRadius: "8px",
                      padding: "8px",
                      fontSize: "12px",
                      display: "flex",
                      flexDirection: "column",
                      gap: "6px",
                    }}
                  >
                    <div style={{ fontSize: "10px", color: "var(--org-primary)", fontWeight: 700 }}>
                      {doc.documentType}
                    </div>

                    {doc.isImage ? (
                      <div
                        style={{
                          width: "100%",
                          height: "80px",
                          borderRadius: "4px",
                          overflow: "hidden",
                          cursor: "pointer",
                          background: "#000",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                        }}
                        onClick={() =>
                          setPreviewDoc({
                            url: doc.fileUrl,
                            isImage: true,
                            name: doc.fileName,
                            type: doc.documentType,
                          })
                        }
                        title="Click to enlarge"
                      >
                        <img
                          src={doc.previewUrl || doc.fileUrl}
                          alt={doc.fileName}
                          style={{ width: "100%", height: "100%", objectFit: "cover" }}
                        />
                      </div>
                    ) : (
                      <button
                        type="button"
                        onClick={() =>
                          setPreviewDoc({
                            url: doc.fileUrl,
                            previewUrl: doc.previewUrl,
                            isImage: false,
                            name: doc.fileName,
                            type: doc.documentType,
                          })
                        }
                        style={{
                          width: "100%",
                          height: "80px",
                          borderRadius: "4px",
                          background: "rgba(239, 68, 68, 0.15)",
                          border: "none",
                          cursor: "pointer",
                          display: "flex",
                          flexDirection: "column",
                          alignItems: "center",
                          justifyContent: "center",
                          color: "#ef4444",
                          fontWeight: 700,
                        }}
                        title="Click to preview PDF"
                      >
                        <span>PDF</span>
                        <span style={{ fontSize: "10px", fontWeight: 400 }}>Preview file</span>
                      </button>
                    )}

                    <div
                      style={{
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        color: "var(--org-text)",
                      }}
                      title={doc.fileName}
                    >
                      {doc.fileName}
                    </div>

                    <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                      <button
                        type="button"
                        onClick={() =>
                          setPreviewDoc({
                            url: doc.fileUrl,
                            previewUrl: doc.previewUrl,
                            isImage: doc.isImage,
                            name: doc.fileName,
                            type: doc.documentType,
                          })
                        }
                        style={{
                          background: "none",
                          border: "none",
                          padding: 0,
                          fontSize: "11px",
                          color: "var(--org-primary)",
                          cursor: "pointer",
                          textAlign: "left",
                          textDecoration: "underline",
                        }}
                      >
                        Preview
                      </button>
                      <span style={{ fontSize: "10px", color: "var(--org-text-faint)" }}>•</span>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          handleDownloadFile(doc.fileUrl, doc.fileName);
                        }}
                        style={{
                          background: "none",
                          border: "none",
                          padding: 0,
                          fontSize: "11px",
                          color: "#60a5fa",
                          cursor: "pointer",
                          textAlign: "left",
                          textDecoration: "underline",
                        }}
                      >
                        Download ⬇️
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Read-Only Info for already reviewed request */}
          {isReadOnly && (
            <div style={{ marginBottom: "16px" }}>
              {request.reviewNote && (
                <div
                  style={{
                    background: "rgba(255, 255, 255, 0.03)",
                    border: "0.5px solid var(--org-line)",
                    borderRadius: "6px",
                    padding: "10px 12px",
                    marginBottom: "10px",
                    fontSize: "13px",
                  }}
                >
                  <span
                    style={{
                      color: "var(--org-text-faint)",
                      fontSize: "11px",
                      display: "block",
                      marginBottom: "2px",
                    }}
                  >
                    REVIEW NOTE
                  </span>
                  <span style={{ color: "var(--org-text)" }}>{request.reviewNote}</span>
                </div>
              )}

              {request.status === "Rejected" && request.rejectionReason && (
                <div
                  style={{
                    background: "rgba(239, 68, 68, 0.08)",
                    border: "0.5px solid rgba(239, 68, 68, 0.3)",
                    borderRadius: "6px",
                    padding: "10px 12px",
                    marginBottom: "10px",
                    fontSize: "13px",
                  }}
                >
                  <span
                    style={{
                      color: "#ef4444",
                      fontSize: "11px",
                      display: "block",
                      fontWeight: 700,
                      marginBottom: "2px",
                    }}
                  >
                    REJECTION REASON
                  </span>
                  <span style={{ color: "#fca5a5" }}>{request.rejectionReason}</span>
                </div>
              )}

              {request.reviewedAt && (
                <div style={{ fontSize: "12px", color: "var(--org-text-faint)" }}>
                  Reviewed on: {new Date(request.reviewedAt).toLocaleString()}
                </div>
              )}
            </div>
          )}

          {/* Action controls for Pending request */}
          {!isReadOnly && (
            <>
              <div className="org-status-toggle">
                <button
                  type="button"
                  className={`is-approve ${decision === "Approved" ? "is-active" : ""}`}
                  onClick={() => setDecision("Approved")}
                >
                  Approve
                </button>
                <button
                  type="button"
                  className={`is-reject ${decision === "Rejected" ? "is-active" : ""}`}
                  onClick={() => setDecision("Rejected")}
                >
                  Reject
                </button>
              </div>

              <div className="org-field">
                <label>Note (optional)</label>
                <textarea
                  rows={2}
                  value={reviewNote}
                  onChange={(e) => setReviewNote(e.target.value)}
                  placeholder="Internal note..."
                />
              </div>

              {decision === "Rejected" && (
                <div className="org-field">
                  <label>Rejection reason *</label>
                  <textarea
                    rows={2}
                    value={rejectionReason}
                    onChange={(e) => setRejectionReason(e.target.value)}
                    placeholder="Let the user know why..."
                  />
                </div>
              )}
            </>
          )}

          <div className="org-modal__actions">
            {isReadOnly ? (
              <button className="org-button" onClick={onClose}>
                Close
              </button>
            ) : (
              <>
                <button
                  className="org-button org-button--ghost"
                  onClick={onClose}
                  disabled={submitting}
                >
                  Cancel
                </button>
                <button className="org-button" onClick={handleSubmit} disabled={submitting}>
                  {submitting ? "Saving..." : "Confirm"}
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Lightbox / Modal for previewing clicked Document inside page */}
      {previewDoc && (
        <div
          className="org-modal-backdrop"
          style={{ zIndex: 70 }}
          onClick={() => setPreviewDoc(null)}
        >
          <div
            style={{
              position: "relative",
              maxWidth: "92vw",
              maxHeight: "92vh",
              width: previewDoc.isImage ? "auto" : "900px",
              background: "var(--org-card)",
              border: "1px solid var(--org-line)",
              borderRadius: "12px",
              padding: "20px",
              display: "flex",
              flexDirection: "column",
              gap: "14px",
              boxShadow: "0 20px 40px rgba(0,0,0,0.6)",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                borderBottom: "0.5px solid var(--org-line)",
                paddingBottom: "12px",
              }}
            >
              <div>
                <h3 style={{ margin: 0, fontSize: "16px", color: "var(--org-text)" }}>
                  {previewDoc.type || "Document Preview"}
                </h3>
                <span style={{ fontSize: "12px", color: "var(--org-text-faint)" }}>
                  {previewDoc.name}
                </span>
              </div>
              <div style={{ display: "flex", gap: "8px" }}>
                <button
                  type="button"
                  className="org-button org-button--ghost"
                  style={{ height: "30px", padding: "0 10px", fontSize: "12px", display: "inline-flex", alignItems: "center", gap: "4px" }}
                  onClick={() => handleDownloadFile(previewDoc.url, previewDoc.name)}
                >
                  ⬇️ Download
                </button>
                <button
                  type="button"
                  className="org-button org-button--ghost"
                  style={{ height: "30px", padding: "0 10px", fontSize: "12px" }}
                  onClick={() => setPreviewDoc(null)}
                >
                  ✕ Close
                </button>
              </div>
            </div>

            <div
              style={{
                flex: 1,
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                overflow: "hidden",
              }}
            >
              {previewDoc.isImage || (previewDoc.previewUrl && previewDoc.previewUrl.includes(".jpg")) ? (
                <div style={{ textAlign: "center", width: "100%", maxHeight: "75vh", overflow: "auto" }}>
                  <img
                    src={previewDoc.previewUrl || previewDoc.url}
                    alt={previewDoc.name}
                    style={{
                      maxWidth: "100%",
                      maxHeight: "75vh",
                      objectFit: "contain",
                      borderRadius: "8px",
                    }}
                  />
                </div>
              ) : (
                <div style={{ width: "100%", display: "flex", flexDirection: "column", gap: "8px" }}>
                  <iframe
                    src={previewDoc.url}
                    title={previewDoc.name}
                    style={{
                      width: "100%",
                      height: "70vh",
                      border: "none",
                      borderRadius: "8px",
                      background: "#fff",
                    }}
                  />
                  <div
                    style={{
                      padding: "8px 12px",
                      background: "rgba(239, 68, 68, 0.1)",
                      border: "0.5px solid rgba(239, 68, 68, 0.3)",
                      borderRadius: "6px",
                      fontSize: "12px",
                      color: "#fca5a5",
                      textAlign: "left",
                      lineHeight: "1.5",
                    }}
                  >
                    ⚠️ <strong>Lưu ý:</strong> Nếu trình duyệt không tải được file PDF từ Cloudinary (lỗi <em>deny or ACL failure 401</em>), vui lòng vào <strong>Cloudinary Console &gt; Settings (bánh răng) &gt; Security &gt; Bật "PDF and ZIP files delivery"</strong> rồi bấm Save.
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

export default function OrganizerRequestsAdminPage() {
  const [requests, setRequests] = useState([]);
  const [filter, setFilter] = useState(undefined);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reviewing, setReviewing] = useState(null);

  const fetchRequests = async (status) => {
    setLoading(true);
    setError("");
    try {
      const res = await organizerRequestApi.getAll(status);
      setRequests(res.data);
    } catch (err) {
      setError(err.response?.data?.message || "Failed to load requests.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRequests(filter);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filter]);

  return (
    <AdminShell title="Organizer Requests">
      <div className="org-page admin-org-page">
        <div className="org-container org-container--wide">
          <div className="org-card" style={{ padding: "28px" }}>
            <h1 className="org-title">Organizer requests</h1>
            <p className="org-subtitle">Review and manage user requests to become organizers.</p>

            <div className="org-filter-bar">
              {FILTERS.map((f) => (
                <button
                  key={f.label}
                  className={`org-filter-btn ${filter === f.value ? "is-active" : ""}`}
                  onClick={() => setFilter(f.value)}
                >
                  {f.label}
                </button>
              ))}
            </div>

            {error && <div className="org-alert org-alert--error">{error}</div>}

            {loading ? (
              <p className="org-empty">Loading...</p>
            ) : requests.length === 0 ? (
              <p className="org-empty">No requests found.</p>
            ) : (
              <div className="org-table-wrapper">
                <table className="org-table">
                  <thead>
                    <tr>
                      <th style={{ width: "21%" }}>User</th>
                      <th style={{ width: "20%" }}>Company</th>
                      <th style={{ width: "24%" }}>Reason</th>
                      <th style={{ width: "12%", textAlign: "center" }}>Attachments</th>
                      <th style={{ width: "10%", textAlign: "center" }}>Status</th>
                      <th style={{ width: "13%", textAlign: "center" }}>Submitted</th>
                      <th style={{ width: "80px", textAlign: "right" }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {requests.map((r) => (
                      <tr key={r.requestId}>
                        <td style={{ whiteSpace: "normal", wordBreak: "break-word" }}>
                          <span style={{ fontWeight: 600 }}>{r.userFullName}</span>
                          <br />
                          <span style={{ color: "var(--org-text-faint)", fontSize: "12px" }}>
                            {r.userEmail}
                          </span>
                        </td>
                        <td style={{ whiteSpace: "normal", wordBreak: "break-word" }}>
                          {r.companyName || "—"}
                        </td>
                        <td
                          className="org-table__reason"
                          style={{ whiteSpace: "normal", wordBreak: "break-word" }}
                        >
                          {r.reason}
                        </td>
                        <td style={{ textAlign: "center" }}>
                          <span
                            style={{
                              padding: "3px 8px",
                              borderRadius: "4px",
                              fontSize: "12px",
                              background: "rgba(255, 111, 181, 0.12)",
                              color: "var(--org-primary)",
                              display: "inline-block",
                              whiteSpace: "nowrap",
                            }}
                          >
                            📎 {r.documentsCount || r.documents?.length || 0} files
                          </span>
                        </td>
                        <td style={{ textAlign: "center" }}>
                          <StatusBadge status={r.status} />
                        </td>
                        <td
                          style={{
                            textAlign: "center",
                            fontSize: "12.5px",
                            color: "var(--org-text-muted)",
                            whiteSpace: "nowrap",
                          }}
                        >
                          {new Date(r.createdAt).toLocaleDateString()}
                        </td>
                        <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
                          {r.status === "Pending" ? (
                            <button
                              className="org-button org-button--ghost"
                              style={{ height: "32px", padding: "0 12px", fontSize: "12px" }}
                              onClick={() => setReviewing(r)}
                            >
                              Review
                            </button>
                          ) : (
                            <button
                              className="org-button org-button--ghost"
                              style={{ height: "32px", padding: "0 12px", fontSize: "12px" }}
                              onClick={() => setReviewing(r)}
                            >
                              View
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>

        {reviewing && (
          <ReviewModal
            request={reviewing}
            onClose={() => setReviewing(null)}
            onDone={() => {
              setReviewing(null);
              fetchRequests(filter);
            }}
          />
        )}
      </div>
    </AdminShell>
  );
}
