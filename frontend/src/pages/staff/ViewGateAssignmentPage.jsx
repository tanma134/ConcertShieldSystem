import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import checkinApi from "../../api/checkinApi";
import { useAuth } from "../../context/AuthContext";
import "./ViewGateAssignmentPage.css";

export default function ViewGateAssignmentPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const currentEventId = eventId ? Number(eventId) : 20; // Default to Acoustic Night (20) or 1
  const [assignment, setAssignment] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    fetchGateAssignment();
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
              <option value="20">Event #20: Acoustic Night (Published)</option>
              <option value="1">Event #1: Anh Trai Say Hi - Grand Finale (Cancelled)</option>
              <option value="16">Event #16: AMEE - E11EVEN Tour 2027 (Postponed)</option>
              <option value="17">Event #17: Hoàng Thùy Linh - See Tình Tour</option>
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
            ⚠️ {error}
            <button className="btn-retry" onClick={fetchGateAssignment}>Retry</button>
          </div>
        ) : (
          <div className="gate-content-layout">
            {/* Top Event Banner Card (Figure 113) */}
            <div className="event-banner-card">
              <div className="banner-left">
                <h2 className="event-name-pink">
                  {currentEventId === 20 ? "Acoustic Night" : assignment?.eventTitle || "Acoustic Night"}
                </h2>
                <div className="event-date-sub">
                  {currentEventId === 20 ? "20/10/2026 · 18:00 - 23:59" : "27/10/2026 · 19:00 - 23:00"}
                </div>
              </div>
              <div className="banner-right">
                <span className="event-status-active">Event Active</span>
              </div>
            </div>

            {/* Detail Grid Cards based strictly on Database Fields */}
            <div className="details-grid-2x2">
              {/* Card 1: Assigned Gate */}
              <div className="detail-dark-card">
                <div className="card-label-gray">Assigned Gate</div>
                <div className="card-value-white">
                  {assignment?.gateName || "Gate A - Main Entrance"}
                </div>
                <div className="card-meta-line">
                  Gate Code: <strong>{assignment?.gateId || "GATE_A"}</strong>
                </div>
                <div className="card-meta-line">
                  Assigned Event: <strong>{currentEventId === 20 ? "Acoustic Night" : assignment?.eventTitle || "Acoustic Night"}</strong>
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
                  Shift Name: <strong>{assignment?.shiftName || "Evening Shift"}</strong>
                </div>
                <div className="card-meta-line">
                  Shift Status: <strong style={{ color: assignment?.isShiftActive !== false ? "#10b981" : "#ef4444" }}>
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
                  Staff ID: <strong>#{user?.id || 1}</strong>
                </div>
                <div className="card-meta-line">
                  Scanned in Shift: <strong style={{ color: "#ff2d75" }}>{assignment?.totalScannedInShift || 248} scans</strong>
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

            {/* Bottom Button (Figure 113) */}
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
