import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import appealApi from "../../api/appealApi";
import { useAuth } from "../../context/AuthContext";
import { Shell, Banner, Chips, fmt, dash, errMsg, levelPill, statusPill, slaInfo } from "./frCommon";

const MIN_NOTE = 10;
const MAX_NOTE = 1000;

// Review & Resolve Appeal (Admin only)
// GET api/admin/risk/appeals/{id} + POST api/admin/risk/appeals/{id}/resolve  { decision: "Accept" | "Reject", reviewNote }
export default function AppealReviewPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [a, setA] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [decision, setDecision] = useState("Accept");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      setA((await appealApi.getDetail(id)).data);
    } catch (e) {
      setError(errMsg(e, "Failed to load appeal."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [id]);

  const submit = async (e) => {
    e.preventDefault();
    const text = note.trim();
    if (text.length < MIN_NOTE || text.length > MAX_NOTE) {
      setError(`The review reason must be ${MIN_NOTE}-${MAX_NOTE} characters.`);
      return;
    }
    setBusy(true);
    setError("");
    try {
      const res = await appealApi.resolve(id, { decision, reviewNote: text });
      const lifted = res.data?.liftedDecisionIds || [];
      setNotice(
        decision === "Accept"
          ? `Appeal accepted. ${lifted.length} block decision(s) lifted${lifted.length ? ` (#${lifted.join(", #")})` : ""}. The customer has been notified.`
          : "Appeal rejected. The block stays in place and the customer has been notified."
      );
      setNote("");
      await load();
    } catch (e2) {
      setError(errMsg(e2, "Failed to resolve appeal. It may already have been resolved."));
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <Shell title="Appeal"><div className="empty-state">Loading...</div></Shell>;
  if (!a) {
    return (
      <Shell title="Appeal">
        <Banner error={error} onClearError={() => setError("")} />
        <button type="button" className="admin-button-secondary" onClick={() => navigate("/admin/risk/appeals")}>← Appeals</button>
      </Shell>
    );
  }

  const dec = a.decision || {};
  const pending = a.status === "Pending";
  const sla = pending ? slaInfo(a.slaDueAt) : null;
  const evidence = a.evidence || [];
  const myId = Number(user?.userId ?? user?.id ?? user?.UserId);
  const madeByMe = dec.decidedByType === "STAFF" && dec.decidedBy != null && dec.decidedBy === myId;

  return (
    <Shell title={`Appeal #${a.riskAppealId}`}>
      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      <div className="fr-top">
        <div>
          <h1>Appeal #{a.riskAppealId}</h1>
          <p>{[dec.decisionCode, dec.userId != null && `User #${dec.userId}`].filter(Boolean).join(" · ") || "—"}</p>
        </div>
        <div className="fr-row">
          {dec.riskDecisionId != null && (
            <button type="button" className="admin-button-secondary" onClick={() => navigate(`/admin/risk/decisions/${dec.riskDecisionId}`)}>View decision</button>
          )}
          {dec.fraudAlertId != null && (
            <button type="button" className="admin-button-secondary" onClick={() => navigate(`/admin/fraud-alerts/${dec.fraudAlertId}`)}>View alert</button>
          )}
          <button type="button" className="admin-button-secondary" onClick={() => navigate("/admin/risk/appeals")}>← Appeals</button>
        </div>
      </div>

      <section className="admin-panel">
        <div className="panel-header">
          <div>
            <h2>Customer appeal</h2>
            <p className="fr-sub">Submitted {fmt(a.createdAt)} · Resolve by {fmt(a.slaDueAt)}</p>
          </div>
          <div className="fr-row">
            <span className={`kyc-pill ${statusPill(a.status)}`}>{a.status}</span>
            {sla && <span className={`kyc-pill ${a.isOverdue ? "kyc-pill--bad" : sla.cls}`}>{sla.text}</span>}
          </div>
        </div>
        <div className="fr-body">
          <div className="fr-kv">
            <span>Customer says</span><div style={{ whiteSpace: "pre-wrap" }}>{dash(a.reason)}</div>
            <span>Evidence</span>
            <div>
              {evidence.length === 0 ? "—" : evidence.map((f, i) => (
                <a key={i} className="fr-chip" href={f.url} target="_blank" rel="noreferrer noopener" style={{ color: "var(--fr-pink)" }}>
                  {f.fileName || `Link ${i + 1}`}
                </a>
              ))}
            </div>
          </div>
          {evidence.length > 0 && (
            <span className="fr-hint">Evidence links are supplied by the customer. Open them with care.</span>
          )}
        </div>
      </section>

      <section className="admin-panel">
        <div className="panel-header">
          <div>
            <h2>Original decision</h2>
            <p className="fr-sub">
              Decided by {dec.decidedByType === "STAFF" ? `Admin #${dec.decidedBy}` : "the risk engine"} · {fmt(dec.createdAt)} · Expires {fmt(dec.expiresAt)}
            </p>
          </div>
          <div className="fr-row">
            {dec.riskLevel && <span className={`kyc-pill ${levelPill(dec.riskLevel)}`}>{dec.riskLevel}</span>}
            {dec.status && <span className={`kyc-pill ${statusPill(dec.status)}`}>{dec.status}</span>}
          </div>
        </div>
        <div className="fr-body">
          <div className="fr-kv">
            <span>Action / scope</span><div>{[dec.action, dec.scope].filter(Boolean).join(" · ") || "—"}</div>
            <span>Scores</span><div>Bot {dash(dec.botScore)} · Fraud {dash(dec.fraudScore)} · {dash(dec.dominantType)}</div>
            <span>Reason code</span><div>{dash(dec.reasonCode)}</div>
            <span>Manual reason</span><div>{dash(dec.manualReason)}</div>
            <span>Hard rule</span><div>{dec.hardRule || "None"}</div>
            <span>Rule version</span><div>{dash(dec.ruleVersion)} <span className="fr-hint">(version in force when the decision was made)</span></div>
            <span>Escalation level</span><div>{dash(dec.escalationLevel)}</div>
            <span>IP / device</span><div>{dash(dec.ipAddress)} · {dash(dec.deviceFp)} <span className="fr-hint">(partially masked)</span></div>
          </div>
          <div className="fr-field"><div className="fr-label">Triggered rules</div><Chips value={dec.triggeredRules} /></div>
          <div className="fr-field"><div className="fr-label">Trust modifiers</div><Chips value={dec.trustModifiers} /></div>
          {a.aiSummary?.summary && <div className="fr-ai"><b>AI summary.</b> {a.aiSummary.summary}</div>}
        </div>
      </section>

      <section className="admin-panel">
        <div className="panel-header"><h2>Resolution</h2></div>
        <form className="fr-body" onSubmit={submit}>
          {pending ? (
            <>
              {madeByMe && (
                <div className="fr-info fr-info--warn">
                  <b>You made the original decision.</b> Another Admin must review this appeal.
                </div>
              )}
              <div className="fr-field">
                <div className="fr-label">Outcome</div>
                <div className="fr-radios">
                  <button type="button" className={`fr-radio${decision === "Accept" ? " on" : ""}`} onClick={() => setDecision("Accept")}>
                    <span className="dot" />
                    <div><b>Accept</b><p>Lift the block. Every block row created by the same event is lifted and the customer is notified.</p></div>
                  </button>
                  <button type="button" className={`fr-radio${decision === "Reject" ? " on" : ""}`} onClick={() => setDecision("Reject")}>
                    <span className="dot" />
                    <div><b>Reject</b><p>Confirmed fraud: keep the block in place until it expires.</p></div>
                  </button>
                </div>
              </div>

              <div className="fr-field">
                <div className="fr-label">Review reason<small>{note.trim().length} / {MAX_NOTE.toLocaleString()}</small></div>
                <textarea className="fr-area" placeholder="Document why you accepted or rejected this appeal" maxLength={MAX_NOTE}
                  value={note} onChange={(e) => setNote(e.target.value)} />
                <span className="fr-hint">
                  At least {MIN_NOTE} characters. Recorded in the audit log only. The customer is told the outcome and a general reason, not this note.
                </span>
              </div>

              <div className="fr-info fr-info--warn">A resolved appeal cannot be changed.</div>

              <div className="fr-row end">
                <button type="button" className="admin-button-secondary" onClick={() => navigate("/admin/risk/appeals")}>Cancel</button>
                <button type="submit" className="admin-button" disabled={busy || madeByMe}>{busy ? "Processing..." : "Resolve appeal"}</button>
              </div>
            </>
          ) : (
            <div className="fr-info">
              <b>This appeal was {String(a.status).toLowerCase()}.</b>{" "}
              {a.reviewNote ? `Reason: ${a.reviewNote}` : ""}
              {a.reviewedBy != null ? ` · Reviewed by Admin #${a.reviewedBy}` : ""}
              {a.reviewedAt ? ` · ${fmt(a.reviewedAt)}` : ""}
            </div>
          )}
        </form>
      </section>
    </Shell>
  );
}
