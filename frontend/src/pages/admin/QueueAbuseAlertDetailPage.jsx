import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import AdminShell from "./AdminShell";
import queueAdminApi from "../../api/queueAdminApi";
import "./AdminPages.css";

const alertTypeLabels = {
  MULTIPLE_REGISTRATION_ATTEMPTS: "Multiple Registration Attempts",
  AUTOMATED_REQUEST_PATTERN: "Automated Request Pattern",
  TOKEN_REPLAY: "Token Replay",
};

const readEvidence = (value) => {
  try {
    return JSON.parse(value || "{}");
  } catch {
    return { summary: value };
  }
};

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
      if (name === "unblock") {
        await queueAdminApi.unblock(alert.sessionId, alert.alertId);
      } else {
        await queueAdminApi[name](alert.sessionId, "Manual security review", alert.alertId);
      }
      setAlert((current) => ({ ...current, status: name === "block" ? "BLOCKED" : name === "flag" ? "FLAGGED" : "OPEN" }));
    } catch {
      setError("Security action failed.");
    }
  };

  const evidence = alert ? readEvidence(alert.detectedBehavior) : null;

  return <AdminShell title="Abuse Alert Details">
    <section className="admin-header queue-security-header">
      <div><h1>Alert {alertId}</h1><span>Inspect the evidence, then decide whether this session should be flagged or blocked.</span></div>
      <Link className="admin-button" to="/admin/queue/abuse-alerts">Back to alerts</Link>
    </section>
    {error && <div className="auth-message error">{error}</div>}
    {alert && <section className="queue-alert-detail-grid">
      <div className="admin-panel queue-detail-main">
        <div className="queue-detail-title"><div><span className={`queue-risk-badge risk-${alert.severity.toLowerCase()}`}>{alert.severity} risk</span><h2>{alertTypeLabels[alert.alertType] || alert.alertType}</h2><p>Detected {new Date(alert.createdAt).toLocaleString()}</p></div><span className={`admin-status-pill status-${alert.status.toLowerCase()}`}>{alert.status}</span></div>
        <div className="queue-evidence">
          <span>Detection evidence</span>
          {alert.alertType === "MULTIPLE_REGISTRATION_ATTEMPTS" && <>
            <p><strong>{evidence.attemptCount ?? "—"}</strong> registration attempts in <strong>{evidence.detectionWindowSeconds ?? 300}</strong> seconds.</p>
            <p>Attempt times: {(evidence.attemptTimestamps || []).map((time) => new Date(time).toLocaleTimeString()).join(", ") || "Not available"}</p>
          </>}
          {alert.alertType === "AUTOMATED_REQUEST_PATTERN" && <>
            <p><strong>{evidence.requestCount ?? "—"}</strong> registration requests in <strong>{evidence.detectionWindowSeconds ?? 60}</strong> seconds.</p>
            <p>Interval coefficient of variation: {typeof evidence.coefficientOfVariation === "number" ? `${(evidence.coefficientOfVariation * 100).toFixed(1)}%` : "Not available"} (threshold ≤ 10%).</p>
            <p>Request times: {(evidence.requestTimestamps || []).map((time) => new Date(time).toLocaleTimeString()).join(", ") || "Not available"}</p>
          </>}
          {alert.alertType === "TOKEN_REPLAY" && <>
            <p><strong>{evidence.invalidTokenReuseAttemptCount ?? "—"}</strong> revoked-token reuse attempts in <strong>{evidence.detectionWindowSeconds ?? 120}</strong> seconds.</p>
            <p>Safe token fingerprint: <code>{evidence.tokenFingerprint || alert.tokenFingerprint || "Not available"}</code></p>
          </>}
          {evidence.summary && <p>{evidence.summary}</p>}
          <p>Detection indicates suspicious activity, not proof of malicious intent. Review before taking action.</p>
        </div>
        <div className="ow-actions queue-detail-actions">
          <button className="admin-button" type="button" disabled={alert.status === "BLOCKED"} onClick={() => action("flag")}>Flag Session</button>
          <button className="admin-button admin-btn-danger" type="button" disabled={alert.status === "BLOCKED"} onClick={() => action("block")}>Block Session</button>
          <button className="admin-button" type="button" disabled={alert.status !== "BLOCKED"} onClick={() => action("unblock")}>Unblock Session</button>
        </div>
      </div>
      <aside className="admin-panel queue-detail-meta"><h3>Session context</h3><dl className="queue-admin-details"><dt>Event</dt><dd>{alert.eventId}</dd><dt>User</dt><dd>{alert.userId ?? "—"}</dd><dt>Session</dt><dd>{alert.sessionId || "—"}</dd>{alert.alertType === "TOKEN_REPLAY" && <><dt>Token fingerprint</dt><dd>{evidence.tokenFingerprint || alert.tokenFingerprint || "—"}</dd></>}<dt>Alert ID</dt><dd>#{alert.alertId}</dd></dl></aside>
    </section>}
  </AdminShell>;
}
