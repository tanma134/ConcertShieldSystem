import { useEffect, useState, useMemo } from "react";
import { Link, useNavigate } from "react-router-dom";
import incidentApi from "../../api/incidentApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "./StaffShell";
import AdminShell from "../admin/AdminShell";
import "../../styles/incidentManagement.css";

/* ==========================================================================
   MISSION-CRITICAL VECTOR ICONS (Zero-dependency, crisp, high-tech)
   ========================================================================== */
const SearchIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="11" cy="11" r="8" />
    <line x1="21" y1="21" x2="16.65" y2="16.65" />
  </svg>
);

const PlusIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" y1="5" x2="12" y2="19" />
    <line x1="5" y1="12" x2="19" y2="12" />
  </svg>
);

const FlameIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z" />
  </svg>
);

const ShieldCheckIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    <polyline points="9 12 11 14 15 10" />
  </svg>
);

const ClockIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

const MapPinIcon = () => (
  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <circle cx="12" cy="10" r="3" />
  </svg>
);

const PaperclipIcon = () => (
  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
  </svg>
);

const AlertTriangleIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
    <line x1="12" y1="9" x2="12" y2="13" />
    <line x1="12" y1="17" x2="12.01" y2="17" />
  </svg>
);

const EyeIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
    <circle cx="12" cy="12" r="3" />
  </svg>
);

const ActivityIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);

/**
 * IncidentListPage
 * Complete on-site incident monitoring & triage list for Staff and Admin
 */
