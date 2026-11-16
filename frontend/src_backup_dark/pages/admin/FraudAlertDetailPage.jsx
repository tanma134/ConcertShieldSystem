import { useEffect, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import riskApi from "../../api/riskApi";
import AdminShell from "./AdminShell";
import "./fraudRisk.css";

// Bọc mọi nội dung (kể cả loading / lỗi / popup) trong .fr-page để fraudRisk.css được áp dụng
const Shell = ({ title, children }) => (
  <AdminShell title={title}>
    <div className="fr-page">{children}</div>
  </AdminShell>
);

const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");

function errMsg(e, fallback) {
  return e.response?.data?.message || fallback;
}

// ---- Matched rules: render as a readable list instead of raw JSON ----
const TITLE_KEYS = ["rule", "ruleName", "rule_name", "name", "ruleCode", "rule_code", "code", "id"];
const SCORE_KEYS = ["score", "weight", "points", "contribution"];
const DESC_KEYS = ["description", "reason", "message", "detail", "details"];

function humanize(key) {
  return String(key)
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/[_-]+/g, " ")
    .replace(/^./, (c) => c.toUpperCase());
}

function formatValue(v) {
  if (v == null) return "—";
  if (typeof v === "boolean") return v ? "Yes" : "No";
  if (Array.isArray(v)) return v.map(formatValue).join(", ");
  if (typeof v === "object") return JSON.stringify(v);
  return String(v);
}

// Accepts an array, an object, a JSON string, or a plain string -> array of rule items
function normalizeRules(value) {
  let v = value;
  if (typeof v === "string") {
    try { v = JSON.parse(v); } catch { return v.trim() ? [v] : []; }
  }
  if (v == null) return [];
  if (Array.isArray(v)) return v;
  if (typeof v === "object") {
    const arrayKey = Object.keys(v).find((k) => Array.isArray(v[k]));
    if (arrayKey && Object.keys(v).length === 1) return v[arrayKey];
    // { ruleName: value } or { ruleName: { ... } } -> one item per key
    return Object.entries(v).map(([k, val]) =>
      val !== null && typeof val === "object" && !Array.isArray(val)
        ? { name: k, ...val }
        : { name: k, value: val }
    );
  }
  return [v];
}

function RuleItem({ rule }) {
  if (rule == null || typeof rule !== "object") {
    return <li style={{ padding: "8px 0" }}>{formatValue(rule)}</li>;
  }
  const titleKey = TITLE_KEYS.find((k) => rule[k] != null);
  const scoreKey = SCORE_KEYS.find((k) => rule[k] != null);
  const descKey = DESC_KEYS.find((k) => rule[k] != null);
  const used = new Set([titleKey, scoreKey, descKey].filter(Boolean));
  const extras = Object.entries(rule).filter(([k]) => !used.has(k));

  return (
    <li style={{ padding: "10px 0", borderTop: "1px solid rgba(148,163,184,.18)" }}>
      <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        <strong>{titleKey ? formatValue(rule[titleKey]) : "Rule"}</strong>
        {scoreKey && (
          <span className="kyc-pill kyc-pill--pending">
            {humanize(scoreKey)}: {formatValue(rule[scoreKey])}
          </span>
        )}
      </div>
      {descKey && <div className="kyc-admin-note" style={{ margin: "4px 0 0" }}>{formatValue(rule[descKey])}</div>}
      {extras.length > 0 && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: "4px 16px", marginTop: 6, fontSize: 13 }}>
          {extras.map(([k, v]) => (
            <span key={k}>
              <span style={{ opacity: 0.65 }}>{humanize(k)}:</span> {formatValue(v)}
            </span>
          ))}
        </div>
      )}
    </li>
  );
}

function RuleList({ value, label }) {
  const rules = normalizeRules(value);
  if (rules.length === 0) return null;
  return (
    <details open style={{ marginTop: 6 }}>
      <summary style={{ cursor: "pointer", color: "var(--kyc-accent, #c026d3)" }}>
        {label} ({rules.length})
      </summary>
      <ul style={{ listStyle: "none", margin: "8px 0 0", padding: 0 }}>
        {rules.map((r, i) => <RuleItem key={i} rule={r} />)}
      </ul>
    </details>
  );
}

function VerdictPill({ verdict }) {
  const cls =
    verdict === "bot" || verdict === "fraud" ? "kyc-pill--bad"
    : verdict === "clean" ? "kyc-pill--ok"
    : "kyc-pill--muted";
  return <span className={`kyc-pill ${cls}`}>{verdict}</span>;
}

