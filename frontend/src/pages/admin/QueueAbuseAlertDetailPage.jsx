import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import AdminShell from "./AdminShell";
import queueAdminApi from "../../api/queueAdminApi";
import "./AdminPages.css";

export default function QueueAbuseAlertDetailPage() {
  const { alertId } = useParams();
  const [alert, setAlert] = useState(null);
  const [error, setError] = useState("");

  useEffect(() => {
    queueAdminApi.getAlert(alertId)
      .then((response) => setAlert(response.data))
      .catch(() => setError("Could not load alert details."));
  }, [alertId]);

  const action = async (name) => {
    if (!alert) return;
    if (name === "block" && !window.confirm("Block this suspicious queue session? The user will lose queue access.")) return;
    try {
      await queueAdminApi[name](alert.sessionId, "Manual security review");
      setAlert((current) => ({ ...current, status: name === "block" ? "BLOCKED" : name === "flag" ? "FLAGGED" : "OPEN" }));
    } catch {
      setError("Security action failed.");
    }
  };

  return <AdminShell title="Abuse Alert Details">
    <section className="admin-header queue-security-header">
      <div><h1>Alert {alertId}</h1><span>Inspect the evidence, then decide whether this session should be flagged or blocked.</span></div>
      <Link className="admin-button" to="/admin/queue/abuse-alerts">Back to alerts</Link>
    </section>
    {error && <div className="auth-message error">{error}</div>}
    {alert && <section className="queue-alert-detail-grid">
      <div className="admin-panel queue-detail-main">
        <div className="queue-detail-title"><div><span className={`queue-risk-badge risk-${alert.severity.toLowerCase()}`}>{alert.severity} risk</span><h2>{alert.alertType.replaceAll("_", " ")}</h2><p>Created {new Date(alert.createdAt).toLocaleString()}</p></div><span className={`admin-status-pill status-${alert.status.toLowerCase()}`}>{alert.status}</span></div>
        <div className="queue-evidence"><span>Detected behavior</span><strong>{alert.detectedBehavior || "No behavior description provided."}</strong><p>This alert is linked to the queue session below. Use the evidence and current status to apply the least disruptive action.</p></div>
        <div className="ow-actions queue-detail-actions">
          <button className="admin-button" type="button" disabled={alert.status === "BLOCKED"} onClick={() => action("flag")}>Flag Session</button>
          <button className="admin-button admin-btn-danger" type="button" disabled={alert.status === "BLOCKED"} onClick={() => action("block")}>Block Session</button>
          <button className="admin-button" type="button" disabled={alert.status !== "BLOCKED"} onClick={() => action("unblock")}>Unblock Session</button>
        </div>
      </div>
      <aside className="admin-panel queue-detail-meta"><h3>Session context</h3><dl className="queue-admin-details"><dt>Event</dt><dd>{alert.eventId}</dd><dt>User</dt><dd>{alert.userId}</dd><dt>Session</dt><dd>{alert.sessionId}</dd><dt>Queue token</dt><dd>{alert.queueToken}</dd><dt>Alert ID</dt><dd>#{alert.alertId}</dd></dl></aside>
    </section>}
  </AdminShell>;
}
