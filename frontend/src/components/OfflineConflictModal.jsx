import React, { useState, useEffect } from "react";
import checkinApi from "../api/checkinApi";
import "./OfflineConflictModal.css";

export default function OfflineConflictModal({
  isOpen,
  onClose,
  eventId,
  onConflictResolved,
}) {
  const [conflicts, setConflicts] = useState([]);
  const [loading, setLoading] = useState(false);
  const [resolvingId, setResolvingId] = useState(null);
  const [filterStatus, setFilterStatus] = useState("ALL"); // ALL, PENDING, RESOLVED
  const [actionNotice, setActionNotice] = useState(null);

  const fetchConflicts = async () => {
    try {
      setLoading(true);
      const params = {};
      if (eventId && Number(eventId) > 0) {
        params.eventId = Number(eventId);
      }
      const res = await checkinApi.getOfflineConflicts(params);
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

  const handleResolve = async (conflictId, decision, defaultNote) => {
    try {
      setResolvingId(conflictId);
      const payload = {
        decision,
        notes: defaultNote,
      };
      await checkinApi.resolveOfflineConflict(conflictId, payload);
      setActionNotice({
        type: "SUCCESS",
        text: `Conflict ${conflictId} successfully resolved: ${decision}`,
      });
      setTimeout(() => setActionNotice(null), 4000);
      await fetchConflicts();
      if (onConflictResolved) onConflictResolved();
    } catch (err) {
      console.error("Failed to resolve conflict:", err);
      setActionNotice({
        type: "ERROR",
        text: "Error resolving conflict: " + (err.response?.data?.message || err.message),
      });
      setTimeout(() => setActionNotice(null), 5000);
    } finally {
      setResolvingId(null);
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
            <div>
              <h3>Duplicate Ticket & Scan Collision Audit</h3>
              <p>
                Reconcile duplicate scan collisions between online gate admissions and offline sync terminals.
              </p>
            </div>
          </div>
          <div className="header-actions">
            <span className={`pending-badge ${pendingCount > 0 ? "has-pending" : ""}`}>
              {pendingCount} Pending Review
            </span>
            <button className="btn-close-modal" onClick={onClose} title="Close">
              Close
            </button>
          </div>
        </header>

        {actionNotice && (
          <div
            style={{
              padding: "10px 20px",
              background: actionNotice.type === "SUCCESS" ? "rgba(16, 185, 129, 0.2)" : "rgba(239, 68, 68, 0.2)",
              color: actionNotice.type === "SUCCESS" ? "#34d399" : "#f87171",
              borderBottom: "1px solid rgba(255,255,255,0.1)",
              fontSize: "13px",
              fontWeight: 600,
            }}
          >
            {actionNotice.text}
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
            {loading ? "Loading..." : "Refresh List"}
          </button>
        </div>

        <div className="conflict-list-body">
          {loading && conflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <div className="spinner"></div>
              <p>Loading duplicate audit records...</p>
            </div>
          ) : filteredConflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <h4>No Admission Conflicts Found</h4>
              <p>All synchronized check-in records are consistent and validated against the central database.</p>
            </div>
          ) : (
            filteredConflicts.map((c) => {
              const isPending = c.status === "PENDING" || c.status === "PENDING_REVIEW";
              const isResolving = resolvingId === c.conflictId;

              return (
                <div
                  key={c.conflictId}
                  className={`conflict-card ${!isPending ? "resolved" : "pending"}`}
                >
                  <div className="conflict-card-header">
                    <div className="conflict-meta">
                      <span className="conflict-id">Audit ID: {c.conflictId}</span>
                      <span className="attendee-name" style={{ color: "#f8fafc", fontWeight: 700 }}>
                        {c.attendeeName || "Attendee"}
                      </span>
                      {c.zoneName && <span className="zone-tag">{c.zoneName}</span>}
                      {c.seatNumber && (
                        <span style={{ fontSize: "12px", color: "#94a3b8" }}>
                          Seat: {c.seatNumber}
                        </span>
                      )}
                    </div>
                    <span className={`status-pill ${c.status?.toLowerCase()}`}>
                      {isPending
                        ? "Duplicate Collision Detected"
                        : c.status === "RESOLVED"
                        ? "Resolved & Verified"
                        : c.status}
                    </span>
                  </div>

                  {/* Dual Stream Side-by-Side Comparison */}
                  <div className="dual-scan-comparison" style={{ margin: "14px 0" }}>
                    <div className="scan-stream-card stream-earlier">
                      <div className="stream-badge earlier-badge">
                        <span>Earlier Admission Scan (Scan 1)</span>
                        <span className="tag-recommended">Initial Entry</span>
                      </div>
                      <div className="stream-details">
                        <div className="detail-row">
                          <span className="lbl">Method:</span>
                          <span className="val">{c.earlierMethod || "Online Dynamic QR"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Entrance Gate:</span>
                          <span className="val font-bold">{c.earlierGateName || c.earlierGateId || "Gate A"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Scan Timestamp:</span>
                          <span className="val highlight">
                            {new Date(c.earlierScannedAt || Date.now()).toLocaleString()}
                          </span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Operating Staff:</span>
                          <span className="val">{c.earlierStaffName || "Online Gate Staff"}</span>
                        </div>
                      </div>
                    </div>

                    <div className="comparison-divider">
                      <div className="time-delta-pill">
                        Duplicate Collision
                      </div>
                    </div>

                    <div className="scan-stream-card stream-later">
                      <div className="stream-badge later-badge">
                        <span>Duplicate Scan (Scan 2)</span>
                        <span className="tag-warning">Duplicate Detected</span>
                      </div>
                      <div className="stream-details">
                        <div className="detail-row">
                          <span className="lbl">Method:</span>
                          <span className="val">{c.duplicateMethod || "Offline QR Scan"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Entrance Gate:</span>
                          <span className="val font-bold">{c.duplicateGateName || c.duplicateGateId || "Gate A"}</span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Scan Timestamp:</span>
                          <span className="val highlight warning">
                            {new Date(c.duplicateScannedAt || Date.now()).toLocaleString()}
                          </span>
                        </div>
                        <div className="detail-row">
                          <span className="lbl">Operating Staff:</span>
                          <span className="val">{c.duplicateStaffName || "Offline Staff"}</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Action or Audit Trail Note */}
                  {isPending ? (
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        flexWrap: "wrap",
                        gap: "10px",
                        background: "rgba(245, 158, 11, 0.08)",
                        border: "1px solid rgba(245, 158, 11, 0.25)",
                        borderRadius: "8px",
                        padding: "12px 14px",
                      }}
                    >
                      <span style={{ fontSize: "0.85rem", color: "#fde68a" }}>
                        Select resolution action to finalize this duplicate ticket conflict:
                      </span>
                      <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                        <button
                          type="button"
                          className="btn-action-resolve-green"
                          style={{
                            background: "#059669",
                            color: "#fff",
                            border: "none",
                            borderRadius: "6px",
                            padding: "6px 12px",
                            fontSize: "12px",
                            fontWeight: 700,
                            cursor: isResolving ? "not-allowed" : "pointer",
                          }}
                          disabled={isResolving}
                          onClick={() =>
                            handleResolve(
                              c.conflictId,
                              "ACCEPT_EARLIER_REJECT_LATER",
                              "Earlier scan verified as valid attendee admission; duplicate rejected."
                            )
                          }
                        >
                          Accept Earlier Entry (Scan 1)
                        </button>
                        <button
                          type="button"
                          className="btn-action-resolve-blue"
                          style={{
                            background: "#2563eb",
                            color: "#fff",
                            border: "none",
                            borderRadius: "6px",
                            padding: "6px 12px",
                            fontSize: "12px",
                            fontWeight: 700,
                            cursor: isResolving ? "not-allowed" : "pointer",
                          }}
                          disabled={isResolving}
                          onClick={() =>
                            handleResolve(
                              c.conflictId,
                              "OVERRIDE_ACCEPT_OFFLINE",
                              "Override approved: Offline scan verified as authentic ticket holder entry."
                            )
                          }
                        >
                          Accept Offline Entry (Scan 2)
                        </button>
                        <button
                          type="button"
                          className="btn-action-resolve-red"
                          style={{
                            background: "#dc2626",
                            color: "#fff",
                            border: "none",
                            borderRadius: "6px",
                            padding: "6px 12px",
                            fontSize: "12px",
                            fontWeight: 700,
                            cursor: isResolving ? "not-allowed" : "pointer",
                          }}
                          disabled={isResolving}
                          onClick={() =>
                            handleResolve(
                              c.conflictId,
                              "REVOKE_BOTH_ESCALATE",
                              "Flagged duplicate credential collision; escalated to on-site Helpdesk."
                            )
                          }
                        >
                          Escalate to Helpdesk
                        </button>
                      </div>
                    </div>
                  ) : (
                    <div className="resolution-audit-box">
                      <span style={{ fontSize: "0.85rem", color: "#34d399", fontWeight: 600 }}>
                        Audit Resolution: {c.resolutionNotes || c.status}
                      </span>
                      {c.resolvedByName && (
                        <div style={{ fontSize: "0.78rem", color: "#94a3b8", marginTop: "4px" }}>
                          Decided by: <strong>{c.resolvedByName}</strong> on {new Date(c.resolvedAt || Date.now()).toLocaleString()}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })
          )}
        </div>

        <footer className="conflict-modal-footer">
          <div className="footer-legend">
            <span>Duplicate admission records are stored immutably in the central audit database.</span>
          </div>
          <button className="btn-footer-close" onClick={onClose}>
            Close Window
          </button>
        </footer>
      </div>
    </div>
  );
}
