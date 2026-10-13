import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import appealApi from "../../api/appealApi";
import riskApi from "../../api/riskApi";
import { Shell, Banner, Chips, fmt, dash, errMsg, levelPill, statusPill } from "./frCommon";

const MIN_REASON = 10;
const MAX_REASON = 500;
const num = (v) => (v == null || v === "" ? "—" : Number(v).toFixed(2));

// scoreByGroup: { bot: {Velocity: 40}, fraud: {Payment: 25} } or { Velocity: 40 }
function groupRows(v) {
  if (!v || typeof v !== "object") return [];
  return Object.entries(v).flatMap(([k, val]) =>
    val && typeof val === "object"
      ? Object.entries(val).map(([g2, s]) => [`${k} / ${g2}`, s])
      : [[k, val]]
  );
}

// View Risk Decision Details (Admin) — GET api/risk/decisions/{id}
// Decisions are immutable records (BR-250): this page never edits scores or rules, it only restores.
export default function RiskDecisionDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [d, setD] = useState(null);
  const [appeal, setAppeal] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [restoreOpen, setRestoreOpen] = useState(false);
  const [restoreReason, setRestoreReason] = useState("");
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const dec = (await appealApi.getDecision(id)).data;
      setD(dec);
      // The decision DTO does not carry the appeal id, so look it up in the user's appeals (non-fatal if it fails).
      setAppeal(null);
      if (dec.userId != null) {
        try {
          const res = await appealApi.search({ userId: dec.userId, page: 1, pageSize: 100 });
          setAppeal((res.data?.items || []).find((x) => x.riskDecisionId === dec.riskDecisionId) || null);
        } catch { /* ignore */ }
      }
    } catch (e) {
      setError(errMsg(e, "Failed to load risk decision."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);

  const restore = async () => {
    const text = restoreReason.trim();
    if (text.length < MIN_REASON || text.length > MAX_REASON) {
      setError(`The restore reason must be ${MIN_REASON}-${MAX_REASON} characters.`);
      return;
    }
    setBusy(true);
    setError("");
    try {
      await riskApi.restoreBlock(d.riskDecisionId, text);
      setRestoreOpen(false);
      setRestoreReason("");
      setNotice("Restored. The customer has been notified.");
      await load();
    } catch (e) {
      setError(errMsg(e, "Failed to restore. It may have already been restored."));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Shell title="Risk Decision"><div className="empty-state">Loading...</div></Shell>;
  if (!d) {
    return (
      <Shell title="Risk Decision">
        <Banner error={error} onClearError={() => setError("")} />
        <button type="button" className="admin-button-secondary" onClick={() => navigate(-1)}>← Back</button>
      </Shell>
    );
  }

  const canRestore = (d.action === "BLOCK" || d.action === "HOLD") && d.status === "Active" && !d.isShadow;
  const groups = groupRows(d.scoreByGroup);
  const decidedBy = d.decidedByType === "STAFF" ? `Admin #${d.decidedBy}` : "the risk engine";

  return (
    <Shell title="Risk Decision">
      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      <div className="fr-top">
        <div>
          <h1>Risk decision</h1>
          <p>{dash(d.decisionCode)}</p>
        </div>
        <div className="fr-row">
          {d.fraudAlertId != null && (
            <button type="button" className="admin-button-secondary" onClick={() => navigate(`/admin/fraud-alerts/${d.fraudAlertId}`)}>View alert</button>
          )}
          <button type="button" className="admin-button-secondary" disabled={!appeal}
            title={appeal ? "" : "No appeal for this decision"}
            onClick={() => navigate(`/admin/risk/appeals/${appeal.riskAppealId}`)}>
            {appeal ? `View appeal (${appeal.status})` : "No appeal"}
          </button>
          {canRestore && (
            <button type="button" className="admin-button-secondary" onClick={() => setRestoreOpen((v) => !v)}>Restore</button>
          )}
          <button type="button" className="admin-button-secondary" onClick={() => navigate(-1)}>← Back</button>
        </div>
      </div>

      {restoreOpen && (
        <section className="admin-panel">
          <div className="panel-header"><h2>Restore this decision</h2></div>
          <div className="fr-body">
            <div className="fr-field">
              <div className="fr-label">Reason<small>{restoreReason.trim().length} / {MAX_REASON}</small></div>
              <textarea className="fr-area" maxLength={MAX_REASON} placeholder="Why the block reason no longer applies (at least 10 characters)"
                value={restoreReason} onChange={(e) => setRestoreReason(e.target.value)} />
              <span className="fr-hint">Only an Admin can restore manually, and a documented reason is required. The customer is notified.</span>
            </div>
            <div className="fr-row end">
              <button type="button" className="admin-button-secondary" onClick={() => setRestoreOpen(false)}>Cancel</button>
              <button type="button" className="admin-button" disabled={busy} onClick={restore}>
                {busy ? "Processing..." : "Confirm restore"}
              </button>
            </div>
          </div>
        </section>
      )}

      <section className="admin-panel">
        <div className="panel-header">
          <div>
            <h2>{[d.action, d.scope].filter(Boolean).join(" · ") || "Decision"}</h2>
            <p className="fr-sub">Decided by {decidedBy} · {fmt(d.createdAt)} · Expires {fmt(d.expiresAt)}</p>
          </div>
          <div className="fr-row">
            {d.riskLevel && <span className={`kyc-pill ${levelPill(d.riskLevel)}`}>{d.riskLevel}</span>}
            {d.status && <span className={`kyc-pill ${statusPill(d.status)}`}>{d.status}</span>}
          </div>
        </div>

        <div className="fr-body">
          <div className="fr-stats">
            <div className="fr-stat"><small>Bot score</small><b>{num(d.botScore)}</b></div>
            <div className="fr-stat"><small>Fraud score</small><b>{num(d.fraudScore)}</b></div>
            <div className="fr-stat"><small>Dominant type</small><b>{dash(d.dominantType)}</b></div>
          </div>

          <div className="fr-kv">
            <span>Why</span><div><b>{dash(d.reasonCode)}</b></div>
            <span>Manual reason</span><div>{dash(d.manualReason)}</div>
            <span>User</span><div>{d.userId != null ? `User #${d.userId}` : "—"}</div>
            <span>Order / Ticket</span>
            <div>{[d.orderId != null && `Order #${d.orderId}`, d.ticketId != null && `Ticket #${d.ticketId}`].filter(Boolean).join(" · ") || "—"}</div>
            <span>Session</span><div>{dash(d.sessionId)}</div>
            <span>Device</span><div>{dash(d.deviceFp)}</div>
            <span>IP address</span><div>{dash(d.ipAddress)} <span className="fr-hint">(partially masked)</span></div>
            <span>Rule version</span><div>{dash(d.ruleVersion)} <span className="fr-hint">(version in force when this decision was made)</span></div>
            <span>Escalation level</span><div>{dash(d.escalationLevel)}</div>
            <span>Shadow mode</span><div>{d.isShadow ? "Yes (not enforced, the customer cannot see it)" : "No"}</div>
            <span>Hard rule</span><div>{d.hardRule || "None"}</div>
          </div>

          {d.status === "Restored" || d.status === "Overturned" ? (
            <div className="fr-info">
              <b>{d.status === "Overturned" ? "Overturned by an accepted appeal" : "Restored"}</b>{" "}
              · by Admin #{dash(d.restoredBy)} · {fmt(d.restoredAt)}<br />
              Reason: {dash(d.restoreReason)}
            </div>
          ) : null}

          {groups.length > 0 && (
            <div className="fr-field">
              <div className="fr-label">Score by group</div>
              <div className="table-wrap" style={{ padding: 0 }}>
                <table className="user-table">
                  <thead><tr><th>Group</th><th>Score</th></tr></thead>
                  <tbody>{groups.map(([k, v]) => <tr key={k}><td>{k}</td><td>{num(v)}</td></tr>)}</tbody>
                </table>
              </div>
            </div>
          )}

          <div className="fr-field"><div className="fr-label">Triggered rules</div><Chips value={d.triggeredRules} /></div>
          <div className="fr-field"><div className="fr-label">Trust modifiers</div><Chips value={d.trustModifiers} /></div>
          <div className="fr-field"><div className="fr-label">Signals missing</div><Chips value={d.signalsMissing} /></div>
        </div>
      </section>
    </Shell>
  );
}