export default function IncidentListPage() {
  const { isAdmin } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();

  const [incidents, setIncidents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState("");
  const [selectedStatus, setSelectedStatus] = useState("");
  const [selectedSeverity, setSelectedSeverity] = useState("");
  const [selectedCategory, setSelectedCategory] = useState("");
  const [onlyEscalated, setOnlyEscalated] = useState(false);

  // Quick Escalation Modal (UC_16.4)
  const [escalateTarget, setEscalateTarget] = useState(null);
  const [escalateReason, setEscalateReason] = useState("");
  const [isEscalating, setIsEscalating] = useState(false);

  const loadIncidents = async () => {
    setLoading(true);
    try {
      const res = await incidentApi.getIncidents({
        status: selectedStatus || undefined,
        severity: selectedSeverity || undefined,
        category: selectedCategory || undefined,
        isEscalated: onlyEscalated ? true : undefined,
      });
      const list = Array.isArray(res.data) ? res.data : (res.data?.data || []);
      setIncidents(list);
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not load incident reports.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadIncidents();
  }, [selectedStatus, selectedSeverity, selectedCategory, onlyEscalated]);

  const filteredIncidents = useMemo(() => {
    if (!searchTerm.trim()) return incidents;
    const term = searchTerm.toLowerCase();
    return incidents.filter(
      (inc) =>
        inc.title?.toLowerCase().includes(term) ||
        inc.location?.toLowerCase().includes(term) ||
        inc.eventName?.toLowerCase().includes(term) ||
        inc.reporterName?.toLowerCase().includes(term) ||
        inc.description?.toLowerCase().includes(term)
    );
  }, [incidents, searchTerm]);

  const metrics = useMemo(() => {
    return {
      total: incidents.length,
      inProgress: incidents.filter((i) => i.status === "InProgress" || i.status === "Investigating").length,
      escalated: incidents.filter((i) => i.isEscalated || i.status === "Escalated").length,
      resolved: incidents.filter((i) => i.status === "Resolved" || i.status === "Closed").length,
    };
  }, [incidents]);

  const handleConfirmEscalate = async (e) => {
    e.preventDefault();
    if (!escalateReason.trim()) {
      toast.error("Please enter a reason for escalating to Admin.");
      return;
    }
    setIsEscalating(true);
    try {
      await incidentApi.escalateIncident(escalateTarget.id, {
        reason: escalateReason,
      });
      toast.success(`Incident #${escalateTarget.id} escalated to Admin successfully!`);
      setEscalateTarget(null);
      setEscalateReason("");
      loadIncidents();
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not escalate incident.");
    } finally {
      setIsEscalating(false);
    }
  };

  const getSeverityBadge = (severity) => {
    const sev = (severity || "").toLowerCase();
    switch (sev) {
      case "critical":
        return <span className="inc-badge inc-sev-critical">Critical</span>;
      case "high":
        return <span className="inc-badge inc-sev-high">High</span>;
      case "medium":
        return <span className="inc-badge inc-sev-medium">Medium</span>;
      default:
        return <span className="inc-badge inc-sev-low">Low</span>;
    }
  };

  const getStatusBadge = (status, isEscalated) => {
    if (isEscalated || status === "Escalated") {
      return <span className="inc-badge inc-status-escalated">Escalated</span>;
    }
    switch (status) {
      case "Resolved":
        return <span className="inc-badge inc-status-resolved">Resolved</span>;
      case "InProgress":
        return <span className="inc-badge inc-status-inprogress">In Progress</span>;
      case "Investigating":
        return <span className="inc-badge inc-status-investigating">Investigating</span>;
      case "Closed":
        return <span className="inc-badge inc-status-closed">Closed</span>;
      default:
        return <span className="inc-badge inc-status-reported">Reported</span>;
    }
  };

  const Shell = isAdmin ? AdminShell : StaffShell;

  return (
    <Shell title="On-site Incident Management">
      <div className="inc-container">
        {/* Header Bar */}
        <div className="inc-header">
          <div className="inc-header-title">
            <div className="inc-dossier-pill">
              <span>Command Center</span>
            </div>
            <h1>On-site Incident Management</h1>
            <p>Live situational monitoring, evidence vault synchronization, escalation routing, and incident resolution</p>
          </div>
          <div className="inc-header-actions">
            <Link
              to={isAdmin ? "/admin/incidents/new" : "/staff/incidents/new"}
              className="inc-btn inc-btn-primary"
            >
              Report New Incident
            </Link>
          </div>
        </div>

        {/* Metrics Grid */}
        <div className="inc-metrics-grid">
          <div className="inc-metric-card">
            <span className="inc-metric-label">Total Incidents</span>
            <span className="inc-metric-value">{metrics.total}</span>
          </div>
          <div className="inc-metric-card">
            <span className="inc-metric-label">Active / In Progress</span>
            <span className="inc-metric-value">{metrics.inProgress}</span>
          </div>
          <div className="inc-metric-card">
            <span className="inc-metric-label">Escalated to Admin</span>
            <span className="inc-metric-value">{metrics.escalated}</span>
          </div>
          <div className="inc-metric-card">
            <span className="inc-metric-label">Resolved</span>
            <span className="inc-metric-value">{metrics.resolved}</span>
          </div>
        </div>

        {/* Filters */}
        <div className="inc-filter-bar">
          <div className="inc-search-box">
            <input
              type="text"
              placeholder="Filter incidents by title, location, event, reporter..."
              className="inc-search-input"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <select
            className="inc-select"
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
          >
            <option value="">All Statuses</option>
            <option value="Reported">Reported</option>
            <option value="Investigating">Investigating</option>
            <option value="InProgress">In Progress</option>
            <option value="Escalated">Escalated</option>
            <option value="Resolved">Resolved</option>
            <option value="Closed">Closed</option>
          </select>

          <select
            className="inc-select"
            value={selectedSeverity}
            onChange={(e) => setSelectedSeverity(e.target.value)}
          >
            <option value="">All Severities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>

          <select
            className="inc-select"
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
          >
            <option value="">All Categories</option>
            <option value="Security">Security</option>
            <option value="Medical">Medical</option>
            <option value="Technical">Technical</option>
            <option value="CrowdControl">Crowd Control</option>
            <option value="Facility">Facility</option>
            <option value="Other">Other</option>
          </select>

          <label style={{ display: "flex", alignItems: "center", gap: "6px", fontSize: "13px", cursor: "pointer", color: "#e5e7eb" }}>
            <input
              type="checkbox"
              checked={onlyEscalated}
              onChange={(e) => setOnlyEscalated(e.target.checked)}
            />
            Only Escalated
          </label>
        </div>

        {/* Data Table */}
        <div className="inc-table-card">
          {loading ? (
            <div className="inc-empty-state">
              <div className="inc-empty-icon">
                <ClockIcon />
              </div>
              <div className="inc-empty-title">Synchronizing incident database...</div>
            </div>
          ) : filteredIncidents.length === 0 ? (
            <div className="inc-empty-state">
              <div className="inc-empty-icon">
                <ShieldCheckIcon />
              </div>
              <div className="inc-empty-title">No Incident Logs Found</div>
              <p className="inc-empty-desc">
                No active or historical reports match your selected criteria. Create a new incident report to log activity.
              </p>
              <Link
                to={isAdmin ? "/admin/incidents/new" : "/staff/incidents/new"}
                className="inc-btn inc-btn-primary"
              >
                <PlusIcon />
                Report New Incident
              </Link>
            </div>
          ) : (
            <table className="inc-table">
              <thead>
                <tr>
                  <th style={{ width: "80px" }}>ID</th>
                  <th>Incident Overview</th>
                  <th>Event & Location</th>
                  <th>Severity</th>
                  <th>Status</th>
                  <th>Reporter</th>
                  <th>Reported Time</th>
                  <th style={{ textAlign: "right" }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredIncidents.map((inc) => (
                  <tr key={inc.id}>
                    <td>
                      <strong style={{ color: "#94a3b8", fontFamily: "ui-monospace, monospace" }}>
                        #{inc.id}
                      </strong>
                    </td>
                    <td>
                      <div style={{ fontWeight: 700, color: "#fff", marginBottom: "4px" }}>
                        {inc.title}
                      </div>
                      <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                        <span className="inc-cat-badge">{inc.category}</span>
                        {inc.evidenceCount > 0 && (
                          <span style={{ fontSize: "11px", color: "#9ca3af", display: "inline-flex", alignItems: "center" }}>
                            {inc.evidenceCount} {inc.evidenceCount === 1 ? "file" : "files"}
                          </span>
                        )}
                      </div>
                    </td>
                    <td>
                      <div style={{ color: "#e2e8f0", fontWeight: 600 }}>
                        {inc.eventName || `Event #${inc.eventId}`}
                      </div>
                      <div style={{ fontSize: "12px", color: "#d1d5db", marginTop: "2px" }}>
                        {inc.location}
                      </div>
                    </td>
                    <td>{getSeverityBadge(inc.severity)}</td>
                    <td>{getStatusBadge(inc.status, inc.isEscalated)}</td>
                    <td>
                      <div style={{ color: "#f1f5f9", fontWeight: 600 }}>{inc.reporterName}</div>
                      <div style={{ fontSize: "11px", color: "var(--inc-text-muted)" }}>ID: {inc.reporterId}</div>
                    </td>
                    <td>
                      <div style={{ fontSize: "12px", color: "#cbd5e1" }}>
                        {new Date(inc.createdAt).toLocaleString("en-US", {
                          hour: "2-digit",
                          minute: "2-digit",
                          month: "short",
                          day: "numeric",
                        })}
                      </div>
                    </td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", gap: "8px", justifyContent: "flex-end" }}>
                        <button
                          type="button"
                          className="inc-btn inc-btn-secondary inc-btn-sm"
                          onClick={() =>
                            navigate(
                              isAdmin
                                ? `/admin/incidents/${inc.id}`
                                : `/staff/incidents/${inc.id}`
                            )
                          }
                        >
                          Dossier
                        </button>

                        {!isAdmin && !inc.isEscalated && inc.status !== "Resolved" && inc.status !== "Closed" && (
                          <button
                            type="button"
                            className="inc-btn inc-btn-warning inc-btn-sm"
                            title="Escalate to Executive Admin"
                            onClick={() => {
                              setEscalateTarget(inc);
                              setEscalateReason("");
                            }}
                          >
                            Escalate
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Modal: Escalate to Admin (UC_16.4) */}
        {escalateTarget && (
          <div className="inc-modal-overlay">
            <div className="inc-modal-content">
              <div className="inc-modal-header">
                <h3>Escalate Incident #{escalateTarget.id} to Admin</h3>
                <button
                  type="button"
                  className="inc-modal-close"
                  onClick={() => setEscalateTarget(null)}
                >
                  ✕
                </button>
              </div>
              <form onSubmit={handleConfirmEscalate}>
                <div className="inc-modal-body">
                  <div
                    style={{
                      background: "#2d1519",
                      border: "1px solid #7f1d1d",
                      borderRadius: "6px",
                      padding: "12px 14px",
                      marginBottom: "16px",
                      color: "#fecdd3",
                      fontSize: "13px",
                      lineHeight: "1.5",
                    }}
                  >
                    <strong style={{ color: "#fda4af" }}>Notice:</strong> Incident <strong>#{escalateTarget.id} - "{escalateTarget.title}"</strong> will be forwarded to administrator command for review.
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Escalation Justification <span className="required">*</span>
                    </label>
                    <textarea
                      className="inc-form-control"
                      rows="4"
                      placeholder="Specify reasons for escalation: exceeds staff scope, security perimeter breached, emergency medical response needed..."
                      value={escalateReason}
                      onChange={(e) => setEscalateReason(e.target.value)}
                      required
                    />
                  </div>
                </div>
                <div className="inc-modal-footer">
                  <button
                    type="button"
                    className="inc-btn inc-btn-secondary"
                    onClick={() => setEscalateTarget(null)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="inc-btn inc-btn-danger"
                    disabled={isEscalating}
                  >
                    {isEscalating ? "Escalating..." : "Confirm Escalation"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </Shell>
  );
}
