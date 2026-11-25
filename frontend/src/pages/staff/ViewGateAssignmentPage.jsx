import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import checkinApi from "../../api/checkinApi";
import staffApi from "../../api/staffApi";
import { useAuth } from "../../context/AuthContext";
import "./ViewGateAssignmentPage.css";

export default function ViewGateAssignmentPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [assignedEvents, setAssignedEvents] = useState([]);
  const [assignment, setAssignment] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const currentEventId = eventId ? Number(eventId) : (assignedEvents.length > 0 ? assignedEvents[0].eventId : null);

  // Load events assigned to this staff member from event_staff table
  useEffect(() => {
    loadAssignedEvents();
  }, [user?.userId, user?.id]);

  const loadAssignedEvents = async () => {
    try {
      setLoading(true);
      const staffId = user?.userId ?? user?.id ?? user?.UserId ?? 0;
      const res = await staffApi.getMyAssignments(staffId);
      const list = res.data?.data || [];
      setAssignedEvents(list);

      if (list.length > 0) {
        const isCurrentAssigned = list.some((e) => e.eventId === Number(currentEventId));
        if (!isCurrentAssigned && !eventId) {
          navigate(`/staff/gate-assignment/${list[0].eventId}`, { replace: true });
        }
      } else {
        setLoading(false);
      }
    } catch (err) {
      console.warn("Could not load staff assignments from event_staff:", err);
      setLoading(false);
    }
  };

  useEffect(() => {
    if (currentEventId) {
      fetchGateAssignment();
    } else {
      setAssignment(null);
      setLoading(false);
    }
  }, [currentEventId]);

  const fetchGateAssignment = async () => {
    if (!currentEventId) return;
    try {
      setLoading(true);
      setError("");
      const staffId = user?.userId ?? user?.id ?? user?.UserId ?? 0;
      const res = await checkinApi.getGateAssignment(currentEventId, staffId);
      const assignmentData = res.data?.data || res.data;
      if (assignmentData) {
        setAssignment(assignmentData);
      } else {
        setError("Could not retrieve gate assignment information.");
      }
    } catch (err) {
      console.error("Failed to load gate assignment:", err);
      setError("Unable to connect to server. Please try again later.");
    } finally {
      setLoading(false);
    }
  };

  const handleOpenScanner = () => {
    const targetEventId = assignment?.eventId || currentEventId;
    if (targetEventId) {
      navigate(`/staff/checkin/${targetEventId}`);
    }
  };

  const currentAssignedEvent = assignedEvents.find((e) => e.eventId === currentEventId);
  const eventTitle = currentAssignedEvent?.eventTitle
    || (assignment?.eventTitle && !assignment.eventTitle.startsWith("Concert") && !assignment.eventTitle.startsWith("Event") ? assignment.eventTitle : null)
    || assignment?.eventName
    || (currentEventId ? `Event #${currentEventId}` : "");

  const formatEventTime = () => {
    if (currentAssignedEvent?.startsAt && currentAssignedEvent?.endsAt) {
      const s = new Date(currentAssignedEvent.startsAt);
      const e = new Date(currentAssignedEvent.endsAt);
      return `${s.toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })} · ${s.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })} - ${e.toLocaleTimeString("en-US", { hour: "2-digit", minute: "2-digit" })}`;
    }
    return "";
  };

  const hasAssignment = assignedEvents.length > 0 || assignment?.isAssigned;

  return (
    <div className="gate-assignment-page-wrapper">
      <div className="gate-assignment-container">
        {/* Header */}
        <div className="gate-assignment-header">
          <div>
            <h1 className="gate-main-title">Gate Assignment & Shift Schedule</h1>
            <p className="gate-subtitle">
              View your assigned concert, physical gate location, and shift operational schedule.
            </p>
          </div>
          <div className="event-picker-box">
            <span className="picker-label">Select Event:</span>
            <select
              value={currentEventId || ""}
              onChange={(e) => navigate(`/staff/gate-assignment/${e.target.value}`)}
              className="event-select-dropdown"
              disabled={assignedEvents.length === 0}
            >
              {assignedEvents.length > 0 ? (
                assignedEvents.map((ev) => (
                  <option key={ev.eventId} value={ev.eventId}>
                    {ev.eventTitle} ({ev.gateName || "Main Gate"})
                  </option>
                ))
              ) : (
                <option value="">No events assigned</option>
              )}
            </select>
          </div>
        </div>

        {loading ? (
          <div className="gate-loading-card">
            <div className="gate-spinner"></div>
            <p>Loading gate assignment parameters...</p>
          </div>
        ) : error ? (
          <div className="gate-error-banner">
            {error}
            <button className="btn-retry" onClick={fetchGateAssignment}>Retry</button>
          </div>
        ) : !hasAssignment ? (
          /* Empty State when Staff is Not Assigned to Any Event */
          <div style={{ textAlign: "center", padding: "56px 24px", background: "#18181b", borderRadius: "14px", border: "1px dashed #3f3f46", margin: "24px 0" }}>
            <h2 style={{ color: "#ffffff", fontSize: "1.35rem", fontWeight: 700, marginBottom: "8px" }}>
              No Event Assigned Yet
            </h2>
            <p style={{ color: "#9ca3af", maxWidth: "520px", margin: "0 auto", fontSize: "0.95rem", lineHeight: "1.6" }}>
              Your staff account has not been assigned to any concert or admission gate yet. Please contact your Event Organizer to be assigned to a shift schedule and gate.
            </p>
          </div>
        ) : (
          <div className="gate-content-layout">
            {/* Top Event Banner Card */}
            <div className="event-banner-card">
              <div className="banner-left">
                <h2 className="event-name-pink">{eventTitle}</h2>
                {formatEventTime() && <div className="event-date-sub">{formatEventTime()}</div>}
              </div>
              <div className="banner-right">
                <span className="event-status-active">
                  Assigned Event
                </span>
              </div>
            </div>

            {/* Detail Grid Cards based on Database Fields */}
            <div className="details-grid-2x2">
              {/* Card 1: Assigned Gate */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Assigned Gate</div>
                <div className="card-value-white">
                  {assignment?.gateName || currentAssignedEvent?.gateName || "Unassigned"}
                </div>
                <div className="card-meta-line">
                  Assigned Event: <strong>{eventTitle}</strong>
                </div>
              </div>

              {/* Card 2: Shift & Operational Hours */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Shift Operational Hours</div>
                <div className="card-value-white">
                  {assignment?.shiftStartTime && assignment?.shiftEndTime
                    ? `${assignment.shiftStartTime} - ${assignment.shiftEndTime}`
                    : (currentAssignedEvent?.shiftStartTime && currentAssignedEvent?.shiftEndTime)
                    ? `${currentAssignedEvent.shiftStartTime} - ${currentAssignedEvent.shiftEndTime}`
                    : "All Day / Flexible"}
                </div>
                <div className="card-meta-line">
                  Shift Date: <strong>{assignment?.shiftDate ? new Date(assignment.shiftDate).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" }) : (currentAssignedEvent?.shiftDate || "All Event Dates")}</strong>
                </div>
                <div className="card-meta-line">
                  Shift Status:{" "}
                  <strong style={{ color: "#10b981" }}>
                    Active / Ready for Check-in
                  </strong>
                </div>
              </div>

              {/* Card 3: Staff Officer Assignment */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Staff Member</div>
                <div className="card-value-white">
                  {user?.fullName || user?.email || "Check-in Staff"}
                </div>
                <div className="card-meta-line">
                  Email: <strong>{user?.email || "N/A"}</strong>
                </div>
                <div className="card-meta-line">
                  Scanned in Shift:{" "}
                  <strong style={{ color: "#ff2d75" }}>
                    {assignment?.totalScannedInShift ?? 0} scans
                  </strong>
                </div>
              </div>

              {/* Card 4: Allowed Ticket Categories */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Permitted Admission Tiers</div>
                <div className="card-value-white" style={{ fontSize: "1.1rem", color: "#38bdf8" }}>
                  {assignment?.allowedTicketTiers && assignment.allowedTicketTiers.length > 0
                    ? assignment.allowedTicketTiers.join(", ")
                    : "All Ticket Types (Open Gate)"}
                </div>
                <div className="card-meta-line">
                  Gate Rule:{" "}
                  <strong style={{ color: "#facc15" }}>
                    {assignment?.allowedTicketTiers && assignment.allowedTicketTiers.length > 0
                      ? `Gate accepts ticket tier(s): ${assignment.allowedTicketTiers.join(", ")}`
                      : "All valid ticket types accepted at this entrance"}
                  </strong>
                </div>
              </div>
            </div>

            {/* Assigned Roles & Duties for this Event */}
            <div style={{ marginTop: "20px", background: "#1c1917", borderRadius: "12px", padding: "18px 20px", border: "1px solid #292524" }}>
              <div style={{ fontSize: "0.85rem", color: "#a8a29e", fontWeight: 700, marginBottom: "12px", textTransform: "uppercase", letterSpacing: "0.05em" }}>
                Your Assigned Duties & Operational Roles
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(210px, 1fr))", gap: "10px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "12px 14px", borderRadius: "8px", background: currentAssignedEvent?.canCheckin !== false ? "rgba(16, 185, 129, 0.1)" : "rgba(255, 255, 255, 0.03)", border: `1px solid ${currentAssignedEvent?.canCheckin !== false ? "#059669" : "#3f3f46"}` }}>
                  <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: currentAssignedEvent?.canCheckin !== false ? "#10b981" : "#6b7280" }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: "0.88rem", color: currentAssignedEvent?.canCheckin !== false ? "#34d399" : "#9ca3af" }}>Gate Check-in</div>
                    <div style={{ fontSize: "0.76rem", color: "#a1a1aa" }}>{currentAssignedEvent?.canCheckin !== false ? `Assigned to ${currentAssignedEvent?.gateName || assignment?.gateName || "Main Gate"}` : "No check-in duty"}</div>
                  </div>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "12px 14px", borderRadius: "8px", background: currentAssignedEvent?.canReviewReturns ? "rgba(16, 185, 129, 0.1)" : "rgba(255, 255, 255, 0.03)", border: `1px solid ${currentAssignedEvent?.canReviewReturns ? "#059669" : "#3f3f46"}` }}>
                  <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: currentAssignedEvent?.canReviewReturns ? "#10b981" : "#6b7280" }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: "0.88rem", color: currentAssignedEvent?.canReviewReturns ? "#34d399" : "#9ca3af" }}>Return Review</div>
                    <div style={{ fontSize: "0.76rem", color: "#a1a1aa" }}>{currentAssignedEvent?.canReviewReturns ? "Review & Refund Allowed" : "No permission"}</div>
                  </div>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "12px 14px", borderRadius: "8px", background: currentAssignedEvent?.canManageIncidents ? "rgba(16, 185, 129, 0.1)" : "rgba(255, 255, 255, 0.03)", border: `1px solid ${currentAssignedEvent?.canManageIncidents ? "#059669" : "#3f3f46"}` }}>
                  <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: currentAssignedEvent?.canManageIncidents ? "#10b981" : "#6b7280" }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: "0.88rem", color: currentAssignedEvent?.canManageIncidents ? "#34d399" : "#9ca3af" }}>Incident Handling</div>
                    <div style={{ fontSize: "0.76rem", color: "#a1a1aa" }}>{currentAssignedEvent?.canManageIncidents ? "Report & Escalate Allowed" : "No permission"}</div>
                  </div>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "12px 14px", borderRadius: "8px", background: currentAssignedEvent?.canManageSupportCases ? "rgba(16, 185, 129, 0.1)" : "rgba(255, 255, 255, 0.03)", border: `1px solid ${currentAssignedEvent?.canManageSupportCases ? "#059669" : "#3f3f46"}` }}>
                  <div style={{ width: "8px", height: "8px", borderRadius: "50%", background: currentAssignedEvent?.canManageSupportCases ? "#10b981" : "#6b7280" }} />
                  <div>
                    <div style={{ fontWeight: 700, fontSize: "0.88rem", color: currentAssignedEvent?.canManageSupportCases ? "#34d399" : "#9ca3af" }}>Helpdesk Support</div>
                    <div style={{ fontSize: "0.76rem", color: "#a1a1aa" }}>{currentAssignedEvent?.canManageSupportCases ? "Customer Help Desk Allowed" : "No permission"}</div>
                  </div>
                </div>
              </div>
            </div>

            {/* Bottom Button */}
            <div className="gate-footer-action">
              <button className="btn-open-terminal-pink" onClick={handleOpenScanner}>
                Open Check-in Terminal
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
