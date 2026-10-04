import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import appealApi from "../api/appealApi";
import { Banner, fmt, dash, errMsg, statusPill } from "./admin/frCommon";
import "./admin/fraudRisk.css";

const MIN = 10;
const MAX = 2000;
const MAX_LINKS = 5;

// Customer view of a risk decision + Submit Appeal Request
// GET api/risk/decisions/{id} (public view: outcome and general reason only, BR-237) + POST api/risk/appeals
// Route: /risk/decisions/:id/appeal  (open it from the block notification)
export default function SubmitAppealPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [d, setD] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [reason, setReason] = useState("");
  const [touched, setTouched] = useState(false);
  const [links, setLinks] = useState([]);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(null);

  useEffect(() => {
    (async () => {
      try {
        setD((await appealApi.getDecision(id)).data);
      } catch (e) {
        setError(e.response?.status === 404
          ? "We could not find this decision."
          : errMsg(e, "We could not load this decision."));
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const len = reason.trim().length;
  const reasonErr = touched && (len < MIN ? `Please write at least ${MIN} characters.` : len > MAX ? `Please keep it under ${MAX} characters.` : "");

  const setLink = (i, k, v) => setLinks((ls) => ls.map((l, j) => (j === i ? { ...l, [k]: v } : l)));
  const addLink = () => links.length < MAX_LINKS && setLinks([...links, { fileName: "", url: "" }]);

  const submit = async (e) => {
    e.preventDefault();
    setTouched(true);
    if (len < MIN || len > MAX) return;

    const filled = links.filter((l) => l.url.trim());
    for (const l of filled) {
      if (!/^https?:\/\//i.test(l.url.trim())) { setError("Each evidence link must start with http:// or https://"); return; }
    }

    setBusy(true);
    setError("");
    try {
      const evidence = filled.map((l, i) => ({
        fileName: l.fileName.trim() || `Evidence ${i + 1}`,
        url: l.url.trim(),
        sizeBytes: 0,
      }));
      const res = await appealApi.submit({ riskDecisionId: Number(id), reason: reason.trim(), evidence });
      setSent(res.data);
      setNotice("Your appeal has been sent.");
    } catch (e2) {
      setError(errMsg(e2, "We could not send your appeal. Please try again."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="fr-page fr-standalone">
      <div className="fr-top">
        <div>
          <h1>Your account activity</h1>
          <p>{dash(d?.decisionCode)}</p>
        </div>
        <button type="button" className="admin-button-secondary" onClick={() => navigate(-1)}>← Back</button>
      </div>

      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      {loading ? (
        <div className="empty-state">Loading...</div>
      ) : !d ? null : (
        <section className="admin-panel">
          <div className="panel-header">
            <h2>{d.action === "BLOCK" ? "Activity blocked" : "Under review"}</h2>
            <span className={`kyc-pill ${statusPill(d.status)}`}>{d.status}</span>
          </div>
          <form className="fr-body" onSubmit={submit}>
            <div className="fr-info">
              {dash(d.reasonPublic)}
              <br />
              Applies to: {dash(d.scope)} · Since {fmt(d.createdAt)}{d.expiresAt ? ` · Until ${fmt(d.expiresAt)}` : ""}
            </div>

            {sent ? (
              <div className="fr-info fr-info--warn">
                <b>Thank you. We received your appeal (#{sent.riskAppealId}).</b> A reviewer will answer by {fmt(sent.slaDueAt)} and you will be notified of the result.
              </div>
            ) : d.existingAppealId != null ? (
              <div className="fr-info fr-info--warn">
                You already sent an appeal for this decision (#{d.existingAppealId}, status <b>{d.existingAppealStatus}</b>).
                {" "}Only one appeal is allowed per decision.
              </div>
            ) : !d.canAppeal ? (
              <div className="fr-info fr-info--warn">
                <b>This decision cannot be appealed.</b> {dash(d.cannotAppealReason)}
              </div>
            ) : (
              <>
                <div className="fr-field">
                  <div className="fr-label">Why should this be lifted?<small>{len} / {MAX.toLocaleString()}</small></div>
                  <textarea
                    className={`fr-area${reasonErr ? " is-err" : ""}`}
                    placeholder="Explain what happened in your own words"
                    value={reason}
                    maxLength={MAX + 200}
                    onChange={(e) => setReason(e.target.value)}
                    onBlur={() => setTouched(true)}
                  />
                  {reasonErr ? <span className="fr-err">{reasonErr}</span> : <span className="fr-hint">Required. At least {MIN} characters.</span>}
                </div>

                <div className="fr-field">
                  <div className="fr-label">Supporting evidence <small>Optional · up to {MAX_LINKS} links</small></div>
                  {links.map((l, i) => (
                    <div key={i} className="fr-row" style={{ marginBottom: 8 }}>
                      <input style={{ flex: 1 }} placeholder="Name (e.g. Bank statement)" value={l.fileName} onChange={(e) => setLink(i, "fileName", e.target.value)} />
                      <input style={{ flex: 2 }} placeholder="https://link-to-your-file" value={l.url} onChange={(e) => setLink(i, "url", e.target.value)} />
                      <button type="button" className="admin-button-secondary" onClick={() => setLinks(links.filter((_, j) => j !== i))}>Remove</button>
                    </div>
                  ))}
                  {links.length < MAX_LINKS && (
                    <button type="button" className="admin-button-secondary" onClick={addLink}>+ Add evidence link</button>
                  )}
                  <span className="fr-hint">Upload your file to a storage service you trust and paste a shareable link.</span>
                </div>

                <div className="fr-info fr-info--warn">
                  You can send <b>one appeal per decision</b>, within 7 days of the notice. A reviewer answers within 48 hours.
                </div>

                <div className="fr-row end">
                  <button type="button" className="admin-button-secondary" onClick={() => navigate(-1)}>Cancel</button>
                  <button type="submit" className="admin-button" disabled={busy}>{busy ? "Sending..." : "Submit appeal"}</button>
                </div>
              </>
            )}
          </form>
        </section>
      )}
    </div>
  );
}
