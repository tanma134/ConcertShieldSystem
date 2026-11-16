import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import incidentApi from "../../api/incidentApi";
import eventApi from "../../api/eventApi";
import staffApi from "../../api/staffApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "./StaffShell";
import AdminShell from "../admin/AdminShell";
import "../../styles/incidentManagement.css";

const TrashIcon = () => (
  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="3 6 5 6 21 6" />
    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
  </svg>
);

/**
 * IncidentReportPage
 * Comprehensive implementation of on-site incident intake & evidence upload
 */
export default function IncidentReportPage() {
  const { user, isAdmin } = useAuth();
  const navigate = useNavigate();
  const toast = useToast();

  const [events, setEvents] = useState([]);
  const [assignments, setAssignments] = useState([]);
  const [selectedDropdownValue, setSelectedDropdownValue] = useState("");
  const [selectedEventId, setSelectedEventId] = useState("");
  const [customEventName, setCustomEventName] = useState("");
  const [title, setTitle] = useState("");
  const [category, setCategory] = useState("Security");
  const [severity, setSeverity] = useState("Medium");
  const [location, setLocation] = useState("");
  const [description, setDescription] = useState("");

  const [selectedFiles, setSelectedFiles] = useState([]);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [uploadProgressText, setUploadProgressText] = useState("");

  useEffect(() => {
    const initData = async () => {
      let staffAssigned = [];
      const staffId = user?.id || user?.userId || user?.UserId;
      if (!isAdmin && staffId) {
        try {
          const assignRes = await staffApi.getMyAssignments(staffId);
          const raw = assignRes.data?.data || assignRes.data;
          staffAssigned = Array.isArray(raw) ? raw : [];
          setAssignments(staffAssigned);
        } catch (err) {
          console.warn("Could not load staff assignments:", err);
        }
      }

      try {
        const res = await eventApi.getList({ pageSize: 50 });
        const raw = res.data?.data?.items || res.data?.items || res.data?.data || res.data;
        const list = Array.isArray(raw) ? raw : (Array.isArray(raw?.items) ? raw.items : []);
        setEvents(list);

        if (staffAssigned.length > 0) {
          const firstAssign = staffAssigned[0];
          const dropVal = `assign_${firstAssign.eventStaffId || firstAssign.eventId}`;
          setSelectedDropdownValue(dropVal);
          setSelectedEventId(String(firstAssign.eventId));
          if (firstAssign.gateName) {
            setLocation(firstAssign.gateName);
          }
        } else if (isAdmin && list.length > 0) {
          const firstEv = list[0];
          const firstId = String(firstEv.id || firstEv.eventId);
          setSelectedDropdownValue(`event_${firstId}`);
          setSelectedEventId(firstId);
        }
      } catch (err) {
        console.warn("Could not load event list from API:", err);
        setEvents([]);
      }
    };

    initData();
  }, [user, isAdmin]);

  const handleDropdownChange = (e) => {
    const val = e.target.value;
    setSelectedDropdownValue(val);

    if (val.startsWith("assign_")) {
      const targetKey = val.replace("assign_", "");
      const matched = assignments.find(
        (a) => String(a.eventStaffId || a.eventId) === targetKey
      );
      if (matched) {
        setSelectedEventId(String(matched.eventId));
        if (matched.gateName) {
          setLocation(matched.gateName);
        }
      }
    } else if (val.startsWith("event_")) {
      const targetId = val.replace("event_", "");
      setSelectedEventId(targetId);
      const matchedOld = assignments.find((a) => a.gateName === location);
      if (matchedOld) {
        setLocation("");
      }
    } else {
      setSelectedEventId(val);
    }
  };

  const handleFileChange = (e) => {
    const files = Array.from(e.target.files || []);
    if (!files.length) return;

    const newItems = files.map((file) => {
      const isImg = file.type.startsWith("image/");
      const isVid = file.type.startsWith("video/");
      return {
        file,
        caption: "",
        previewUrl: isImg || isVid ? URL.createObjectURL(file) : null,
        isImage: isImg,
        isVideo: isVid,
      };
    });

    setSelectedFiles((prev) => [...prev, ...newItems]);
  };

  const handleCaptionChange = (index, text) => {
    setSelectedFiles((prev) =>
      prev.map((item, idx) => (idx === index ? { ...item, caption: text } : item))
    );
  };

  const handleRemoveFile = (index) => {
    setSelectedFiles((prev) => {
      const item = prev[index];
      if (item.previewUrl) URL.revokeObjectURL(item.previewUrl);
      return prev.filter((_, idx) => idx !== index);
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!isAdmin && assignments.length === 0) {
      toast.error("You are not currently assigned to any concert. Please contact your organizer.");
      return;
    }

    if (!title.trim()) {
      toast.error("Please enter the incident title.");
      return;
    }
    if (!location.trim()) {
      toast.error("Please enter the on-site location.");
      return;
    }
    if (!description.trim()) {
      toast.error("Please provide a detailed description.");
      return;
    }

    let finalEventId = parseInt(selectedEventId, 10);
    let finalEventName = "";

    if (selectedDropdownValue.startsWith("assign_")) {
      const targetKey = selectedDropdownValue.replace("assign_", "");
      const matchedAssignment = assignments.find(
        (a) => String(a.eventStaffId || a.eventId) === targetKey
      );
      if (matchedAssignment) {
        finalEventId = matchedAssignment.eventId;
        finalEventName = matchedAssignment.eventTitle;
      }
    } else {
      const matchedEv = events.find((x) => (x.id || x.eventId) === finalEventId);
      if (matchedEv) {
        finalEventName = matchedEv.title || matchedEv.name || `Event #${finalEventId}`;
      } else {
        const fallbackAssignment = assignments.find((a) => a.eventId === finalEventId);
        finalEventName = fallbackAssignment?.eventTitle || (finalEventId ? `Event #${finalEventId}` : "On-site Event");
      }
    }

    if (!finalEventId || isNaN(finalEventId)) {
      finalEventId = 1;
      finalEventName = finalEventName || "On-site Event";
    }

    setIsSubmitting(true);
    setUploadProgressText("Creating incident report...");

    try {
      const reportPayload = {
        eventId: finalEventId,
        eventName: finalEventName,
        title: title.trim(),
        category,
        severity,
        location: location.trim(),
        description: description.trim(),
      };

      const createRes = await incidentApi.reportIncident(reportPayload);
      const createdIncident = createRes.data?.data || createRes.data;
      const incidentId = createdIncident.id;

      if (selectedFiles.length > 0) {
        for (let i = 0; i < selectedFiles.length; i++) {
          const item = selectedFiles[i];
          setUploadProgressText(
            `Uploading evidence ${i + 1}/${selectedFiles.length}: ${item.file.name}...`
          );
          try {
            await incidentApi.uploadEvidence(incidentId, item.file, item.caption);
          } catch (uploadErr) {
            console.error(`Error uploading file ${item.file.name}:`, uploadErr);
            toast.error(`Could not upload file ${item.file.name}`);
          }
        }
      }

      toast.success(`Incident #${incidentId} reported successfully!`);
      const redirectPath = isAdmin
        ? `/admin/incidents/${incidentId}`
        : `/staff/incidents/${incidentId}`;
      navigate(redirectPath);
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to create incident report.");
    } finally {
      setIsSubmitting(false);
      setUploadProgressText("");
    }
  };

  const Shell = isAdmin ? AdminShell : StaffShell;

  return (
    <Shell title="Report On-site Incident">
      <div className="inc-container">
        <div className="inc-header">
          <div className="inc-header-title">
            <div className="inc-dossier-pill">
              <span>Incident Intake</span>
            </div>
            <h1>Report On-site Incident</h1>
            <p>
              Log on-site operational anomalies, designate severity level, and upload evidence directly to Cloudinary
            </p>
          </div>
          <button
            type="button"
            className="inc-btn inc-btn-secondary"
            onClick={() => navigate(isAdmin ? "/admin/incidents" : "/staff/incidents")}
          >
            Back to Overview
          </button>
        </div>

        <form onSubmit={handleSubmit} style={{ maxWidth: "880px", margin: "0 auto" }}>
          <div className="inc-card">
            <div className="inc-card-title">
              <div className="inc-card-title-left">
                <span>Incident Particulars</span>
              </div>
              <span style={{ fontSize: "12px", color: "var(--inc-text-muted)" }}>
                Reporting Officer: <strong style={{ color: "#fff" }}>{user?.fullName || user?.email || "On-site Staff"}</strong>
              </span>
            </div>

            <div className="inc-form-group">
              <label className="inc-form-label">
                Associated Event <span className="required">*</span>
              </label>

              <select
                className="inc-form-control"
                value={selectedDropdownValue}
                onChange={handleDropdownChange}
                required
              >
                {!selectedDropdownValue && (
                  <option value="">-- Choose an Event --</option>
                )}

                {!isAdmin ? (
                  assignments.length > 0 ? (
                    assignments.map((a) => (
                      <option
                        key={`assign_${a.eventStaffId || a.eventId}`}
                        value={`assign_${a.eventStaffId || a.eventId}`}
                      >
                        {a.eventTitle} — Post: {a.gateName} ({a.city || a.locationName || "Venue"})
                      </option>
                    ))
                  ) : (
                    <option value="" disabled>No assigned shifts found for your staff account</option>
                  )
                ) : (
                  events.map((ev) => {
                    const evId = ev.id || ev.eventId;
                    return (
                      <option key={`event_${evId}`} value={`event_${evId}`}>
                        {ev.title || ev.name} ({ev.city || ev.locationName || ev.location || "Venue"})
                      </option>
                    );
                  })
                )}
              </select>

              {/* Assignment or Selection Details Info Bar */}
              {!isAdmin && assignments.length === 0 && (
                <div style={{ marginTop: "6px", fontSize: "12px", color: "#f87171" }}>
                  Note: You currently have no active concert assignments. An organizer must assign you to an event gate before you can report incidents.
                </div>
              )}

              {selectedDropdownValue.startsWith("assign_") && (
                (() => {
                  const targetKey = selectedDropdownValue.replace("assign_", "");
                  const matched = assignments.find((a) => String(a.eventStaffId || a.eventId) === targetKey);
                  if (!matched) return null;
                  return (
                    <div style={{ marginTop: "8px", fontSize: "12px", color: "var(--inc-text-secondary)", display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" }}>
                      <span className="inc-badge inc-status-investigating" style={{ fontSize: "11px", padding: "2px 8px" }}>
                        Assigned Duty
                      </span>
                      <span>
                        Assigned Post: <strong style={{ color: "#38bdf8" }}>{matched.gateName}</strong> • Venue: {matched.city || matched.locationName || "On-site Venue"}
                      </span>
                    </div>
                  );
                })()
              )}

              {isAdmin && selectedDropdownValue.startsWith("event_") && (
                <div style={{ marginTop: "6px", fontSize: "12px", color: "var(--inc-text-muted)" }}>
                  Event selected. Please verify or enter your specific on-site location below.
                </div>
              )}
            </div>

            <div className="inc-form-group">
              <label className="inc-form-label">
                Incident Summary / Title <span className="required">*</span>
              </label>
              <input
                type="text"
                className="inc-form-control"
                placeholder="E.g. Crowd congestion at Gate 3, Attendee collapsed near stage..."
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                required
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" }}>
              <div className="inc-form-group">
                <label className="inc-form-label">
                  Incident Category <span className="required">*</span>
                </label>
                <select
                  className="inc-form-control"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  required
                >
                  <option value="Security">Security</option>
                  <option value="Medical">Medical / First Aid</option>
                  <option value="Technical">Technical / AV & Stage</option>
                  <option value="CrowdControl">Crowd Control</option>
                  <option value="Facility">Facility & Barrier</option>
                  <option value="Other">Other Operational</option>
                </select>
              </div>

              <div className="inc-form-group">
                <label className="inc-form-label">
                  Severity Classification <span className="required">*</span>
                </label>
                <select
                  className="inc-form-control"
                  value={severity}
                  onChange={(e) => setSeverity(e.target.value)}
                  required
                >
                  <option value="Low">Low - Localized, resolved by post officer</option>
                  <option value="Medium">Medium - Multiple staff coordination needed</option>
                  <option value="High">High - Crowd flow / safety disruption risk</option>
                  <option value="Critical">Critical - Immediate life safety or emergency</option>
                </select>
              </div>
            </div>

            {severity === "Critical" && (
              <div
                style={{
                  background: "#2d1519",
                  border: "1px solid #e11d48",
                  borderRadius: "8px",
                  padding: "12px 16px",
                  marginBottom: "18px",
                  color: "#fecdd3",
                  fontSize: "13px",
                }}
              >
                <strong style={{ color: "#fda4af" }}>Critical Severity:</strong> Immediate escalation to executive administration is recommended.
              </div>
            )}

            <div className="inc-form-group">
              <label className="inc-form-label">
                On-site Location <span className="required">*</span>
              </label>
              <input
                type="text"
                className="inc-form-control"
                placeholder="E.g. Turnstile Gate B, East Bleachers, Restroom Concourse 2..."
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                required
              />
            </div>

            <div className="inc-form-group">
              <label className="inc-form-label">
                Detailed Log & Situation Assessment <span className="required">*</span>
              </label>
              <textarea
                className="inc-form-control"
                rows="4"
                placeholder="Document chronological sequence, persons involved, initial mitigation actions taken, and reinforcements needed..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                required
              />
            </div>
          </div>

          {/* Evidence Upload (UC_16.2) */}
          <div className="inc-card" style={{ marginTop: "20px" }}>
            <div className="inc-card-title">
              <div className="inc-card-title-left">
                <span>Evidence Vault Attachments</span>
              </div>
              <span style={{ fontSize: "11px", background: "#374151", color: "#e5e7eb", padding: "2px 8px", borderRadius: "4px", fontWeight: 600 }}>
                Cloudinary Storage
              </span>
            </div>

            <p style={{ fontSize: "13px", color: "var(--inc-text-secondary)", margin: "0 0 16px 0" }}>
              Upload photographic evidence, video clips, radio audio captures, or written incident logs. Assets will be preserved on secure Cloudinary storage.
            </p>

            <label className="inc-dropzone" htmlFor="evidence-file-input">
              <div className="inc-dropzone-title">Click or drag & drop evidence files here</div>
              <div className="inc-dropzone-sub">
                Supports JPG, PNG, WebP, MP4, MOV, MP3, WAV, and PDF formats
              </div>
              <input
                id="evidence-file-input"
                type="file"
                multiple
                accept="image/*,video/*,audio/*,application/pdf"
                style={{ display: "none" }}
                onChange={handleFileChange}
              />
            </label>

            {selectedFiles.length > 0 && (
              <div style={{ marginTop: "20px" }}>
                <h4 style={{ fontSize: "13px", color: "#fff", marginBottom: "12px", textTransform: "uppercase", letterSpacing: "0.06em", fontWeight: 700 }}>
                  Selected Files ({selectedFiles.length}):
                </h4>
                <div style={{ display: "flex", flexDirection: "column", gap: "10px" }}>
                  {selectedFiles.map((item, idx) => (
                    <div
                      key={idx}
                      style={{
                        background: "rgba(10, 14, 23, 0.7)",
                        border: "1px solid rgba(255, 255, 255, 0.08)",
                        borderRadius: "10px",
                        padding: "12px",
                        display: "flex",
                        gap: "14px",
                        alignItems: "center",
                      }}
                    >
                      {item.isImage && item.previewUrl ? (
                        <img
                          src={item.previewUrl}
                          alt="preview"
                          style={{ width: "56px", height: "56px", objectFit: "cover", borderRadius: "8px" }}
                        />
                      ) : item.isVideo && item.previewUrl ? (
                        <video
                          src={item.previewUrl}
                          style={{ width: "56px", height: "56px", objectFit: "cover", borderRadius: "8px" }}
                        />
                      ) : (
                        <div
                          style={{
                            width: "56px",
                            height: "56px",
                            background: "#06090e",
                            borderRadius: "8px",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            color: "var(--inc-text-muted)",
                          }}
                        >
                          <PaperclipIcon />
                        </div>
                      )}

                      <div style={{ flex: 1 }}>
                        <div style={{ fontSize: "13px", fontWeight: 700, color: "#fff" }}>
                          {item.file.name}
                        </div>
                        <div style={{ fontSize: "11px", color: "var(--inc-text-muted)", marginBottom: "6px" }}>
                          {(item.file.size / (1024 * 1024)).toFixed(2)} MB • {item.file.type || "file"}
                        </div>
                        <input
                          type="text"
                          className="inc-form-control"
                          style={{ padding: "6px 10px", fontSize: "12px" }}
                          placeholder="Add context / caption (e.g. Turnstile gate damage at 20:15)..."
                          value={item.caption}
                          onChange={(e) => handleCaptionChange(idx, e.target.value)}
                        />
                      </div>

                      <button
                        type="button"
                        className="inc-btn inc-btn-danger inc-btn-sm"
                        style={{ padding: "8px 12px" }}
                        onClick={() => handleRemoveFile(idx)}
                      >
                        <TrashIcon />
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              gap: "14px",
              marginTop: "24px",
              marginBottom: "40px",
            }}
          >
            <button
              type="button"
              className="inc-btn inc-btn-secondary"
              onClick={() => navigate(isAdmin ? "/admin/incidents" : "/staff/incidents")}
              disabled={isSubmitting}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="inc-btn inc-btn-primary"
              disabled={isSubmitting}
              style={{ minWidth: "180px" }}
            >
              {isSubmitting ? (uploadProgressText || "Filing Report...") : "Submit Incident Report"}
            </button>
          </div>
        </form>
      </div>
    </Shell>
  );
}
