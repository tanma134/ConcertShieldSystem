import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import checkinApi from "../../api/checkinApi";
import { useAuth } from "../../context/AuthContext";
import OfflineConflictModal from "../../components/OfflineConflictModal";
import "./RealtimeCheckinMonitorPage.css";

export default function RealtimeCheckinMonitorPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const { user, isAuthenticated, isAdmin, isOrganizer } = useAuth();

  const [activeEventId, setActiveEventId] = useState(eventId ? Number(eventId) : 1);
  const [eventList, setEventList] = useState([
    { id: 1, title: "Sơn Tùng M-TP Sky Tour 2026" },
    { id: 2, title: "BlackPink Born Pink World Tour Hanoi" },
    { id: 3, title: "Anh Trai Say Hi Live Concert 2026" },
  ]);
  const [showConflictModal, setShowConflictModal] = useState(false);
  const [pendingConflictsCount, setPendingConflictsCount] = useState(0);

  const [monitorData, setMonitorData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [unauthorized, setUnauthorized] = useState(false);

  // Filter States (BR-396)
  const [selectedGate, setSelectedGate] = useState("");
  const [selectedZone, setSelectedZone] = useState("");

  // Auto-refresh controls (BR-397)
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshIntervalSec, setRefreshIntervalSec] = useState(4);
  const [lastRefreshedAt, setLastRefreshedAt] = useState(null);
  const [refreshing, setRefreshing] = useState(false);

  const isInitialMount = useRef(true);

  useEffect(() => {
    if (eventId) {
      setActiveEventId(Number(eventId));
    }
  }, [eventId]);

  useEffect(() => {
    const loadEvents = async () => {
      try {
        const res = await checkinApi.getGovernanceEvents();
        if (res?.data?.data && res.data.data.length > 0) {
          setEventList(res.data.data.map((e) => ({ id: e.eventId, title: e.eventName })));
        }
      } catch (e) {
        // keep defaults
      }
    };
    loadEvents();
  }, []);

  const fetchPendingConflictsCount = useCallback(async () => {
    try {
      const res = await checkinApi.getOfflineConflicts({ eventId: Number(activeEventId), status: "PENDING" });
      if (res?.data?.data) {
        setPendingConflictsCount(res.data.data.length);
      }
    } catch (e) {
      // ignore
    }
  }, [activeEventId]);

  // Fetch telemetry from API
  const fetchMonitorData = useCallback(async (isManualTrigger = false) => {
    if (!activeEventId) return;
    try {
      if (isManualTrigger) setRefreshing(true);
      setError("");

      const params = {};
      if (selectedGate) params.gateId = selectedGate;
      if (selectedZone) params.zone = selectedZone;

      const response = await checkinApi.getMonitorStatus(activeEventId, params);
      const data = response.data?.data;

      if (data) {
        setMonitorData(data);
        setUnauthorized(false);
        setLastRefreshedAt(new Date());
      }
    } catch (err) {
      if (err.response?.status === 403) {
        setUnauthorized(true);
        setError(err.response?.data?.message || "Access Denied: You do not own this concert or lack permission (BR-398).");
      } else {
        const msg = err.response?.data?.message || "Server / Connection Error (EX-01). Retaining last valid telemetry state.";
        setError(msg);
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [activeEventId, selectedGate, selectedZone]);

  // Initial Load
  useEffect(() => {
    fetchMonitorData();
    fetchPendingConflictsCount();
  }, [fetchMonitorData, fetchPendingConflictsCount]);

  // Auto-refresh loop (BR-397)
  useEffect(() => {
    if (!autoRefresh || unauthorized) return;

    const intervalId = setInterval(() => {
      fetchMonitorData();
    }, refreshIntervalSec * 1000);

    return () => clearInterval(intervalId);
  }, [autoRefresh, refreshIntervalSec, unauthorized, fetchMonitorData]);

  // Handle Manual Refresh
  const handleManualRefresh = () => {
    fetchMonitorData(true);
  };

  if (loading && !monitorData) {
    return (
      <div className="rcm-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <div style={{ textAlign: "center" }}>
          <div className="rcm-pulse-dot" style={{ width: "24px", height: "24px", margin: "0 auto 16px" }}></div>
          <h2 style={{ fontSize: "18px", fontWeight: 700, color: "#94a3b8" }}>Connecting to Real-time Check-in Telemetry...</h2>
        </div>
      </div>
    );
  }

  if (unauthorized) {
    return (
      <div className="rcm-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <div className="rcm-card" style={{ maxWidth: "480px", textAlign: "center", padding: "40px 24px" }}>
          <h2 style={{ fontSize: "20px", fontWeight: 800, color: "#f87171", margin: "0 0 12px" }}>Access Denied (BR-398 / BR-61)</h2>
          <p style={{ fontSize: "14px", color: "#cbd5e1", lineHeight: 1.6, margin: "0 0 24px" }}>
            {error || "You do not have authorization to view the real-time check-in monitoring dashboard for this concert."}
          </p>
          <button className="rcm-btn rcm-btn-primary" onClick={() => navigate("/organizer/dashboard")}>
            Back to Organizer Dashboard
          </button>
        </div>
      </div>
    );
  }

  const {
    eventTitle = `Concert #${activeEventId}`,
    totalIssued = 0,
    checkedIn = 0,
    pending = 0,
    occupancyRate = 0,
    currentScanRatePerMin = 0,
    peakScanRatePerMin = 0,
    gates = [],
    zones = [],
    recentScans = []
  } = monitorData || {};

  return (
    <div className="rcm-container">
      {/* Header Bar */}
      <header className="rcm-header-bar">
        <div className="rcm-header-title">
          <h1>
            <span>{eventTitle}</span>
            <span className="rcm-live-badge">
              <span className="rcm-pulse-dot"></span>
              LIVE TELEMETRY
            </span>
          </h1>
          <div className="rcm-header-subtitle">
            Real-time Check-in Monitoring Dashboard • Event ID {activeEventId}
          </div>
        </div>

        <div className="rcm-controls">
          <select
            className="rcm-select"
            value={activeEventId}
            onChange={(e) => setActiveEventId(Number(e.target.value))}
            title="Select Event to Monitor"
          >
            {eventList.map((evt) => (
              <option key={evt.id} value={evt.id}>
                {evt.title} (ID: {evt.id})
              </option>
            ))}
          </select>

          <button
            className="rcm-btn"
            style={{
              background: pendingConflictsCount > 0 ? "rgba(245, 158, 11, 0.25)" : undefined,
              borderColor: pendingConflictsCount > 0 ? "#f59e0b" : undefined,
              color: pendingConflictsCount > 0 ? "#fbbf24" : undefined,
              fontWeight: 700,
            }}
            onClick={() => setShowConflictModal(true)}
            title="View & Resolve Offline Check-in Conflicts"
          >
            Offline Conflicts {pendingConflictsCount > 0 ? `(${pendingConflictsCount})` : ""}
          </button>
          <button
            className={`rcm-btn ${autoRefresh ? "rcm-btn-primary" : ""}`}
            onClick={() => setAutoRefresh(!autoRefresh)}
            title="Toggle automatic data push updates"
          >
            {autoRefresh ? "Live Push: ON" : "Live Push: OFF"}
          </button>

          <select
            className="rcm-select"
            value={refreshIntervalSec}
            onChange={(e) => setRefreshIntervalSec(Number(e.target.value))}
            title="Auto-refresh polling frequency"
          >
            <option value={3}>3 sec refresh</option>
            <option value={5}>5 sec refresh</option>
            <option value={10}>10 sec refresh</option>
          </select>

          <button className="rcm-btn" onClick={handleManualRefresh} disabled={refreshing}>
            {refreshing ? "Refreshing..." : "Refresh Now"}
          </button>

          <button className="rcm-btn" onClick={() => navigate("/organizer/dashboard")}>
            Dashboard
          </button>
        </div>
      </header>

      <main className="rcm-content">
        {error && (
          <div style={{ background: "rgba(239, 68, 68, 0.2)", border: "1px solid #f87171", color: "#f87171", padding: "12px 18px", borderRadius: "10px", marginBottom: "20px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span>{error}</span>
            <button onClick={() => setError("")} style={{ background: "none", border: "none", color: "#f87171", cursor: "pointer", fontWeight: 800 }}>Close</button>
          </div>
        )}

        {/* 4 CORE REAL-TIME METRICS (BR-395) */}
        <div className="rcm-metrics-grid">
          {/* Card 1: Total Issued */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>1. Total Tickets Issued</span>
            </div>
            <div className="rcm-metric-value">{totalIssued.toLocaleString()}</div>
            <div style={{ fontSize: "12px", color: "#94a3b8" }}>Total sold/issued tickets</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-purple" style={{ width: "100%" }}></div>
            </div>
          </div>

          {/* Card 2: Checked-in */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>2. Checked-in</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#34d399" }}>{checkedIn.toLocaleString()}</div>
            <div style={{ fontSize: "12px", color: "#94a3b8" }}>Attendees passed gate</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-emerald" style={{ width: `${Math.min(100, occupancyRate)}%` }}></div>
            </div>
          </div>

          {/* Card 3: Pending */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>3. Pending Admission</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#fbbf24" }}>{pending.toLocaleString()}</div>
            <div style={{ fontSize: "12px", color: "#94a3b8" }}>Tickets yet to enter</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-amber" style={{ width: `${totalIssued > 0 ? (pending / totalIssued) * 100 : 0}%` }}></div>
            </div>
          </div>

          {/* Card 4: Occupancy Rate (%) */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>4. Occupancy Rate</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#c084fc" }}>{occupancyRate}%</div>
            <div style={{ fontSize: "12px", color: "#94a3b8" }}>(Checked-in / Total Issued) * 100</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-pink" style={{ width: `${Math.min(100, occupancyRate)}%` }}></div>
            </div>
          </div>
        </div>

        {/* Scan Velocity Banner */}
        <div className="rcm-telemetry-banner">
          <div className="rcm-telemetry-item">
            <div className="rcm-telemetry-text">
              <div>Current Velocity</div>
              <div>{currentScanRatePerMin} scans / min</div>
            </div>
          </div>

          <div className="rcm-telemetry-item">
            <div className="rcm-telemetry-text">
              <div>Peak Rate Observed</div>
              <div>{peakScanRatePerMin} scans / min</div>
            </div>
          </div>

          <div className="rcm-telemetry-item">
            <div className="rcm-telemetry-text">
              <div>Last Synced At</div>
              <div>{lastRefreshedAt ? lastRefreshedAt.toLocaleTimeString() : "--:--:--"}</div>
            </div>
          </div>
        </div>

        {/* BREAKDOWN SECTIONS (BR-396) */}
        <div className="rcm-breakdown-grid">
          {/* Gate Breakdown */}
          <div className="rcm-card">
            <div className="rcm-card-header">
              <div className="rcm-card-title">
                <span>Gate Admission Breakdown</span>
              </div>
              <select
                className="rcm-select"
                value={selectedGate}
                onChange={(e) => setSelectedGate(e.target.value)}
              >
                <option value="">All Gates</option>
                {gates.map((g) => (
                  <option key={g.gateId} value={g.gateId}>{g.gateName}</option>
                ))}
              </select>
            </div>

            {gates.length === 0 ? (
              <div style={{ color: "#94a3b8", textAlign: "center", padding: "20px" }}>No gate data available.</div>
            ) : (
              gates.map((g) => (
                <div key={g.gateId} className="rcm-gate-item">
                  <div className="rcm-gate-top">
                    <span className="rcm-gate-name">{g.gateName}</span>
                    <span className={`rcm-gate-badge status-${g.status.toLowerCase()}`}>
                      {g.status} ({g.scanRatePerMin} / min)
                    </span>
                  </div>
                  <div className="rcm-gate-stats">
                    <span>Checked-in: <strong>{g.checkedIn}</strong> / {g.totalAssigned}</span>
                    <span>Occupancy: <strong>{g.occupancyRate}%</strong></span>
                  </div>
                  <div className="rcm-progress-bar-bg">
                    <div className="rcm-progress-bar-fill fill-emerald" style={{ width: `${Math.min(100, g.occupancyRate)}%` }}></div>
                  </div>
                </div>
              ))
            )}
          </div>

          {/* Ticket Tier / Seating Zone Breakdown */}
          <div className="rcm-card">
            <div className="rcm-card-header">
              <div className="rcm-card-title">
                <span>Ticket Tier & Seating Zone Breakdown</span>
              </div>
              <select
                className="rcm-select"
                value={selectedZone}
                onChange={(e) => setSelectedZone(e.target.value)}
              >
                <option value="">All Zones</option>
                {zones.map((z) => (
                  <option key={z.zoneName} value={z.zoneName}>{z.zoneName}</option>
                ))}
              </select>
            </div>

            {zones.length === 0 ? (
              <div style={{ color: "#94a3b8", textAlign: "center", padding: "20px" }}>No zone breakdown available.</div>
            ) : (
              zones.map((z) => (
                <div key={z.zoneName} className="rcm-zone-item">
                  <div className="rcm-gate-top">
                    <span className="rcm-gate-name">{z.zoneName}</span>
                    <span style={{ fontSize: "12px", color: "#c084fc", fontWeight: 700 }}>
                      {z.checkedIn} / {z.totalIssued} checked in
                    </span>
                  </div>
                  <div className="rcm-gate-stats">
                    <span>Pending: <strong>{z.pending}</strong></span>
                    <span>Occupancy Rate: <strong>{z.occupancyRate}%</strong></span>
                  </div>
                  <div className="rcm-progress-bar-bg">
                    <div className="rcm-progress-bar-fill fill-purple" style={{ width: `${Math.min(100, z.occupancyRate)}%` }}></div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Live Recent Scan Log Stream */}
        <div className="rcm-card">
          <div className="rcm-card-header">
            <div className="rcm-card-title">
              <span>Recent Scan Telemetry Stream</span>
            </div>
            <span style={{ fontSize: "12px", color: "#94a3b8" }}>Showing latest entrance scans</span>
          </div>

          <div className="rcm-table-wrap">
            <table className="rcm-table">
              <thead>
                <tr>
                  <th>Scan ID</th>
                  <th>Ticket Code</th>
                  <th>Customer Name</th>
                  <th>Seating / Zone</th>
                  <th>Entrance Gate</th>
                  <th>Timestamp</th>
                  <th>Method</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {recentScans.length === 0 ? (
                  <tr>
                    <td colSpan="8" style={{ textAlign: "center", padding: "20px", color: "#94a3b8" }}>
                      No scan events logged yet.
                    </td>
                  </tr>
                ) : (
                  recentScans.map((s) => (
                    <tr key={s.scanId}>
                      <td style={{ fontFamily: "monospace", color: "#c084fc" }}>{s.scanId}</td>
                      <td style={{ fontWeight: 700 }}>{s.ticketCode}</td>
                      <td>{s.customerName}</td>
                      <td>{s.zoneName}</td>
                      <td>{s.gateName}</td>
                      <td style={{ color: "#94a3b8" }}>{new Date(s.scannedAt).toLocaleTimeString()}</td>
                      <td>{s.scanMethod}</td>
                      <td>
                        <span className={`rcm-status-tag tag-${s.status.toLowerCase()}`}>
                          {s.status}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </main>

      <OfflineConflictModal
        isOpen={showConflictModal}
        onClose={() => {
          setShowConflictModal(false);
          fetchPendingConflictsCount();
        }}
        eventId={activeEventId}
        onConflictResolved={() => {
          fetchPendingConflictsCount();
          fetchMonitorData(true);
        }}
      />
    </div>
  );
}
