import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import AdminShell from "./AdminShell";
import queueAdminApi from "../../api/queueAdminApi";
import "./AdminPages.css";

export default function QueueAbuseAlertsPage() {
  const [alerts, setAlerts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [severity, setSeverity] = useState("");
  const [status, setStatus] = useState("");

  const loadAlerts = useCallback(() => {
    queueAdminApi.getAlerts({ severity: severity || undefined, status: status || undefined })
      .then((response) => setAlerts(response.data || []))
      .catch(() => setError("Could not load abuse alerts."))
      .finally(() => setLoading(false));
  }, [severity, status]);

  useEffect(() => { loadAlerts(); }, [loadAlerts]);

  const counts = useMemo(() => ({
    total: alerts.length,
    high: alerts.filter((alert) => alert.severity === "HIGH" || alert.severity === "CRITICAL").length,
    open: alerts.filter((alert) => alert.status === "OPEN").length,
    blocked: alerts.filter((alert) => alert.alertType === "SESSION_BLOCKED" || alert.status === "BLOCKED").length,
  }), [alerts]);

  return <AdminShell title="Abuse Alerts">
    <section className="admin-header queue-security-header">
      <div><h1>Abuse Alert List</h1><span>Detect and review suspicious queue sessions before they affect fair admission.</span></div>
    </section>
    {error && <div className="auth-message error">{error}</div>}
    <section className="queue-alert-summary">
      <div><span>Total alerts</span><strong>{loading ? "—" : counts.total}</strong><small>Current filter</small></div>
      <div className="is-danger"><span>High risk</span><strong>{loading ? "—" : counts.high}</strong><small>Needs escalation</small></div>
      <div className="is-warning"><span>Open reviews</span><strong>{loading ? "—" : counts.open}</strong><small>Awaiting action</small></div>
      <div className="is-success"><span>Blocked sessions</span><strong>{loading ? "—" : counts.blocked}</strong><small>Access prevented</small></div>
    </section>
    <section className="admin-panel queue-alert-panel">
      <div className="admin-panel-head queue-alert-toolbar">
        <div><h2>Suspicious activity</h2><p className="queue-filter-caption">Review evidence and flag or block the queue session from its detail page.</p></div>
        <div className="admin-filter-bar queue-alert-filters">
          <select aria-label="Filter alerts by severity" value={severity} onChange={(event) => setSeverity(event.target.value)}><option value="">All severity</option><option value="CRITICAL">Critical</option><option value="HIGH">High</option><option value="MEDIUM">Medium</option><option value="LOW">Low</option></select>
          <select aria-label="Filter alerts by status" value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All status</option><option value="OPEN">Open</option><option value="FLAGGED">Flagged</option><option value="BLOCKED">Blocked</option></select>
          <button className="admin-button" type="button" onClick={loadAlerts}>Refresh</button>
        </div>
      </div>
      <div className="admin-table-wrap"><table className="ow-table queue-alert-table"><thead><tr><th>Risk</th><th>Alert</th><th>User / session</th><th>Status</th><th>Detected</th><th /></tr></thead><tbody>
        {loading && <tr><td colSpan="6" className="queue-alert-empty">Loading security alerts…</td></tr>}
        {!loading && alerts.length === 0 && <tr><td colSpan="6" className="queue-alert-empty">No alerts match these filters.</td></tr>}
        {!loading && alerts.map((alert) => <tr key={alert.alertId}>
          <td><span className={`queue-risk-badge risk-${alert.severity.toLowerCase()}`}>{alert.severity}</span></td>
          <td><strong>{alert.alertType.replaceAll("_", " ")}</strong><small>Alert #{alert.alertId}</small></td>
          <td><strong>User #{alert.userId}</strong><small className="queue-session-id">{alert.sessionId}</small></td>
          <td><span className={`admin-status-pill status-${alert.status.toLowerCase()}`}>{alert.status}</span></td>
          <td><span className="queue-behavior">{alert.detectedBehavior}</span><small>{new Date(alert.createdAt).toLocaleString()}</small></td>
          <td><Link className="queue-view-link" to={`/admin/queue/abuse-alerts/${alert.alertId}`}>Review <span aria-hidden="true">→</span></Link></td>
        </tr>)}
      </tbody></table></div>
    </section>
  </AdminShell>;
}