// UC_18.2 View Fraud Alert Details, with UC_18.3 Block / UC_18.4 Restore actions
// attached directly to the alert for convenience.
export default function FraudAlertDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [aiLoading, setAiLoading] = useState(false);

  const [blockOpen, setBlockOpen] = useState(false);
  const [blockForm, setBlockForm] = useState({ scope: "ACCOUNT", durationHours: 24, reason: "", userId: "", ticketId: "" });
  const [blockSubmitting, setBlockSubmitting] = useState(false);

  const [restoreTargetId, setRestoreTargetId] = useState(null);
  const [restoreReason, setRestoreReason] = useState("");
  const [restoreSubmitting, setRestoreSubmitting] = useState(false);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await riskApi.getDetail(id);
      setDetail(res.data);
    } catch (e) {
      setError(errMsg(e, "Failed to load alert details."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);

  const handleGenerateAi = async () => {
    setAiLoading(true);
    setError("");
    try {
      await riskApi.generateAiSummary(id);
      await load();
    } catch (e) {
      setError(errMsg(e, "AI is temporarily unavailable. You can still review the scores and rules below."));
    } finally {
      setAiLoading(false);
    }
  };

  const openBlockForAlert = () => {
    setBlockForm({
      scope: "ACCOUNT",
      durationHours: 24,
      reason: "",
      userId: detail?.alert?.userId != null ? String(detail.alert.userId) : "",
      ticketId: detail?.ticketId != null ? String(detail.ticketId) : "",
    });
    setBlockOpen(true);
  };

  const submitBlock = async (e) => {
    e.preventDefault();
    const reason = blockForm.reason.trim();
    const hours = Number(blockForm.durationHours);
    const isTicket = blockForm.scope === "TICKET";
    if (!blockForm.userId.trim()) { setError("Enter the User ID: the customer is notified of the block and can appeal it."); return; }
    if (isTicket && !blockForm.ticketId.trim()) { setError("Enter the Ticket ID to block."); return; }
    if (!Number.isInteger(hours) || hours < 1 || hours > 720) { setError("Duration must be a whole number of hours between 1 and 720."); return; }
    if (reason.length < 10 || reason.length > 500) { setError("The reason must be 10-500 characters."); return; }
    setBlockSubmitting(true);
    setError("");
    try {
      await riskApi.createBlock({
        fraudAlertId: Number(id),
        scope: blockForm.scope,
        userId: Number(blockForm.userId),
        ticketId: isTicket ? Number(blockForm.ticketId) : undefined,
        durationHours: hours,
        reason,
      });
      setBlockOpen(false);
      setNotice("Block created. The customer has been notified and can appeal within 7 days.");
      await load();
    } catch (e2) {
      setError(errMsg(e2, "Failed to create block. Please check the information."));
    } finally {
      setBlockSubmitting(false);
    }
  };

  const submitRestore = async (decisionId) => {
    const text = restoreReason.trim();
    if (text.length < 10 || text.length > 500) {
      setError("The restore reason must be 10-500 characters.");
      return;
    }
    setRestoreSubmitting(true);
    setError("");
    try {
      await riskApi.restoreBlock(decisionId, text);
      setRestoreTargetId(null);
      setRestoreReason("");
      setNotice("Restored. The customer has been notified.");
      await load();
    } catch (e) {
      setError(errMsg(e, "Failed to restore. It may have already been restored."));
    } finally {
      setRestoreSubmitting(false);
    }
  };

  if (loading) {
    return <Shell title="Fraud Alert Details"><div className="empty-state">Loading...</div></Shell>;
  }
  if (!detail) {
    return (
      <Shell title="Fraud Alert Details">
        {error && <div className="auth-message error">{error}</div>}
        <button type="button" className="admin-button-secondary" onClick={() => navigate(-1)}>Back</button>
      </Shell>
    );
  }

  const { alert, decisions, aiSummary } = detail;
  const ai = aiSummary; // already an object (read directly from AdminDbContext jsonb)

  return (
    <Shell title={`Fraud Alert #${alert.fraudAlertId}`}>
      {error && (
        <div className="auth-message error">
          {error}
          <button type="button" onClick={() => setError("")}>Dismiss</button>
        </div>
      )}
      {notice && (
        <div className="auth-message" style={{ background: "rgba(74,222,128,.12)", borderColor: "rgba(74,222,128,.3)" }}>
          {notice}
          <button type="button" onClick={() => setNotice("")}>Dismiss</button>
        </div>
      )}

      <button type="button" className="admin-button-secondary" style={{ marginBottom: 16 }} onClick={() => navigate("/admin/fraud-alerts")}>
        ← Alert list
      </button>

      {/* UC_18.2 - View Fraud Alert Details */}
      <section className="admin-panel">
        <div className="panel-header">
          <h2>Alert information</h2>
          <div style={{ display: "flex", gap: 8 }}>
            <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
            <button type="button" className="admin-button" onClick={openBlockForAlert}>
              Block for this alert
            </button>
          </div>
        </div>

        <div className="kyc-preview" style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(180px,1fr))", gap: 12 }}>
          <div><dt>Type</dt><dd>{alert.alertType}</dd></div>
          <div><dt>Risk level</dt><dd>{alert.riskLevel}</dd></div>
          <div><dt>Score</dt><dd>{alert.riskScore} (bot {alert.botScore} / fraud {alert.fraudScore})</dd></div>
          <div><dt>Status</dt><dd>{alert.status}</dd></div>
          <div><dt>User</dt><dd>{alert.userId != null ? `#${alert.userId}` : "—"}</dd></div>
          <div><dt>Created at</dt><dd>{fmt(alert.createdAt)}</dd></div>
          <div><dt>Reviewed by</dt><dd>{alert.reviewedBy != null ? `#${alert.reviewedBy}` : "—"}</dd></div>
          <div><dt>Reviewed at</dt><dd>{fmt(alert.reviewedAt)}</dd></div>
          <div><dt>Session (masked)</dt><dd>{detail.sessionId || "—"}</dd></div>
          <div><dt>Order / Ticket / Event</dt><dd>{detail.orderId ?? "—"} / {detail.ticketId ?? "—"} / {detail.eventId ?? "—"}</dd></div>
        </div>
        {detail.details && (
          <p className="kyc-admin-note" style={{ marginTop: 12 }}>{detail.details}</p>
        )}
      </section>

      {/* AI summary */}
      <section className="admin-panel">
        <div className="panel-header">
          <h2>AI analysis</h2>
          <button type="button" className="admin-button-secondary" onClick={handleGenerateAi} disabled={aiLoading}>
            {aiLoading ? "Calling AI..." : ai ? "Regenerate summary" : "Generate AI summary"}
          </button>
        </div>

        {!ai ? (
          <div className="empty-state">No AI summary for this alert yet.</div>
        ) : (
          <div className="kyc-item-editor">
            <div style={{ display: "flex", gap: 10, alignItems: "center", marginBottom: 8 }}>
              <VerdictPill verdict={ai.verdict} />
              <span className="kyc-admin-note" style={{ margin: 0 }}>Confidence: {ai.confidence}%</span>
              <span className="kyc-admin-note" style={{ margin: 0 }}>Suggested action: <strong>{ai.suggested_action}</strong></span>
            </div>
            <p style={{ margin: "8px 0" }}>{ai.summary}</p>
            {ai.key_signals?.length > 0 && (
              <ul style={{ margin: "8px 0", paddingLeft: 18 }}>
                {ai.key_signals.map((s, i) => <li key={i}>{s}</li>)}
              </ul>
            )}
            {ai.caveats && <p className="kyc-admin-note">Caveats: {ai.caveats}</p>}
            <p className="kyc-admin-note" style={{ marginTop: 8 }}>
              Model: {ai.model} · Generated at: {fmt(ai.generated_at)} — This is a suggestion only; the final decision rests with the Admin.
            </p>
          </div>
        )}
      </section>

      {/* UC_18.3 / UC_18.4 - Decisions (blocks) related to this alert */}
      <section className="admin-panel">
        <div className="panel-header"><h2>Risk Decisions</h2></div>

        {decisions.length === 0 ? (
          <div className="empty-state">No decisions for this alert yet.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>Code</th><th>Action</th><th>Scope</th><th>Status</th><th>Reason code</th>
                  <th>Shadow</th><th>Expires at</th><th>Decided by</th><th></th>
                </tr>
              </thead>
              <tbody>
                {decisions.map((d) => (
                  <tr key={d.riskDecisionId}>
                    <td style={{ fontFamily: "monospace", fontSize: 12 }}>{d.decisionCode}</td>
                    <td>{d.action}</td>
                    <td>{d.scope || "—"}</td>
                    <td>
                      <span className={`kyc-pill ${d.status === "Active" ? "kyc-pill--bad" : d.status === "Restored" || d.status === "Overturned" ? "kyc-pill--ok" : "kyc-pill--muted"}`}>
                        {d.status}
                      </span>
                    </td>
                    <td style={{ fontSize: 12 }}>{d.reasonCode || "—"}</td>
                    <td>{d.isShadow ? "Yes (not enforced)" : "No"}</td>
                    <td>{fmt(d.expiresAt)}</td>
                    <td>{d.decidedByType === "STAFF" ? `Admin #${d.decidedBy}` : "System"}</td>
                    <td>
                      <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
                        <Link className="admin-button-secondary" to={`/admin/risk/decisions/${d.riskDecisionId}`}>
                          Details
                        </Link>
                        {(d.action === "BLOCK" || d.action === "HOLD") && d.status === "Active" && !d.isShadow && (
                          restoreTargetId === d.riskDecisionId ? (
                            <div style={{ display: "flex", gap: 6 }}>
                              <input
                                placeholder="Restore reason"
                                value={restoreReason}
                                onChange={(e) => setRestoreReason(e.target.value)}
                                style={{ minWidth: 160 }}
                              />
                              <button type="button" className="admin-button" disabled={restoreSubmitting}
                                onClick={() => submitRestore(d.riskDecisionId)}>
                                Confirm
                              </button>
                              <button type="button" className="admin-button-secondary"
                                onClick={() => { setRestoreTargetId(null); setRestoreReason(""); }}>
                                Cancel
                              </button>
                            </div>
                          ) : (
                            <button type="button" className="admin-button-secondary"
                              onClick={() => setRestoreTargetId(d.riskDecisionId)}>
                              Restore
                            </button>
                          )
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {decisions.map((d) => (
          <div key={`json-${d.riskDecisionId}`} style={{ marginTop: 10 }}>
            <RuleList label={`Matched rule details — ${d.decisionCode}`} value={d.triggeredRules} />
          </div>
        ))}
      </section>

      {/* Simple modal to create a block for this alert */}
      {blockOpen && (
        <div style={{
          position: "fixed", inset: 0, background: "rgba(0,0,0,.5)",
          display: "flex", alignItems: "center", justifyContent: "center", zIndex: 50,
        }}>
          <form onSubmit={submitBlock} className="admin-panel" style={{ maxWidth: 480, width: "90%" }}>
            <div className="panel-header"><h2>Block for alert #{alert.fraudAlertId}</h2></div>
            <div className="kyc-admin-fields">
              <label>
                Scope
                <select value={blockForm.scope} onChange={(e) => setBlockForm((f) => ({ ...f, scope: e.target.value }))}>
                  <option value="ACCOUNT">ACCOUNT (end all sessions, lock account)</option>
                  <option value="TICKET">TICKET (ticket cannot be used for entry)</option>
                </select>
              </label>
              <label>
                User ID (owner)
                <input
                  inputMode="numeric"
                  value={blockForm.userId}
                  onChange={(e) => setBlockForm((f) => ({ ...f, userId: e.target.value }))}
                />
              </label>
              {blockForm.scope === "TICKET" && (
                <label>
                  Ticket ID
                  <input
                    inputMode="numeric"
                    value={blockForm.ticketId}
                    onChange={(e) => setBlockForm((f) => ({ ...f, ticketId: e.target.value }))}
                  />
                </label>
              )}
              <label>
                Duration (hours)
                <input
                  type="number" min={1} max={720}
                  value={blockForm.durationHours}
                  onChange={(e) => setBlockForm((f) => ({ ...f, durationHours: e.target.value }))}
                />
              </label>
              <label>
                Reason
                <textarea
                  rows={3}
                  maxLength={500}
                  placeholder="Fraud-risk evidence for this block (10-500 characters)"
                  value={blockForm.reason}
                  onChange={(e) => setBlockForm((f) => ({ ...f, reason: e.target.value }))}
                />
              </label>
            </div>
            <p className="kyc-admin-note" style={{ marginTop: 12 }}>
              Only an Admin can block manually, and you cannot block your own account. The customer is notified and can appeal within 7 days.
              If an appeal is already pending for this alert, the alert stays open until it is resolved.
            </p>
            <div style={{ display: "flex", gap: 8, justifyContent: "flex-end", marginTop: 16 }}>
              <button type="button" className="admin-button-secondary" onClick={() => setBlockOpen(false)}>Cancel</button>
              <button type="submit" className="admin-button" disabled={blockSubmitting}>
                {blockSubmitting ? "Processing..." : "Confirm block"}
              </button>
            </div>
          </form>
        </div>
      )}
    </Shell>
  );
}
