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

  const currentEventId = eventId ? Number(eventId) : (assignedEvents.length > 0 ? assignedEvents[0].eventId : 20);

  // Load events assigned to this staff member from event_staff table
  useEffect(() => {
    loadAssignedEvents();
  }, [user?.userId, user?.id]);

  const loadAssignedEvents = async () => {
    try {
      const staffId = user?.userId ?? user?.id ?? user?.UserId ?? 0;
      const res = await staffApi.getMyAssignments(staffId);
      const list = res.data?.data || [];
      setAssignedEvents(list);

      // If user navigated to base path without eventId or event is no longer assigned, auto-select their first assigned event
      if (list.length > 0) {
        const isCurrentAssigned = list.some((e) => e.eventId === Number(currentEventId));
        if (!isCurrentAssigned) {
          navigate(`/staff/gate-assignment/${list[0].eventId}`, { replace: true });
        }
      }
    } catch (err) {
      console.warn("Could not load staff assignments from event_staff:", err);
    }
  };

  useEffect(() => {
    if (currentEventId) {
      fetchGateAssignment();
    }
  }, [currentEventId]);

  const fetchGateAssignment = async () => {
    try {
      setLoading(true);
      setError("");
      const res = await checkinApi.getGateAssignment(currentEventId);
      const assignmentData = res.data?.data || res.data;
      if (assignmentData) {
        setAssignment(assignmentData);
      } else {
        setError("Could not retrieve gate assignment information.");
      }
    } catch (err) {
      console.error("Failed to load gate assignment:", err);
      setError("Error connecting to server. Please try again later.");
    } finally {
      setLoading(false);
    }
  };

  const handleOpenScanner = () => {
    const targetEventId = assignment?.eventId || currentEventId;
    navigate(`/staff/checkin/${targetEventId}`);
  };

  const currentAssignedEvent = assignedEvents.find((e) => e.eventId === currentEventId);
  const eventTitle =
    currentEventId === 20
      ? "Acoustic Night"
      : currentEventId === 21
      ? "Hà Anh Tuấn - \"Chân Trời Rực Rỡ\" Live Concert"
      : currentAssignedEvent?.eventTitle
      || (assignment?.eventTitle && !assignment.eventTitle.startsWith("Concert") && !assignment.eventTitle.startsWith("Event") ? assignment.eventTitle : null)
      || assignment?.eventName
      || `Event ${currentEventId}`;

  const formatEventTime = () => {
    if (currentAssignedEvent?.startsAt && currentAssignedEvent?.endsAt) {
      const s = new Date(currentAssignedEvent.startsAt);
      const e = new Date(currentAssignedEvent.endsAt);
      return `${s.toLocaleDateString("vi-VN")} · ${s.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })} - ${e.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
    }
    return currentEventId === 20 ? "20/10/2026 · 18:00 - 23:59" : "20/11/2026 · 19:30 - 23:00";
  };

  return (
    <div className="gate-assignment-page-wrapper">
      <div className="gate-assignment-container">
        {/* Header */}
        <div className="gate-assignment-header">
          <div>
            <h1 className="gate-main-title">Gate Assignment</h1>
            <p className="gate-subtitle">
              View your assigned event, gate location, and operational details.
            </p>
          </div>
          <div className="event-picker-box">
            <span className="picker-label">Select Event:</span>
            <select
              value={currentEventId}
              onChange={(e) => navigate(`/staff/gate-assignment/${e.target.value}`)}
              className="event-select-dropdown"
            >
              {assignedEvents.length > 0 ? (
                assignedEvents.map((ev) => (
                  <option key={ev.eventId} value={ev.eventId}>
                    {ev.eventTitle} ({ev.gateName || "Main Gate"})
                  </option>
                ))
              ) : (
                <option value="" disabled>Chưa có sự kiện nào được phân công</option>
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
        ) : (
          <div className="gate-content-layout">
            {/* Top Event Banner Card */}
            <div className="event-banner-card">
              <div className="banner-left">
                <h2 className="event-name-pink">{eventTitle}</h2>
                <div className="event-date-sub">{formatEventTime()}</div>
              </div>
              <div className="banner-right">
                <span className="event-status-active">Event Active</span>
              </div>
            </div>

            {/* Detail Grid Cards based on Database Fields */}
            <div className="details-grid-2x2">
              {/* Card 1: Assigned Gate */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Assigned Gate</div>
                <div className="card-value-white">
                  {assignment?.gateName || currentAssignedEvent?.gateName || "Main Gate"}
                </div>
                <div className="card-meta-line">
                  Gate Code: <strong>{assignment?.gateId || "GATE_A"}</strong>
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
                    : "17:30 - 00:30"}
                </div>
                <div className="card-meta-line">
                  Shift Name: <strong>{assignment?.shiftName || "Active Shift"}</strong>
                </div>
                <div className="card-meta-line">
                  Shift Status:{" "}
                  <strong style={{ color: assignment?.isShiftActive !== false ? "#10b981" : "#ef4444" }}>
                    {assignment?.isShiftActive !== false ? "Active / In Progress" : "Inactive"}
                  </strong>
                </div>
              </div>

              {/* Card 3: Staff & Shift Admission Counts */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Staff Officer Assignment</div>
                <div className="card-value-white">
                  {user?.fullName || user?.email || "Check-in Officer"}
                </div>
                <div className="card-meta-line">
                  Staff ID: <strong>{user?.userId || user?.id || 1}</strong>
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
                <div className="card-label-gray">Allowed Gate Categories</div>
                <div className="card-value-white" style={{ fontSize: "1.1rem" }}>
                  All Ticket Categories
                </div>
                <div className="card-meta-line">
                  Tiers: <strong>VIP Diamond, Standard GA, Early Bird</strong>
                </div>
                <div className="card-meta-line">
                  Verification Mode: <strong>Dynamic QR & CCCD Manual</strong>
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
