import { useEffect, useState, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import checkinApi from "../../api/checkinApi";
import eventApi from "../../api/eventApi";
import reportApi, { saveBlob } from "../../api/reportApi";
import { useAuth } from "../../context/AuthContext";
import OfflineConflictModal from "../../components/OfflineConflictModal";
import "./RealtimeCheckinMonitorPage.css";

export default function RealtimeCheckinMonitorPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const { user, isAuthenticated, isAdmin, isOrganizer } = useAuth();

  const [activeEventId, setActiveEventId] = useState(eventId ? Number(eventId) : null);
  const [eventList, setEventList] = useState([]);
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
  const [exporting, setExporting] = useState(false);

  const isInitialMount = useRef(true);

  useEffect(() => {
    if (eventId) {
      setActiveEventId(Number(eventId));
    }
  }, [eventId]);

  useEffect(() => {
    const loadEvents = async () => {
      try {
        let list = [];
        const cleanTitle = (t) =>
          String(t || "")
            .replace(/\s*-\s*offline\s*sync\s*demo/gi, "")
            .replace(/\s*offline\s*sync\s*demo/gi, "")
            .replace(/\s*offlinesync\s*demo/gi, "")
            .trim();

        if (isOrganizer) {
          const res = await eventApi.getMine();
          const data = res.data?.data || res.data || [];
          list = Array.isArray(data) ? data.map((e) => ({ id: e.eventId, title: cleanTitle(e.title) })) : [];
        } else if (isAdmin) {
          const res = await eventApi.getList({ pageSize: 100 });
          const items = res.data?.data?.items || res.data?.data || [];
          list = Array.isArray(items) ? items.map((e) => ({ id: e.eventId, title: cleanTitle(e.title) })) : [];
        } else {
          const res = await eventApi.getMine().catch(() => null);
          const data = res?.data?.data || [];
          list = Array.isArray(data) ? data.map((e) => ({ id: e.eventId, title: cleanTitle(e.title) })) : [];
        }

        if (list.length > 0) {
          setEventList(list);
          setActiveEventId((prev) => {
            if (prev && list.some((e) => e.id === Number(prev))) {
              return prev;
            }
            return list[0].id;
          });
        } else {
          setEventList([]);
          setLoading(false);
        }
      } catch (e) {
        console.warn("Could not load events for realtime check-in:", e);
        setEventList([]);
        setLoading(false);
      }
    };
    loadEvents();
  }, [isOrganizer, isAdmin]);

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

  // Export Check-in Report CSV (UC-14.2) — Exports structured Real-time Check-in Monitor data
  const handleExportReport = async () => {
    if (!activeEventId) return;
    setExporting(true);
    try {
      let d = monitorData;
      if (!d) {
        const res = await checkinApi.getMonitorStatus(activeEventId, {});
        d = res?.data?.data || {};
      }

      const esc = (val) => {
        const str = val == null ? "" : String(val);
        if (str.includes(",") || str.includes('"') || str.includes("\n")) {
          return `"${str.replace(/"/g, '""')}"`;
        }
        return str;
      };

      const exportedAt = new Date().toLocaleString("en-US");
      const gatesRows = Array.isArray(d.gates) ? d.gates : [];
      const zonesRows = Array.isArray(d.zones) ? d.zones : [];
      const scansRows = Array.isArray(d.recentScans) ? d.recentScans : [];

      const lines = [
        "=== 1. REAL-TIME CHECK-IN OVERVIEW ===",
        "Metric,Value",
        `Event ID,${esc(activeEventId)}`,
        `Event Title,${esc(d.eventTitle || `Concert #${activeEventId}`)}`,
        `Exported At,${esc(exportedAt)}`,
        `Total Tickets Issued,${d.totalIssued ?? 0}`,
        `Checked-in Tickets (Used),${d.checkedIn ?? 0}`,
        `Total / Used Ratio,${esc(`${d.totalIssued ?? 0} / ${d.checkedIn ?? 0}`)}`,
        `Pending Admission,${d.pending ?? 0}`,
        `Occupancy Rate (%),${esc(`${d.occupancyRate ?? 0}%`)}`,
        "",
        "=== 2. GATE ADMISSION BREAKDOWN ===",
        "Gate ID,Gate Name,Total Assigned,Checked-in (Used),Pending,Occupancy Rate (%),Gate Status",
        ...(gatesRows.length > 0
          ? gatesRows.map(
              (g) =>
                `${esc(g.gateId)},${esc(g.gateName)},${g.totalAssigned ?? 0},${g.checkedIn ?? 0},${g.pending ?? Math.max(0, (g.totalAssigned ?? 0) - (g.checkedIn ?? 0))},${esc(`${g.occupancyRate ?? 0}%`)},${esc(g.status || "Normal")}`
            )
          : ["No gate data available,,,,"]),
        "",
        "=== 3. TICKET TIER & SEATING ZONE BREAKDOWN ===",
        "Ticket Tier / Zone Name,Total Issued,Checked-in (Used),Pending,Occupancy Rate (%)",
        ...(zonesRows.length > 0
          ? zonesRows.map(
              (z) =>
                `${esc(z.zoneName)},${z.totalIssued ?? 0},${z.checkedIn ?? 0},${z.pending ?? Math.max(0, (z.totalIssued ?? 0) - (z.checkedIn ?? 0))},${esc(`${z.occupancyRate ?? 0}%`)}`
            )
          : ["No zone data available,,,,"]),
        "",
        "=== 4. RECENT SCAN TELEMETRY STREAM (SCAN HISTORY) ===",
        "Scan ID,Attendee Name,Ticket Tier / Zone,Entrance Gate,Scan Time,Scan Method,Status",
        ...(scansRows.length > 0
          ? scansRows.map(
              (s) =>
                `${esc(s.scanId)},${esc(s.customerName)},${esc(s.zoneName)},${esc(s.gateName)},${esc(s.scannedAt ? new Date(s.scannedAt).toLocaleString("en-US") : "")},${esc(s.scanMethod)},${esc(s.status)}`
            )
          : ["No scan events logged yet,,,,,,"]),
      ];

      const csvContent = "\uFEFF" + lines.join("\r\n");
      const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
      saveBlob({ data: blob, headers: {} }, `realtime-checkin-event-${activeEventId}.csv`);
    } finally {
      setExporting(false);
    }
  };

  if (!loading && eventList.length === 0) {
    return (
      <div className="rcm-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <div className="rcm-card" style={{ maxWidth: "480px", textAlign: "center", padding: "40px 24px" }}>
          <h2 style={{ fontSize: "20px", fontWeight: 800, color: "#38bdf8", margin: "0 0 12px" }}>No Events Available</h2>
          <p style={{ fontSize: "14px", color: "#cbd5e1", lineHeight: 1.6, margin: "0 0 24px" }}>
            You do not have any events created yet to monitor live check-in. Please create an event and publish tickets to use this feature.
          </p>
          <div style={{ display: "flex", gap: "12px", justifyContent: "center" }}>
            <button className="rcm-btn rcm-btn-primary" onClick={() => navigate("/organizer/events/new")}>
              + Create New Concert
            </button>
            <button className="rcm-btn" onClick={() => navigate("/organizer/events")}>
              My Concerts
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (loading && !monitorData) {
    return (
      <div className="rcm-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <div style={{ textAlign: "center" }}>
          <div className="rcm-pulse-dot" style={{ width: "24px", height: "24px", margin: "0 auto 16px" }}></div>
          <span className="app-loading-spinner" role="status" aria-label="Loading" />
        </div>
      </div>
    );
  }

  if (unauthorized) {
    return (
      <div className="rcm-container" style={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100vh" }}>
        <div className="rcm-card" style={{ maxWidth: "480px", textAlign: "center", padding: "40px 24px" }}>
          <h2 style={{ fontSize: "20px", fontWeight: 800, color: "#d64545", margin: "0 0 12px" }}>Access Denied (BR-398 / BR-61)</h2>
          <p style={{ fontSize: "14px", color: "#5f587a", lineHeight: 1.6, margin: "0 0 24px" }}>
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
    <OrganizerShell title="Check-in Live Monitor">
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
              Live Check-in Attendance & Gate Operations Telemetry
            </div>
          </div>

          <div className="rcm-controls">
            <select
              className="rcm-select"
              value={activeEventId || ""}
              onChange={(e) => {
                const newId = Number(e.target.value);
                setActiveEventId(newId);
                navigate(`/organizer/events/${newId}/realtime-checkin`);
              }}
              title="Select Event to Monitor"
            >
              {eventList.map((evt) => (
                <option key={evt.id} value={evt.id}>
                  {evt.title}
                </option>
              ))}
            </select>

          <button
            className="rcm-btn"
            onClick={handleExportReport}
            disabled={exporting || !activeEventId}
            title="Export Check-in Report as CSV"
          >
            {exporting ? "Exporting..." : "Export Check-in Report"}
          </button>

          <button
            className="rcm-btn"
            style={{
              background: pendingConflictsCount > 0 ? "rgba(245, 158, 11, 0.25)" : undefined,
              borderColor: pendingConflictsCount > 0 ? "#d98f14" : undefined,
              color: pendingConflictsCount > 0 ? "#d99a16" : undefined,
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
            {refreshing ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Refresh Now"}
          </button>

          <button className="rcm-btn" onClick={() => navigate("/organizer/dashboard")}>
            Dashboard
          </button>
        </div>
      </header>

      <main className="rcm-content">
        {error && (
          <div style={{ background: "rgba(239, 68, 68, 0.2)", border: "1px solid #d64545", color: "#d64545", padding: "12px 18px", borderRadius: "10px", marginBottom: "20px", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span>{error}</span>
            <button onClick={() => setError("")} style={{ background: "none", border: "none", color: "#d64545", cursor: "pointer", fontWeight: 800 }}>Close</button>
          </div>
        )}

        {/* 4 CORE REAL-TIME METRICS (BR-395) */}
        <div className="rcm-metrics-grid">
          {/* Card 1: Total Issued / Used */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>1. Total Tickets Issued</span>
            </div>
            <div className="rcm-metric-value">
              {totalIssued.toLocaleString()} / {checkedIn.toLocaleString()}
            </div>
            <div style={{ fontSize: "12px", color: "#7a7392" }}>Total Tickets / Used Tickets</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-purple" style={{ width: `${totalIssued > 0 ? Math.min(100, (checkedIn / totalIssued) * 100) : 0}%` }}></div>
            </div>
          </div>

          {/* Card 2: Checked-in */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>2. Checked-in</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#21a87b" }}>{checkedIn.toLocaleString()}</div>
            <div style={{ fontSize: "12px", color: "#7a7392" }}>Attendees passed gate</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-emerald" style={{ width: `${Math.min(100, occupancyRate)}%` }}></div>
            </div>
          </div>

          {/* Card 3: Pending */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>3. Pending Admission</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#d99a16" }}>{pending.toLocaleString()}</div>
            <div style={{ fontSize: "12px", color: "#7a7392" }}>Tickets yet to enter</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-amber" style={{ width: `${totalIssued > 0 ? (pending / totalIssued) * 100 : 0}%` }}></div>
            </div>
          </div>

          {/* Card 4: Occupancy Rate (%) */}
          <div className="rcm-metric-card">
            <div className="rcm-metric-label">
              <span>4. Occupancy Rate</span>
            </div>
            <div className="rcm-metric-value" style={{ color: "#a855f7" }}>{occupancyRate}%</div>
            <div style={{ fontSize: "12px", color: "#7a7392" }}>(Checked-in / Total Issued) * 100</div>
            <div className="rcm-progress-bar-bg">
              <div className="rcm-progress-bar-fill fill-pink" style={{ width: `${Math.min(100, occupancyRate)}%` }}></div>
            </div>
          </div>
        </div>

        {/* Telemetry Status Bar */}
        <div className="rcm-telemetry-banner">
          <div className="rcm-telemetry-item">
            <span className="rcm-pulse-dot" style={{ width: "10px", height: "10px" }}></span>
            <div className="rcm-telemetry-text">
              <div>Telemetry Status</div>
              <div>Connected &amp; Live Synchronized</div>
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
              <div style={{ color: "#7a7392", textAlign: "center", padding: "20px" }}>No gate data available.</div>
            ) : (
              gates.map((g) => (
                <div key={g.gateId} className="rcm-gate-item">
                  <div className="rcm-gate-top">
                    <span className="rcm-gate-name">{g.gateName}</span>
                    <span className={`rcm-gate-badge status-${g.status.toLowerCase()}`}>
                      {g.status}
                    </span>
                  </div>
                  <div className="rcm-gate-stats">
                    <span>Total / Used: <strong>{g.totalAssigned} / {g.checkedIn}</strong></span>
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
              <div style={{ color: "#7a7392", textAlign: "center", padding: "20px" }}>No zone breakdown available.</div>
            ) : (
              zones.map((z) => (
                <div key={z.zoneName} className="rcm-zone-item">
                  <div className="rcm-gate-top">
                    <span className="rcm-gate-name">{z.zoneName}</span>
                    <span style={{ fontSize: "12px", color: "#a855f7", fontWeight: 700 }}>
                      {z.totalIssued} / {z.checkedIn} (Total / Used)
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
            <span style={{ fontSize: "12px", color: "#7a7392" }}>Showing latest entrance scans</span>
          </div>

          <div className="rcm-table-wrap">
            <table className="rcm-table">
              <thead>
                <tr>
                  <th>Scan ID</th>
                  <th>Attendee Name</th>
                  <th>Ticket Tier / Zone</th>
                  <th>Entrance Gate</th>
                  <th>Scan Time</th>
                  <th>Method</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {recentScans.length === 0 ? (
                  <tr>
                    <td colSpan="7" style={{ textAlign: "center", padding: "20px", color: "#7a7392" }}>
                      No scan events logged yet.
                    </td>
                  </tr>
                ) : (
                  recentScans.map((s) => (
                    <tr key={s.scanId}>
                      <td style={{ fontFamily: "monospace", color: "#a855f7" }}>{s.scanId}</td>
                      <td>{s.customerName}</td>
                      <td>{s.zoneName}</td>
                      <td>{s.gateName}</td>
                      <td style={{ color: "#7a7392" }}>{new Date(s.scannedAt).toLocaleTimeString()}</td>
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
        hideIncidentReport={true}
        onConflictResolved={() => {
          fetchPendingConflictsCount();
          fetchMonitorData(true);
        }}
      />
      </div>
    </OrganizerShell>
  );
}
