import React, { useState, useEffect } from "react";
import checkinApi from "../api/checkinApi";
import "./OfflineConflictModal.css";

export default function OfflineConflictModal({
  isOpen,
  onClose,
  eventId = 1,
  onConflictResolved,
}) {
  const [conflicts, setConflicts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [filterStatus, setFilterStatus] = useState("ALL"); // ALL, PENDING, RESOLVED
  const [activeNotes, setActiveNotes] = useState({});
  const [actionLoading, setActionLoading] = useState({});
  const [feedbackMessage, setFeedbackMessage] = useState(null);

  const fetchConflicts = async () => {
    try {
      setLoading(true);
      const res = await checkinApi.getOfflineConflicts({
        eventId: Number(eventId),
      });
      if (res?.data?.data) {
        setConflicts(res.data.data);
      }
    } catch (err) {
      console.error("Failed to load offline conflicts:", err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchConflicts();
    }
  }, [isOpen, eventId]);

  if (!isOpen) return null;

  const handleResolve = async (conflictId, decision) => {
    const note = activeNotes[conflictId]?.trim() || "";
    if (decision === "MANUAL_OVERRIDE_ADMIN" && !note) {
      alert("Please provide audit justification notes when selecting Manual Override!");
      return;
    }

    try {
      setActionLoading((prev) => ({ ...prev, [conflictId]: true }));
      const payload = {
        decision,
        notes: note || (
          decision === "ACCEPT_EARLIER_REJECT_LATER"
            ? "Accepted earlier scan as legitimate entry per standard admission policy."
            : decision === "REVOKE_BOTH_SEND_HELPDESK"
            ? "Revoked both scan admissions due to duplicate ticket dispute; escalated to Helpdesk."
            : "Administrator confirmed manual exception override."
        ),
      };

      const res = await checkinApi.resolveOfflineConflict(conflictId, payload);
      if (res?.data?.data) {
        setFeedbackMessage({
          type: "success",
          text: `Conflict #${conflictId} resolved successfully (${decision})!`,
        });
        setTimeout(() => setFeedbackMessage(null), 4000);
        await fetchConflicts();
        if (onConflictResolved) onConflictResolved(res.data.data);
      }
    } catch (err) {
      console.error("Resolve conflict error:", err);
      setFeedbackMessage({
        type: "error",
        text: "Could not resolve conflict: " + (err.response?.data?.message || err.message),
      });
    } finally {
      setActionLoading((prev) => ({ ...prev, [conflictId]: false }));
    }
  };

  const filteredConflicts = conflicts.filter((c) => {
    if (filterStatus === "PENDING") return c.status === "PENDING" || c.status === "PENDING_REVIEW";
    if (filterStatus === "RESOLVED") return c.status !== "PENDING" && c.status !== "PENDING_REVIEW";
    return true;
  });

  const pendingCount = conflicts.filter((c) => c.status === "PENDING" || c.status === "PENDING_REVIEW").length;

  return (
    <div className="conflict-modal-overlay">
      <div className="conflict-modal-content">
        <header className="conflict-modal-header">
          <div className="header-title-area">
            <span className="header-icon">⚠️</span>
            <div>
              <h3>Offline Check-in Conflict Resolution</h3>
              <p>
                Resolves duplicate scan collisions between multiple offline terminals or between local offline cache and central server.
              </p>
            </div>
          </div>
          <div className="header-actions">
            <span className={`pending-badge ${pendingCount > 0 ? "has-pending" : ""}`}>
              {pendingCount} Pending Review
            </span>
            <button className="btn-close-modal" onClick={onClose}>
              ✕
            </button>
          </div>
        </header>

        {feedbackMessage && (
          <div className={`conflict-toast-banner ${feedbackMessage.type}`}>
            {feedbackMessage.text}
          </div>
        )}

        <div className="conflict-toolbar">
          <div className="filter-tabs">
            <button
              className={`filter-tab ${filterStatus === "ALL" ? "active" : ""}`}
              onClick={() => setFilterStatus("ALL")}
            >
              All Records ({conflicts.length})
            </button>
            <button
              className={`filter-tab ${filterStatus === "PENDING" ? "active" : ""}`}
              onClick={() => setFilterStatus("PENDING")}
            >
              Pending ({pendingCount})
            </button>
            <button
              className={`filter-tab ${filterStatus === "RESOLVED" ? "active" : ""}`}
              onClick={() => setFilterStatus("RESOLVED")}
            >
              Resolved ({conflicts.length - pendingCount})
            </button>
          </div>

          <button
            className="btn-refresh-conflicts"
            onClick={fetchConflicts}
            disabled={loading}
          >
            {loading ? "🔄 Loading..." : "🔄 Refresh List"}
          </button>
        </div>

        <div className="conflict-list-body">
          {loading && conflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <div className="spinner"></div>
              <p>Loading conflict audit records...</p>
            </div>
          ) : filteredConflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <span className="empty-icon">✅</span>
              <h4>No Admission Conflicts Found</h4>
              <p>All synchronized offline check-in records are consistent and validated against the central database.</p>
            </div>
          ) : (
            filteredConflicts.map((c) => {
              const isPending = c.status === "PENDING" || c.status === "PENDING_REVIEW";
              const isLoadingThis = !!actionLoading[c.conflictId];

              return (
                <div
                  key={c.conflictId}
                  className={`conflict-card ${!isPending ? "resolved" : "pending"}`}
                >
                  <div className="conflict-card-header">
                    <div className="conflict-meta">
                      <span className="conflict-id">ID: #{c.conflictId}</span>
                      <span className="ticket-code-tag">🎟️ {c.ticketCode}</span>
                      <span className="attendee-name">Attendee: {c.attendeeName || "Attendee"}</span>
                      {c.zoneName && <span className="zone-tag">Zone: {c.zoneName}</span>}
                    </div>
                    <span className={`status-pill ${c.status?.toLowerCase()}`}>
                      {isPending
                        ? "⚠️ Pending Decision"
                        : c.status === "RESOLVED_EARLIER_VALID"
                        ? "✅ Earlier Scan Accepted"
                        : c.status === "RESOLVED_REVOKED_BOTH" || c.status === "REVOKED_BOTH_HELPDESK"
                        ? "⛔ Escalated to Helpdesk"
                        : "⚙️ Override Approved"}
                    </span>
                  </div>

                  <div className="conflict-reason-banner">
                    <strong>Collision Cause:</strong> Duplicate check-in scan registered across different gates or offline terminals within operational shift.
                  </div>

                  {/* Dual Stream Side-by-Side Comparison (UC_14.7) */}
                  <div className="dual-scan-comparison">
                    <div className="scan-stream-card stream-earlier">
                      <div className="stream-badge earlier-badge">
                        <span>1️⃣ Earlier Admission Scan (Scan 1)</span>
                        <span className="tag-recommended">Initial Legitimate</span>
                      </div>
                      <div className="stream-details">
                        <div className="detail-row">
                          <span className="lbl">Scanner / Terminal:</span>
                          <span className="val">{c.earlierMethod || "Online Dynamic QR"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Entrance Gate:</span>
                          <span className="val font-bold">{c.earlierGateName || c.earlierGateId || "Gate A - Main Entrance"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Scan Timestamp:</span>
                          <span className="val highlight">
                            {new Date(c.earlierScannedAt || Date.now()).toLocaleTimeString()}
                          </span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Operating Staff:</span>
                          <span className="val">{c.earlierStaffName || "Gate Staff"}</span>
                        </div>
                      </div>
                    </div>

                    <div className="comparison-divider">
                      <div className="time-delta-pill">
                        ⚡ Duplicate Collision
                      </div>
                    </div>

                    <div className="scan-stream-card stream-later">
                      <div className="stream-badge later-badge">
                        <span>2️⃣ Duplicate Scan (Scan 2)</span>
                        <span className="tag-warning">Potential Duplicate</span>
                      </div>
                      <div className="stream-details">
                        <div className="detail-row">
                          <span className="lbl">Scanner / Terminal:</span>
                          <span className="val">{c.duplicateMethod || "Offline QR Scan"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Entrance Gate:</span>
                          <span className="val font-bold">{c.duplicateGateName || c.duplicateGateId || "Gate A - Main Entrance"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Scan Timestamp:</span>
                          <span className="val highlight warning">
                            {new Date(c.duplicateScannedAt || Date.now()).toLocaleTimeString()}
                          </span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Operating Staff:</span>
                          <span className="val">{c.duplicateStaffName || "Offline Staff"}</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Resolution Section */}
                  {isPending ? (
                    <div className="resolution-action-box">
                      <div className="resolution-input-row">
                        <label htmlFor={`notes-${c.conflictId}`}>
                          Audit Reconciliation Notes (Mandatory for Manual Override):
                        </label>
                        <input
                          id={`notes-${c.conflictId}`}
                          type="text"
                          className="conflict-note-input"
                          placeholder="Enter audit notes (e.g. Verified original ID, matched attendee identity, physical card checked)..."
                          value={activeNotes[c.conflictId] || ""}
                          onChange={(e) =>
                            setActiveNotes({
                              ...activeNotes,
                              [c.conflictId]: e.target.value,
                            })
                          }
                        />
                      </div>

                      <div className="decision-buttons">
                        <button
                          className="btn-decision btn-accept-earlier"
                          disabled={isLoadingThis}
                          onClick={() =>
                            handleResolve(c.conflictId, "ACCEPT_EARLIER_REJECT_LATER")
                          }
                          title="Accept the earlier admission scan as valid and reject second duplicate"
                        >
                          {isLoadingThis ? "Processing..." : "✅ Accept Earlier Scan (Reject Scan 2)"}
                        </button>

                        <button
                          className="btn-decision btn-revoke-both"
                          disabled={isLoadingThis}
                          onClick={() =>
                            handleResolve(c.conflictId, "REVOKE_BOTH_SEND_HELPDESK")
                          }
                          title="Revoke both admissions and direct attendees to the Helpdesk counter"
                        >
                          {isLoadingThis ? "Processing..." : "⛔ Revoke Both & Escalate to Helpdesk"}
                        </button>

                        <button
                          className="btn-decision btn-manual-override"
                          disabled={isLoadingThis}
                          onClick={() =>
                            handleResolve(c.conflictId, "MANUAL_OVERRIDE_ADMIN")
                          }
                          title="Administrator manually approves admittance exception"
                        >
                          {isLoadingThis ? "Processing..." : "⚙️ Manual Override (Admin)"}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <div className="resolution-audit-box">
                      <div className="audit-header">
                        <span className="icon">🛡️</span>
                        <strong>Immutable Audit Log Record</strong>
                      </div>
                      <div className="audit-details">
                        <div>
                          <span>Decided By:</span> <strong>{c.resolvedByName || "Platform Administrator"}</strong>
                        </div>
                        <div>
                          <span>Timestamp:</span>{" "}
                          <strong>{new Date(c.resolvedAt || Date.now()).toLocaleString()}</strong>
                        </div>
                        <div>
                          <span>Audit Notes:</span>{" "}
                          <em>{c.resolutionNotes || "No additional audit notes recorded."}</em>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>

        <footer className="conflict-modal-footer">
          <div className="footer-legend">
            <span>🛡️ All conflict resolution decisions are immutably logged into the central audit trail.</span>
          </div>
          <button className="btn-footer-close" onClick={onClose}>
            Close Window
          </button>
        </footer>
      </div>
    </div>
  );
}
