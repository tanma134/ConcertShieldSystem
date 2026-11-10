import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import incidentApi from "../../api/incidentApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "./StaffShell";
import AdminShell from "../admin/AdminShell";
import "../../styles/incidentManagement.css";

/* ==========================================================================
   MISSION-CRITICAL VECTOR ICONS (Zero-dependency, crisp, high-tech)
   ========================================================================== */
const ArrowLeftIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="19" y1="12" x2="5" y2="12" />
    <polyline points="12 19 5 12 12 5" />
  </svg>
);

const EditIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 20h9" />
    <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
  </svg>
);

const AlertTriangleIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
    <line x1="12" y1="9" x2="12" y2="13" />
    <line x1="12" y1="17" x2="12.01" y2="17" />
  </svg>
);

const CheckCircleIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
    <polyline points="22 4 12 14.01 9 11.01" />
  </svg>
);

const ShieldCheckIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    <polyline points="9 12 11 14 15 10" />
  </svg>
);

const FlameIcon = () => (
  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M8.5 14.5A2.5 2.5 0 0 0 11 12c0-1.38-.5-2-1-3-1.072-2.143-.224-4.054 2-6 .5 2.5 2 4.9 4 6.5 2 1.6 3 3.5 3 5.5a7 7 0 1 1-14 0c0-1.153.433-2.294 1-3a2.5 2.5 0 0 0 2.5 2.5z" />
  </svg>
);

const MapPinIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
    <circle cx="12" cy="10" r="3" />
  </svg>
);

const CalendarIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
    <line x1="16" y1="2" x2="16" y2="6" />
    <line x1="8" y1="2" x2="8" y2="6" />
    <line x1="3" y1="10" x2="21" y2="10" />
  </svg>
);

const ClockIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10" />
    <polyline points="12 6 12 12 16 14" />
  </svg>
);

const UserIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
    <circle cx="12" cy="7" r="4" />
  </svg>
);

const UploadIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
    <polyline points="17 8 12 3 7 8" />
    <line x1="12" y1="3" x2="12" y2="15" />
  </svg>
);

const TrashIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="3 6 5 6 21 6" />
    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
  </svg>
);

const ExternalLinkIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
    <polyline points="15 3 21 3 21 9" />
    <line x1="10" y1="14" x2="21" y2="3" />
  </svg>
);

const EyeIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
    <circle cx="12" cy="12" r="3" />
  </svg>
);

const ActivityIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
  </svg>
);

const PaperclipIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48" />
  </svg>
);

/**
 * IncidentDetailPage
 * Unified command hub for Concert Incident Management:
 *   - UC_16.1: View on-site incident details
 *   - UC_16.2: Attach additional evidence (photos, video, audio, PDF to Cloudinary)
 *   - UC_16.3: Update incident details & log progress notes
 *   - UC_16.4: Escalate incident to Admin
 *   - UC_16.5: View resolution report & Admin finalize resolution
 */
