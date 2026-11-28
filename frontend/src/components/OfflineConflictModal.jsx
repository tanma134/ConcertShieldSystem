import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import checkinApi from "../api/checkinApi";
import "./OfflineConflictModal.css";

export default function OfflineConflictModal({
  isOpen,
  onClose,
  eventId,
  hideIncidentReport = false,
}) {
  const navigate = useNavigate();
  const [conflicts, setConflicts] = useState([]);
  const [loading, setLoading] = useState(false);

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

  const handleGoToIncidentReport = (conflict) => {
    if (onClose) onClose();
    navigate("/staff/incidents/new", {
      state: {
        prefillIncident: {
          eventId: conflict?.eventId || eventId,
          title: `Duplicate Ticket Conflict (Online/Offline Sync) - ${conflict?.attendeeName || "Attendee"}`,
          description: `Duplicate check-in detected between online and offline sync.\nAudit ID: ${conflict?.conflictId}\nAttendee: ${conflict?.attendeeName || "N/A"}\nZone/Seat: ${conflict?.zoneName || "General"} (${conflict?.seatNumber || "Standing"})\nEarlier Scan: ${conflict?.earlierMethod || "Online QR"} at ${conflict?.earlierGateName || "Gate A"} (${new Date(conflict?.earlierScannedAt || Date.now()).toLocaleString()})\nDuplicate Scan: ${conflict?.duplicateMethod || "Offline QR"} at ${conflict?.duplicateGateName || "Gate A"} (${new Date(conflict?.duplicateScannedAt || Date.now()).toLocaleString()})`,
        },
      },
    });
  };

  return (
    <div className="conflict-modal-overlay">
      <div className="conflict-modal-content">
        <header className="conflict-modal-header">
          <div className="header-title-area">
            <div>
              <h3>Online / Offline Sync Duplicate Ticket Conflicts</h3>
              <p>
                Information on tickets duplicated due to online and offline scan synchronization conflicts.
              </p>
            </div>
          </div>
          <div className="header-actions">
            <span className={`pending-badge ${conflicts.length > 0 ? "has-pending" : ""}`}>
              {conflicts.length} Conflict(s)
            </span>
            <button className="btn-close-modal" onClick={onClose} title="Close">
              Close
            </button>
          </div>
        </header>

        <div className="conflict-toolbar" style={{ justifyContent: "space-between" }}>
          <div style={{ fontSize: "13px", color: "#cbd5e1", fontWeight: 600 }}>
            Showing all detected online/offline duplicate ticket records ({conflicts.length})
          </div>
          <div style={{ display: "flex", gap: "8px" }}>
            <button
              type="button"
              className="btn-refresh-conflicts"
              onClick={fetchConflicts}
              disabled={loading}
            >
              {loading ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Refresh List"}
            </button>
            {!hideIncidentReport && (
              <button
                type="button"
                style={{
                  background: "#d91a63",
                  color: "#ffffff",
                  border: "none",
                  borderRadius: "6px",
                  padding: "7px 14px",
                  fontSize: "13px",
                  fontWeight: 700,
                  cursor: "pointer",
                }}
                onClick={() => handleGoToIncidentReport(null)}
              >
                Report On-site Incident
              </button>
            )}
          </div>
        </div>

        <div className="conflict-list-body">
          {loading && conflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <span className="app-loading-spinner" role="status" aria-label="Loading" />
            </div>
          ) : conflicts.length === 0 ? (
            <div className="conflict-empty-state">
              <h4>No Online / Offline Duplicate Ticket Conflicts Found</h4>
              <p>All synchronized check-in records are consistent with no duplicate collisions.</p>
            </div>
          ) : (
            conflicts.map((c) => (
              <div
                key={c.conflictId}
                className="conflict-card pending"
              >
                <div className="conflict-card-header">
                  <div className="conflict-meta">
                    <span className="conflict-id">Conflict ID: {c.conflictId}</span>
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
                  <span className="status-pill pending">
                    Online / Offline Duplicate Conflict
                  </span>
                </div>

                <div className="conflict-reason-banner">
                  <strong>Conflict Details:</strong> This ticket was scanned multiple times due to an online and offline check-in synchronization conflict.
                </div>

                {/* Dual Stream Side-by-Side Comparison */}
                <div className="dual-scan-comparison">
                  <div className="scan-stream-card stream-earlier">
                    <div className="stream-badge earlier-badge">
                      <span>First Scan (Scan 1)</span>
                      <span className="tag-recommended">Initial Scan</span>
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
                      Duplicate Ticket
                    </div>
                  </div>

                  <div className="scan-stream-card stream-later">
                    <div className="stream-badge later-badge">
                      <span>Second Scan (Scan 2)</span>
                      <span className="tag-warning">Offline / Duplicate Scan</span>
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

                {/* Simple button to navigate to Incident / Support Report (Hidden in Organizer read-only view) */}
                {!hideIncidentReport && (
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
                      Submit an incident report for this duplicate ticket conflict:
                    </span>
                    <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                      <button
                        type="button"
                        style={{
                          background: "#d91a63",
                          color: "#fff",
                          border: "none",
                          borderRadius: "6px",
                          padding: "8px 16px",
                          fontSize: "12.5px",
                          fontWeight: 700,
                          cursor: "pointer",
                        }}
                        onClick={() => handleGoToIncidentReport(c)}
                      >
                        Report to Incident Support
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))
          )}
        </div>

        <footer className="conflict-modal-footer">
          <div className="footer-legend">
            <span>
              {hideIncidentReport
                ? "Duplicate ticket conflict records are displayed for organizer review and verification."
                : "Duplicate ticket conflict records are displayed for verification and incident reporting."}
            </span>
          </div>
          <button className="btn-footer-close" onClick={onClose}>
            Close Window
          </button>
        </footer>
      </div>
    </div>
  );
}
