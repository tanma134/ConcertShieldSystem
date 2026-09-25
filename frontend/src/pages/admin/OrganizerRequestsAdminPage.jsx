import { useEffect, useState } from "react";
import organizerRequestApi from "../../api/organizerRequestApi";
import AdminShell from "./AdminShell";
import "../../styles/organizerrequest.css";
import "./AdminPages.css";

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
  const [decision, setDecision] = useState("Approved");
  const [reviewNote, setReviewNote] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

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
      onDone();
    } catch (err) {
      setError(err.response?.data?.message || "Failed to submit review.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="org-modal-backdrop" onClick={onClose}>
      <div className="org-modal" onClick={(e) => e.stopPropagation()}>
        <h3 className="org-modal__title">Review request</h3>
        <p className="org-modal__subtitle">
          {request.userFullName} ({request.userEmail})
        </p>

        {error && <div className="org-alert org-alert--error">{error}</div>}

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

        <div className="org-modal__actions">
          <button className="org-button org-button--ghost" onClick={onClose} disabled={submitting}>
            Cancel
          </button>
          <button className="org-button" onClick={handleSubmit} disabled={submitting}>
            {submitting ? "Saving..." : "Confirm"}
          </button>
        </div>
      </div>
    </div>
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
        <div className="org-card">
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
                    <th>User</th>
                    <th>Company</th>
                    <th>Reason</th>
                    <th>Status</th>
                    <th>Submitted</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {requests.map((r) => (
                    <tr key={r.requestId}>
                      <td>
                        {r.userFullName}
                        <br />
                        <span style={{ color: "var(--org-text-faint)", fontSize: "12px" }}>
                          {r.userEmail}
                        </span>
                      </td>
                      <td>{r.companyName || "—"}</td>
                      <td className="org-table__reason">{r.reason}</td>
                      <td>
                        <StatusBadge status={r.status} />
                      </td>
                      <td>{new Date(r.createdAt).toLocaleDateString()}</td>
                      <td>
                        {r.status === "Pending" && (
                          <button
                            className="org-button org-button--ghost"
                            style={{ height: "34px", padding: "0 14px", fontSize: "13px" }}
                            onClick={() => setReviewing(r)}
                          >
                            Review
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