export default function IncidentDetailPage() {
  const { id } = useParams();
  const { user, isAdmin } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();

  const [incident, setIncident] = useState(null);
  const [evidences, setEvidences] = useState([]);
  const [history, setHistory] = useState([]);
  const [resolution, setResolution] = useState(null);
  const [loading, setLoading] = useState(true);

  // Modal State: UC_16.2 Upload Evidence
  const [showUploadModal, setShowUploadModal] = useState(false);
  const [uploadFile, setUploadFile] = useState(null);
  const [uploadCaption, setUploadCaption] = useState("");
  const [isUploading, setIsUploading] = useState(false);

  // Modal State: UC_16.3 Update Incident
  const [showEditModal, setShowEditModal] = useState(false);
  const [editForm, setEditForm] = useState({
    title: "",
    category: "",
    severity: "",
    location: "",
    description: "",
    status: "",
    updateNote: "",
  });
  const [isUpdating, setIsUpdating] = useState(false);

  // Modal State: UC_16.4 Escalate to Admin
  const [showEscalateModal, setShowEscalateModal] = useState(false);
  const [escalateReason, setEscalateReason] = useState("");
  const [isEscalating, setIsEscalating] = useState(false);

  // Modal State: UC_16.5 Resolve Incident (Admin Action)
  const [showResolveModal, setShowResolveModal] = useState(false);
  const [resolveForm, setResolveForm] = useState({
    resolutionSummary: "",
    actionTaken: "",
  });
  const [isResolving, setIsResolving] = useState(false);

  const [lightboxMedia, setLightboxMedia] = useState(null);

  const loadIncidentData = async () => {
    setLoading(true);
    try {
      const res = await incidentApi.getIncidentById(id);
      const incData = res.data?.data || res.data;
      setIncident(incData);

      setEditForm({
        title: incData.title || "",
        category: incData.category || "Security",
        severity: incData.severity || "Medium",
        location: incData.location || "",
        description: incData.description || "",
        status: incData.status || "Reported",
        updateNote: "",
      });

      try {
        const evRes = await incidentApi.getEvidences(id);
        const evList = Array.isArray(evRes.data) ? evRes.data : (evRes.data?.data || []);
        setEvidences(evList);
      } catch (evErr) {
        console.warn("Could not load evidence list:", evErr);
      }

      try {
        const histRes = await incidentApi.getHistory(id);
        const histList = Array.isArray(histRes.data) ? histRes.data : (histRes.data?.data || []);
        setHistory(histList);
      } catch (hErr) {
        console.warn("Could not load history timeline:", hErr);
      }

      if (incData.status === "Resolved" || incData.status === "Closed") {
        try {
          const resRes = await incidentApi.getResolution(id);
          setResolution(resRes.data?.data || resRes.data);
        } catch (rErr) {
          console.warn("Could not load resolution data:", rErr);
        }
      }
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not load incident details.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadIncidentData();
  }, [id]);

  const handleUploadEvidence = async (e) => {
    e.preventDefault();
    if (!uploadFile) {
      toast.error("Please choose a file to upload.");
      return;
    }
    setIsUploading(true);
    try {
      await incidentApi.uploadEvidence(id, uploadFile, uploadCaption);
      toast.success("Evidence uploaded to Cloudinary successfully!");
      setShowUploadModal(false);
      setUploadFile(null);
      setUploadCaption("");
      loadIncidentData();
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to upload evidence.");
    } finally {
      setIsUploading(false);
    }
  };

  const handleDeleteEvidence = async (evidenceId) => {
    if (!window.confirm("Are you sure you want to delete this evidence?")) return;
    try {
      await incidentApi.deleteEvidence(id, evidenceId);
      toast.success("Evidence removed.");
      loadIncidentData();
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not delete evidence.");
    }
  };

  const handleUpdateIncident = async (e) => {
    e.preventDefault();
    if (!editForm.title.trim() || !editForm.location.trim() || !editForm.description.trim()) {
      toast.error("Please complete all required fields.");
      return;
    }
    setIsUpdating(true);
    try {
      await incidentApi.updateIncident(id, {
        title: editForm.title.trim(),
        category: editForm.category,
        severity: editForm.severity,
        location: editForm.location.trim(),
        description: editForm.description.trim(),
        status: editForm.status,
        updateNote: editForm.updateNote.trim(),
      });
      toast.success("Incident progress updated successfully!");
      setShowEditModal(false);
      loadIncidentData();
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not update incident.");
    } finally {
      setIsUpdating(false);
    }
  };

  const handleEscalate = async (e) => {
    e.preventDefault();
    if (!escalateReason.trim()) {
      toast.error("Please enter an escalation reason.");
      return;
    }
    setIsEscalating(true);
    try {
      await incidentApi.escalateIncident(id, { reason: escalateReason.trim() });
      toast.success("Incident escalated to Admin successfully!");
      setShowEscalateModal(false);
      setEscalateReason("");
      loadIncidentData();
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to escalate incident.");
    } finally {
      setIsEscalating(false);
    }
  };

  const handleResolve = async (e) => {
    e.preventDefault();
    if (!resolveForm.resolutionSummary.trim() || !resolveForm.actionTaken.trim()) {
      toast.error("Please provide both the resolution summary and actions taken.");
      return;
    }
    setIsResolving(true);
    try {
      await incidentApi.resolveIncident(id, {
        resolutionSummary: resolveForm.resolutionSummary.trim(),
        actionTaken: resolveForm.actionTaken.trim(),
      });
      toast.success("Incident marked as Resolved successfully!");
      setShowResolveModal(false);
      loadIncidentData();
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not resolve incident.");
    } finally {
      setIsResolving(false);
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

  if (loading) {
    return (
      <Shell title="Incident Dossier">
        <div className="inc-container">
          <div className="inc-empty-state">
            <div className="inc-empty-icon">
              <ClockIcon />
            </div>
            <div className="inc-empty-title">Loading incident #{id}...</div>
            <div className="inc-empty-desc">Synchronizing dossier from Incident Command Center...</div>
          </div>
        </div>
      </Shell>
    );
  }

  if (!incident) {
    return (
      <Shell title="Incident Dossier">
        <div className="inc-container">
          <div className="inc-empty-state">
            <div className="inc-empty-icon" style={{ color: "#ef4444" }}>
              <AlertTriangleIcon />
            </div>
            <div className="inc-empty-title">Incident #{id} Not Found</div>
            <div className="inc-empty-desc">The requested incident record does not exist or has been archived.</div>
            <button
              type="button"
              className="inc-btn inc-btn-primary"
              onClick={() => navigate(isAdmin ? "/admin/incidents" : "/staff/incidents")}
            >
              <ArrowLeftIcon />
              Back to Overview
            </button>
          </div>
        </div>
      </Shell>
    );
  }

  const isResolvedOrClosed = incident.status === "Resolved" || incident.status === "Closed";

  return (
    <Shell title={`Incident #${incident.id}: ${incident.title}`}>
      <div className="inc-container">
        {/* Top Header */}
        <div className="inc-header">
          <div className="inc-header-title">
            <div style={{ display: "flex", alignItems: "center", gap: "10px", flexWrap: "wrap" }}>
              <div className="inc-dossier-pill">
                <span>Incident Dossier</span>
                <strong>#{incident.id}</strong>
              </div>
              {getStatusBadge(incident.status, incident.isEscalated)}
              {getSeverityBadge(incident.severity)}
            </div>
            <h1>{incident.title}</h1>
          </div>

          <div className="inc-header-actions">
            <button
              type="button"
              className="inc-btn inc-btn-secondary"
              onClick={() => navigate(isAdmin ? "/admin/incidents" : "/staff/incidents")}
            >
              Back to List
            </button>

            {!isResolvedOrClosed && (
              <button
                type="button"
                className="inc-btn inc-btn-secondary"
                onClick={() => setShowEditModal(true)}
              >
                Update Progress
              </button>
            )}

            {!isAdmin && !incident.isEscalated && !isResolvedOrClosed && (
              <button
                type="button"
                className="inc-btn inc-btn-warning"
                onClick={() => setShowEscalateModal(true)}
              >
                Escalate to Admin
              </button>
            )}

            {!isResolvedOrClosed && (!incident.isEscalated || isAdmin) && (
              <button
                type="button"
                className="inc-btn inc-btn-success"
                onClick={() => setShowResolveModal(true)}
              >
                Resolve Incident
              </button>
            )}

            {!isResolvedOrClosed && incident.isEscalated && !isAdmin && (
              <div
                style={{
                  fontSize: "12px",
                  fontWeight: 600,
                  color: "#fca5a5",
                  background: "#2d1519",
                  border: "1px solid #7f1d1d",
                  borderRadius: "6px",
                  padding: "6px 12px",
                  display: "inline-flex",
                  alignItems: "center",
                }}
              >
                Awaiting Admin Resolution
              </div>
            )}
          </div>
        </div>

        {/* Escalation Banner (UC_16.4) */}
        {incident.isEscalated && (
          <div className="inc-escalation-banner" style={{ marginBottom: "20px" }}>
            <div className="inc-escalation-content">
              <div className="inc-escalation-top">
                <h4>Incident Escalated to Management</h4>
                <span className="inc-badge inc-status-escalated">Escalated</span>
              </div>
              <p>
                <strong>Directive:</strong>{" "}
                {incident.escalationReason || "Administrative command intervention requested."}
              </p>
              <div className="inc-escalation-meta">
                Escalated At:{" "}
                {incident.escalatedAt
                  ? new Date(incident.escalatedAt).toLocaleString("en-US", {
                      dateStyle: "medium",
                      timeStyle: "short",
                    })
                  : "N/A"}
              </div>
            </div>
          </div>
        )}

        {/* Resolution Banner (UC_16.5) */}
        {isResolvedOrClosed && (
          <div className="inc-resolution-card" style={{ marginBottom: "20px" }}>
            <div className="inc-resolution-header">
              <div className="inc-resolution-title">
                <h3>Incident Resolution Documentation</h3>
                <div className="inc-resolution-meta">
                  Resolved By:{" "}
                  <strong style={{ color: "#fff" }}>
                    {incident.resolvedByName || resolution?.resolvedByName || "System Administrator"}
                  </strong>
                  {" • "}
                  Resolved At:{" "}
                  {incident.resolvedAt
                    ? new Date(incident.resolvedAt).toLocaleString("en-US", {
                        dateStyle: "medium",
                        timeStyle: "short",
                      })
                    : resolution?.resolvedAt
                    ? new Date(resolution.resolvedAt).toLocaleString("en-US", {
                        dateStyle: "medium",
                        timeStyle: "short",
                      })
                    : "N/A"}
                </div>
              </div>

              <div className="inc-resolved-stamp">
                Officially Resolved
              </div>
            </div>

            <div className="inc-resolution-grid">
              <div className="inc-resolution-box">
                <div className="inc-resolution-box-title">
                  Root Cause & Operational Summary
                </div>
                <p className="inc-resolution-box-content">
                  {incident.resolutionSummary || resolution?.resolutionSummary || "No detailed summary provided."}
                </p>
              </div>

              <div className="inc-resolution-box">
                <div className="inc-resolution-box-title">
                  Corrective Actions Executed
                </div>
                <p className="inc-resolution-box-content">
                  {incident.actionTaken || resolution?.actionTaken || "Resolved according to standard venue protocols."}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* 2-Column Grid */}
        <div className="inc-detail-grid">
          {/* Main Column */}
          <div className="inc-detail-main">
            {/* Information Card */}
            <div className="inc-card">
              <div className="inc-card-title">
                <div className="inc-card-title-left">
                  <span>Incident Dossier</span>
                </div>
                <span className="inc-cat-badge">{incident.category}</span>
              </div>

              <div className="inc-info-grid">
                <div className="inc-info-item">
                  <div className="inc-info-label">
                    Event
                  </div>
                  <div className="inc-info-val">
                    {incident.eventName || `Event #${incident.eventId}`}
                  </div>
                </div>

                <div className="inc-info-item">
                  <div className="inc-info-label">
                    Venue Location
                  </div>
                  <div className="inc-info-val" style={{ color: "#fcd34d" }}>
                    {incident.location}
                  </div>
                </div>

                <div className="inc-info-item">
                  <div className="inc-info-label">
                    Initial Reporter
                  </div>
                  <div className="inc-info-val">{incident.reporterName}</div>
                </div>

                <div className="inc-info-item">
                  <div className="inc-info-label">
                    Reported Time
                  </div>
                  <div className="inc-info-val" style={{ fontSize: "13px" }}>
                    {new Date(incident.createdAt).toLocaleString("en-US", {
                      dateStyle: "medium",
                      timeStyle: "short",
                    })}
                  </div>
                </div>
              </div>

              <div className="inc-desc-box">
                <div className="inc-desc-box-label">
                  Incident Log & Detailed Description
                </div>
                <div className="inc-desc-box-text">
                  {incident.description}
                </div>
              </div>
            </div>

            {/* Evidence Gallery (UC_16.2) */}
            <div className="inc-card">
              <div className="inc-card-title">
                <div className="inc-card-title-left">
                  <span>Attached Evidence Vault</span>
                  <span style={{ fontSize: "11px", background: "#374151", color: "#e5e7eb", padding: "2px 8px", borderRadius: "4px", fontWeight: 600 }}>
                    {evidences.length} {evidences.length === 1 ? "file" : "files"}
                  </span>
                </div>
                <button
                  type="button"
                  className="inc-btn inc-btn-primary inc-btn-sm"
                  onClick={() => setShowUploadModal(true)}
                >
                  Upload Evidence
                </button>
              </div>

              {evidences.length === 0 ? (
                <div className="inc-empty-state" style={{ padding: "40px 20px" }}>
                  <div className="inc-empty-title">No Media or Evidence Attached</div>
                  <p className="inc-empty-desc">
                    Attach photos, video recordings, audio dispatches, or witness statements directly to this incident report.
                  </p>
                  <button
                    type="button"
                    className="inc-btn inc-btn-secondary inc-btn-sm"
                    onClick={() => setShowUploadModal(true)}
                  >
                    Attach Files
                  </button>
                </div>
              ) : (
                <div className="inc-evidence-grid">
                  {evidences.map((ev) => {
                    const isImg =
                      ev.fileType?.toLowerCase().includes("image") ||
                      ev.mimeType?.startsWith("image/") ||
                      /\.(jpg|jpeg|png|webp|gif)$/i.test(ev.fileUrl);
                    const isVid =
                      ev.fileType?.toLowerCase().includes("video") ||
                      ev.mimeType?.startsWith("video/") ||
                      /\.(mp4|mov|webm)$/i.test(ev.fileUrl);

                    return (
                      <div key={ev.id} className="inc-evidence-card">
                        <div className="inc-evidence-thumb">
                          {isImg ? (
                            <>
                              <img
                                src={ev.fileUrl}
                                alt={ev.fileName}
                                onClick={() => setLightboxMedia({ url: ev.fileUrl, type: "image" })}
                              />
                              <div
                                className="inc-evidence-zoom-hint"
                                onClick={() => setLightboxMedia({ url: ev.fileUrl, type: "image" })}
                              >
                                Click to zoom
                              </div>
                            </>
                          ) : isVid ? (
                            <video
                              src={ev.fileUrl}
                              controls
                              preload="metadata"
                            />
                          ) : (
                            <div className="inc-evidence-placeholder">
                              <span>{ev.fileType || "Document"}</span>
                            </div>
                          )}
                        </div>

                        <div className="inc-evidence-info">
                          <div className="inc-evidence-name" title={ev.fileName}>
                            {ev.fileName}
                          </div>
                          {ev.caption && (
                            <div className="inc-evidence-caption" title={ev.caption}>
                              "{ev.caption}"
                            </div>
                          )}
                          <div className="inc-evidence-meta">
                            <span>{ev.uploadedByName}</span>
                            <span>{new Date(ev.uploadedAt).toLocaleDateString("en-US")}</span>
                          </div>
                        </div>

                        <div className="inc-evidence-actions">
                          <a
                            href={ev.fileUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="inc-btn inc-btn-secondary inc-btn-sm"
                            style={{ flex: 1, textAlign: "center" }}
                          >
                            Inspect
                          </a>
                          <button
                            type="button"
                            className="inc-btn inc-btn-danger inc-btn-sm"
                            title="Delete evidence"
                            onClick={() => handleDeleteEvidence(ev.id)}
                          >
                            Delete
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* Sidebar Timeline */}
          <div className="inc-detail-sidebar">
            <div className="inc-card">
              <div className="inc-card-title">
                <div className="inc-card-title-left">
                  <span>Audit Trail & Progress Log</span>
                </div>
                <span style={{ fontSize: "11px", color: "var(--inc-text-muted)", fontWeight: 700 }}>
                  {history.length} {history.length === 1 ? "entry" : "entries"}
                </span>
              </div>

              {history.length === 0 ? (
                <div style={{ color: "var(--inc-text-muted)", fontSize: "13px", padding: "24px 0", textAlign: "center" }}>
                  No timeline entries recorded yet.
                </div>
              ) : (
                <div className="inc-timeline">
                  {history.map((item, idx) => (
                    <div key={item.id || idx} className="inc-timeline-item">
                      <div className="inc-timeline-dot">
                        <div className="inc-timeline-dot-inner" />
                      </div>
                      <div className="inc-timeline-content">
                        <div className="inc-timeline-header">
                          <span className="inc-timeline-author">
                            {item.updatedByName}
                          </span>
                          <span className="inc-timeline-time">
                            {new Date(item.createdAt).toLocaleString("en-US", {
                              hour: "2-digit",
                              minute: "2-digit",
                              month: "short",
                              day: "numeric",
                            })}
                          </span>
                        </div>
                        <div style={{ fontSize: "11px", fontWeight: 700, color: "#818cf8", textTransform: "uppercase", letterSpacing: "0.05em", marginBottom: "4px" }}>
                          {item.actionType}
                          {item.newStatus && ` ➔ ${item.newStatus}`}
                        </div>
                        <div className="inc-timeline-note">{item.note}</div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* MODAL: UC_16.2 Upload Evidence */}
        {showUploadModal && (
          <div className="inc-modal-overlay">
            <div className="inc-modal-content">
              <div className="inc-modal-header">
                <h3>Attach Incident Evidence</h3>
                <button
                  type="button"
                  className="inc-modal-close"
                  onClick={() => setShowUploadModal(false)}
                >
                  ✕
                </button>
              </div>
              <form onSubmit={handleUploadEvidence}>
                <div className="inc-modal-body">
                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Choose Evidence File <span className="required">*</span>
                    </label>
                    <input
                      type="file"
                      className="inc-form-control"
                      accept="image/*,video/*,audio/*,application/pdf"
                      onChange={(e) => setUploadFile(e.target.files[0] || null)}
                      required
                    />
                    <div className="inc-form-help">
                      Accepts photos (JPG, PNG, WebP), video footage (MP4, MOV), audio logs, and PDF files. Assets are secured on Cloudinary.
                    </div>
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">Evidence Caption / Context</label>
                    <input
                      type="text"
                      className="inc-form-control"
                      placeholder="E.g. Crowd congestion observed near Section B, Gate 2..."
                      value={uploadCaption}
                      onChange={(e) => setUploadCaption(e.target.value)}
                    />
                  </div>
                </div>

                <div className="inc-modal-footer">
                  <button
                    type="button"
                    className="inc-btn inc-btn-secondary"
                    onClick={() => setShowUploadModal(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="inc-btn inc-btn-primary"
                    disabled={isUploading}
                  >
                    {isUploading ? "Uploading to Cloudinary..." : "Upload Evidence"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL: UC_16.3 Update Incident */}
        {showEditModal && (
          <div className="inc-modal-overlay">
            <div className="inc-modal-content">
              <div className="inc-modal-header">
                <h3>Update Incident Progress & Status</h3>
                <button
                  type="button"
                  className="inc-modal-close"
                  onClick={() => setShowEditModal(false)}
                >
                  ✕
                </button>
              </div>
              <form onSubmit={handleUpdateIncident}>
                <div className="inc-modal-body">
                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Operational Status <span className="required">*</span>
                    </label>
                    <select
                      className="inc-form-control"
                      value={editForm.status}
                      onChange={(e) => setEditForm({ ...editForm, status: e.target.value })}
                      required
                    >
                      <option value="Reported">Reported</option>
                      <option value="Investigating">Investigating / Attending</option>
                      <option value="InProgress">In Progress on Site</option>
                      {incident.isEscalated && <option value="Escalated">Escalated to Management</option>}
                    </select>
                    <div className="inc-form-help">
                      Note: To conclude and mark this incident as <strong>Resolved</strong>, use the dedicated "Resolve Incident" action to submit the required Root Cause & Countermeasure report.
                    </div>
                  </div>

                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "14px" }}>
                    <div className="inc-form-group">
                      <label className="inc-form-label">
                        Severity <span className="required">*</span>
                      </label>
                      <select
                        className="inc-form-control"
                        value={editForm.severity}
                        onChange={(e) => setEditForm({ ...editForm, severity: e.target.value })}
                        required
                      >
                        <option value="Low">Low</option>
                        <option value="Medium">Medium</option>
                        <option value="High">High</option>
                        <option value="Critical">Critical</option>
                      </select>
                    </div>

                    <div className="inc-form-group">
                      <label className="inc-form-label">
                        Category <span className="required">*</span>
                      </label>
                      <select
                        className="inc-form-control"
                        value={editForm.category}
                        onChange={(e) => setEditForm({ ...editForm, category: e.target.value })}
                        required
                      >
                        <option value="Security">Security</option>
                        <option value="Medical">Medical</option>
                        <option value="Technical">Technical</option>
                        <option value="CrowdControl">Crowd Control</option>
                        <option value="Facility">Facility</option>
                        <option value="Other">Other</option>
                      </select>
                    </div>
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Title <span className="required">*</span>
                    </label>
                    <input
                      type="text"
                      className="inc-form-control"
                      value={editForm.title}
                      onChange={(e) => setEditForm({ ...editForm, title: e.target.value })}
                      required
                    />
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      On-site Location <span className="required">*</span>
                    </label>
                    <input
                      type="text"
                      className="inc-form-control"
                      value={editForm.location}
                      onChange={(e) => setEditForm({ ...editForm, location: e.target.value })}
                      required
                    />
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Updated Description <span className="required">*</span>
                    </label>
                    <textarea
                      className="inc-form-control"
                      rows="3"
                      value={editForm.description}
                      onChange={(e) => setEditForm({ ...editForm, description: e.target.value })}
                      required
                    />
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Progress Note (Recorded in Audit Trail) <span className="required">*</span>
                    </label>
                    <input
                      type="text"
                      className="inc-form-control"
                      placeholder="E.g. Medical team dispatched, crowd dispersal initiated..."
                      value={editForm.updateNote}
                      onChange={(e) => setEditForm({ ...editForm, updateNote: e.target.value })}
                      required
                    />
                  </div>
                </div>

                <div className="inc-modal-footer">
                  <button
                    type="button"
                    className="inc-btn inc-btn-secondary"
                    onClick={() => setShowEditModal(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="inc-btn inc-btn-primary"
                    disabled={isUpdating}
                  >
                    {isUpdating ? "Saving..." : "Save Updates"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* MODAL: UC_16.4 Escalate to Admin */}
        {showEscalateModal && (
          <div className="inc-modal-overlay">
            <div className="inc-modal-content">
              <div className="inc-modal-header">
                <h3>Escalate Incident to Executive Administration</h3>
                <button
                  type="button"
                  className="inc-modal-close"
                  onClick={() => setShowEscalateModal(false)}
                >
                  ✕
                </button>
              </div>
              <form onSubmit={handleEscalate}>
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
                    <strong style={{ color: "#fda4af" }}>Notice:</strong> Escalating notifies administration and requests intervention for this incident.
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Reason for Escalation <span className="required">*</span>
                    </label>
                    <textarea
                      className="inc-form-control"
                      rows="4"
                      placeholder="Detail reasons why operational intervention or management elevation is required..."
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
                    onClick={() => setShowEscalateModal(false)}
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

        {/* MODAL: UC_16.5 Resolve Incident */}
        {showResolveModal && (
          <div className="inc-modal-overlay">
            <div className="inc-modal-content">
              <div className="inc-modal-header">
                <h3>Finalize Incident Resolution</h3>
                <button
                  type="button"
                  className="inc-modal-close"
                  onClick={() => setShowResolveModal(false)}
                >
                  ✕
                </button>
              </div>
              <form onSubmit={handleResolve}>
                <div className="inc-modal-body">
                  <div
                    style={{
                      background: "#11261d",
                      border: "1px solid #059669",
                      borderRadius: "6px",
                      padding: "12px 14px",
                      marginBottom: "16px",
                      color: "#a7f3d0",
                      fontSize: "13px",
                      lineHeight: "1.5",
                    }}
                  >
                    Recording resolution for incident <strong>#{incident.id}</strong>. The operational state will transition to <strong>Resolved</strong>.
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Resolution Summary & Root Cause Analysis <span className="required">*</span>
                    </label>
                    <textarea
                      className="inc-form-control"
                      rows="3"
                      placeholder="Document root cause findings, investigation outcome, and resolution assessment..."
                      value={resolveForm.resolutionSummary}
                      onChange={(e) =>
                        setResolveForm({ ...resolveForm, resolutionSummary: e.target.value })
                      }
                      required
                    />
                  </div>

                  <div className="inc-form-group">
                    <label className="inc-form-label">
                      Corrective Actions Executed <span className="required">*</span>
                    </label>
                    <textarea
                      className="inc-form-control"
                      rows="3"
                      placeholder="Detail countermeasures taken: crowd realigned, barrier reinforced, medical assistance concluded..."
                      value={resolveForm.actionTaken}
                      onChange={(e) =>
                        setResolveForm({ ...resolveForm, actionTaken: e.target.value })
                      }
                      required
                    />
                  </div>
                </div>

                <div className="inc-modal-footer">
                  <button
                    type="button"
                    className="inc-btn inc-btn-secondary"
                    onClick={() => setShowResolveModal(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="inc-btn inc-btn-success"
                    disabled={isResolving}
                  >
                    {isResolving ? "Submitting..." : "Confirm Resolution"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Lightbox Preview */}
        {lightboxMedia && (
          <div
            className="inc-lightbox-overlay"
            onClick={() => setLightboxMedia(null)}
          >
            <div
              className="inc-lightbox-content"
              onClick={(e) => e.stopPropagation()}
            >
              <button
                type="button"
                style={{
                  position: "absolute",
                  top: "-40px",
                  right: "0",
                  background: "rgba(255, 255, 255, 0.1)",
                  border: "1px solid rgba(255, 255, 255, 0.2)",
                  borderRadius: "8px",
                  color: "#fff",
                  fontSize: "14px",
                  padding: "6px 14px",
                  cursor: "pointer",
                  backdropFilter: "blur(8px)",
                }}
                onClick={() => setLightboxMedia(null)}
              >
                ✕ Close Preview
              </button>
              {lightboxMedia.type === "image" ? (
                <img src={lightboxMedia.url} alt="Lightbox preview" />
              ) : (
                <video src={lightboxMedia.url} controls autoPlay />
              )}
            </div>
          </div>
        )}
      </div>
    </Shell>
  );
}
