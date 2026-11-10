import { useEffect, useMemo, useState } from "react";
import riskApi from "../../api/riskApi";
import { Shell, Banner, fmt, errMsg } from "./frCommon";

// UC_18.11 Configure Risk Policy Parameters (Admin). The Risk Engine re-reads the config within ~60 s.
const MIN_REASON = 10;
const MAX_REASON = 500;

const GROUPS = [
  ["Mode & version", "Shadow mode logs decisions without enforcing them.", (k) => k === "risk.mode" || k === "risk.rule_version"],
  ["Risk levels (thresholds)", "Score boundaries: 0 < Medium < High < Critical ≤ 100.", (k) => k.startsWith("risk.threshold.") || k === "risk.critical.min_groups"],
  ["Scoring", "Caps and combo bonus applied when several rule groups fire together.", (k) => k.startsWith("risk.cap.") || k.startsWith("risk.bonus.") || k.startsWith("risk.combo.")],
  ["Blocking & escalation", "How long automatic blocks last and when repeat offenders escalate.", (k) => k.startsWith("risk.block.") || k === "risk.trust.restore_days"],
  ["Alert SLA & appeals", "Review deadlines and appeal evidence limits.", (k) => k === "risk.alert.sla_hours" || k.startsWith("risk.appeal.")],
  ["AI summary & VIP", "AI analysis and VIP customer handling.", (k) => k.startsWith("risk.ai.") || k.startsWith("risk.vip.")],
];
const RULE_FIELD_ORDER = ["type", "score", "score_l2", "threshold", "threshold_l2", "window_sec"];

const isBool = (v) => /^(true|false)$/i.test(v);
const isNum = (v) => v !== "" && !Number.isNaN(Number(v));

function parseRule(key) {
  const rest = key.slice("risk.rule.".length);
  const i = rest.lastIndexOf(".");
  return i < 0 ? { code: rest, field: "value" } : { code: rest.slice(0, i), field: rest.slice(i + 1) };
}

// Client-side mirror of the server validation, so most mistakes are caught before saving.
function validate(values) {
  const errs = {};
  const n = (k) => (values[k] != null && isNum(values[k]) ? Number(values[k]) : null);
  const bad = (k, m) => { errs[k] ||= m; };

  for (const [k, v] of Object.entries(values)) {
    if (v.trim() === "") bad(k, "Value is required.");
    else if (v.length > 500) bad(k, "Max 500 characters.");
  }
  if (values["risk.mode"] != null && !["SHADOW", "ENFORCE"].includes(values["risk.mode"].toUpperCase())) bad("risk.mode", "Must be SHADOW or ENFORCE.");

  const m = n("risk.threshold.medium"), h = n("risk.threshold.high"), c = n("risk.threshold.critical");
  if (m != null && h != null && c != null && !(0 < m && m < h && h < c && c <= 100)) {
    const msg = "Need 0 < medium < high < critical ≤ 100.";
    ["risk.threshold.medium", "risk.threshold.high", "risk.threshold.critical"].forEach((k) => bad(k, msg));
  }
  const b1 = n("risk.block.hours.level1"), b2 = n("risk.block.hours.level2");
  if (b1 != null && b2 != null && (b1 < 1 || b2 < b1 || b2 > 720)) {
    bad("risk.block.hours.level1", "Need 1 ≤ level 1 ≤ level 2 ≤ 720.");
    bad("risk.block.hours.level2", "Need 1 ≤ level 1 ≤ level 2 ≤ 720.");
  }
  const range = (k, lo, hi) => { const x = n(k); if (values[k] != null && x != null && (x < lo || x > hi)) bad(k, `Must be ${lo}-${hi}.`); };
  range("risk.cap.group", 1, 100); range("risk.cap.trust", 0, 100); range("risk.bonus.combo", 0, 100);
  range("risk.ai.min_score", 0, 100); range("risk.appeal.max_evidence_files", 0, 20); range("risk.appeal.max_evidence_mb", 1, 50);
  const min = (k, lo) => { const x = n(k); if (values[k] != null && x != null && x < lo) bad(k, `Must be at least ${lo}.`); };
  min("risk.combo.min_groups", 2); min("risk.critical.min_groups", 1); min("risk.vip.min_order_amount", 0);
  ["risk.block.escalation_window_days", "risk.trust.restore_days", "risk.alert.sla_hours", "risk.appeal.deadline_days", "risk.appeal.sla_hours"].forEach((k) => min(k, 1));

  const codes = new Set(Object.keys(values).filter((k) => k.startsWith("risk.rule.")).map((k) => parseRule(k).code));
  for (const code of codes) {
    const get = (f) => values[`risk.rule.${code}.${f}`];
    ["score", "score_l2"].forEach((f) => { const x = get(f); if (x != null && isNum(x) && (Number(x) < 0 || Number(x) > 100)) bad(`risk.rule.${code}.${f}`, "Must be 0-100."); });
    ["threshold", "threshold_l2", "window_sec"].forEach((f) => { const x = get(f); if (x != null && isNum(x) && Number(x) < 0) bad(`risk.rule.${code}.${f}`, "Must not be negative."); });
    const t = get("threshold"), t2 = get("threshold_l2");
    if (t != null && t2 != null && isNum(t) && isNum(t2) && Number(t2) <= Number(t)) bad(`risk.rule.${code}.threshold_l2`, "Must be greater than threshold.");
    const ty = get("type");
    if (ty != null && !["bot", "fraud", "trust"].includes(ty.toLowerCase())) bad(`risk.rule.${code}.type`, "Bot, Fraud or Trust.");
  }
  return errs;
}

function buildControl({ item, value, error, onChange }) {
  const changed = value !== item.value;
  const lower = item.value.toLowerCase();
  let control;
  if (item.key === "risk.mode") {
    control = (
      <select value={value.toUpperCase()} onChange={(e) => onChange(e.target.value)}>
        <option value="SHADOW">SHADOW (log only)</option><option value="ENFORCE">ENFORCE (apply actions)</option>
      </select>
    );
  } else if (item.key.endsWith(".type")) {
    control = (
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {["Bot", "Fraud", "Trust"].map((t) => <option key={t} value={t}>{t}</option>)}
        {!["bot", "fraud", "trust"].includes(value.toLowerCase()) && <option value={value}>{value}</option>}
      </select>
    );
  } else if (isBool(item.value)) {
    control = (
      <select value={value.toLowerCase()} onChange={(e) => onChange(e.target.value)}>
        <option value="true">Enabled</option><option value="false">Disabled</option>
      </select>
    );
  } else {
    control = <input type={isNum(item.value) ? "number" : "text"} step="any" value={value} onChange={(e) => onChange(e.target.value)} />;
  }
  return { control, changed, lower };
}

function ParamRow({ item, value, error, onChange }) {
  const { control, changed } = buildControl({ item, value, error, onChange });
  return (
    <div className={`fr-param${changed ? " changed" : ""}`}>
      <div>
        <code>{item.key}</code>
        {item.description && <small>{item.description}</small>}
        <small>Updated {fmt(item.updatedAt)} by #{item.updatedBy}</small>
      </div>
      <div>
        {control}
        {changed && <div className="fr-was">was {item.value}</div>}
      </div>
      {error && <div className="fr-err">{error}</div>}
    </div>
  );
}

export default function RiskPolicyPage() {
  const [tab, setTab] = useState("risk_config");
  const [items, setItems] = useState([]);
  const [values, setValues] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [reason, setReason] = useState("");
  const [saving, setSaving] = useState(false);
  const [formErr, setFormErr] = useState("");
  const [query, setQuery] = useState("");

  const load = async () => {
    setLoading(true); setError("");
    try {
      const res = await riskApi.getPolicy();
      const list = Array.isArray(res.data) ? res.data : res.data?.items || [];
      setItems(list);
      setValues(Object.fromEntries(list.map((i) => [i.key, i.value])));
    } catch (e) {
      setError(errMsg(e, "Failed to load risk policy."));
    } finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const byKey = useMemo(() => Object.fromEntries(items.map((i) => [i.key, i])), [items]);
  const errors = useMemo(() => validate(values), [values]);
  const changes = useMemo(
    () => items.filter((i) => values[i.key] != null && values[i.key].trim() !== i.value).map((i) => ({ key: i.key, oldValue: i.value, newValue: values[i.key].trim() })),
    [items, values],
  );
  const errCount = Object.keys(errors).length;
  const set = (k, v) => setValues((s) => ({ ...s, [k]: v }));
  const matches = (i) => !query.trim() || i.key.toLowerCase().includes(query.trim().toLowerCase()) || (i.description || "").toLowerCase().includes(query.trim().toLowerCase());

  const configItems = items.filter((i) => i.category === "risk_config" && matches(i));
  const ruleItems = items.filter((i) => i.category === "risk_rule" && matches(i));
  const groups = GROUPS.map(([title, hint, test]) => ({ title, hint, rows: configItems.filter((i) => test(i.key)) }));
  const used = new Set(groups.flatMap((g) => g.rows.map((r) => r.key)));
  const other = configItems.filter((i) => !used.has(i.key));
  if (other.length) groups.push({ title: "Other", hint: "", rows: other });

  const rules = useMemo(() => {
    const m = new Map();
    for (const i of ruleItems) {
      const { code, field } = parseRule(i.key);
      if (!m.has(code)) m.set(code, []);
      m.get(code).push({ ...i, field });
    }
    const idx = (f) => { const x = RULE_FIELD_ORDER.indexOf(f); return x < 0 ? 99 : x; };
    return [...m.entries()].map(([code, rows]) => [code, rows.sort((a, b) => idx(a.field) - idx(b.field) || a.field.localeCompare(b.field))]);
  }, [ruleItems]);

  const tabChanges = (cat) => changes.filter((c) => byKey[c.key]?.category === cat).length;
  const switchingToEnforce = changes.some((c) => c.key === "risk.mode" && c.newValue.toUpperCase() === "ENFORCE");

  const openConfirm = () => {
    if (errCount) { setError(`Fix ${errCount} invalid value${errCount === 1 ? "" : "s"} before saving.`); return; }
    setReason(""); setFormErr(""); setConfirm(true);
  };
  const save = async (e) => {
    e.preventDefault();
    const r = reason.trim();
    if (r.length < MIN_REASON || r.length > MAX_REASON) return setFormErr(`The reason must be ${MIN_REASON}-${MAX_REASON} characters.`);
    setSaving(true); setFormErr("");
    try {
      const res = await riskApi.updatePolicy(changes.map((c) => ({ key: c.key, value: c.newValue })), r);
      setConfirm(false);
      setNotice(`${res.data?.changes?.length ?? changes.length} parameter(s) updated. ${res.data?.note || "Changes apply within about 60 seconds."}`);
      await load();
    } catch (e2) {
      setFormErr(errMsg(e2, "Failed to save the policy."));
    } finally { setSaving(false); }
  };

  return (
    <Shell title="Risk Policy">
      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      <div className="fr-top">
        <div>
          <h1>Risk policy parameters</h1>
          <p>Tune risk thresholds, scoring, block durations, SLAs and individual rules. The Risk Engine picks up changes within about 60 seconds.</p>
        </div>
        <button type="button" className="admin-button-secondary" onClick={load} disabled={loading || saving}>Reload</button>
      </div>

      <div className="fr-row" style={{ marginBottom: 18, justifyContent: "space-between" }}>
        <div className="fr-seg" role="tablist">
          <button type="button" className={tab === "risk_config" ? "on" : ""} onClick={() => setTab("risk_config")}>
            General{tabChanges("risk_config") ? ` · ${tabChanges("risk_config")} changed` : ""}
          </button>
          <button type="button" className={tab === "risk_rule" ? "on" : ""} onClick={() => setTab("risk_rule")}>
            Rules{tabChanges("risk_rule") ? ` · ${tabChanges("risk_rule")} changed` : ""}
          </button>
        </div>
        <input type="search" placeholder="Search parameter or description" value={query} onChange={(e) => setQuery(e.target.value)} style={{ width: 280 }} />
      </div>

      {loading ? (
        <div className="empty-state">Loading...</div>
      ) : items.length === 0 ? (
        <div className="empty-state">No risk parameters found.</div>
      ) : tab === "risk_config" ? (
        groups.filter((g) => g.rows.length).map((g) => (
          <details key={g.title} className="fr-group" open>
            <summary><span>{g.title}</span><small>{g.hint}</small></summary>
            <div className="fr-group-body">
              {g.rows.map((i) => <ParamRow key={i.key} item={i} value={values[i.key] ?? ""} error={errors[i.key]} onChange={(v) => set(i.key, v)} />)}
            </div>
          </details>
        ))
      ) : rules.length === 0 ? (
        <div className="empty-state">No rules match.</div>
      ) : (
        rules.map(([code, rows]) => {
          const type = values[`risk.rule.${code}.type`];
          const dirty = rows.some((r) => values[r.key] !== r.value);
          return (
            <details key={code} className="fr-group" open={dirty || rules.length <= 6}>
              <summary>
                <span>{code} {type && <span className={`kyc-pill ${/fraud/i.test(type) ? "kyc-pill--bad" : /bot/i.test(type) ? "kyc-pill--pending" : "kyc-pill--ok"}`} style={{ marginLeft: 8 }}>{type}</span>}</span>
                <small>{dirty ? "Modified" : `${rows.length} parameters`}</small>
              </summary>
              <div className="fr-group-body">
                {rows.map((i) => <ParamRow key={i.key} item={{ ...i, description: i.description || i.field }} value={values[i.key] ?? ""} error={errors[i.key]} onChange={(v) => set(i.key, v)} />)}
              </div>
            </details>
          );
        })
      )}

      {changes.length > 0 && (
        <div className="fr-savebar">
          <span>
            <b>{changes.length}</b> unsaved change{changes.length === 1 ? "" : "s"}
            {errCount > 0 && <span className="fr-err"> · {errCount} invalid</span>}
          </span>
          <div className="fr-row">
            <button type="button" className="admin-button-secondary" onClick={() => setValues(Object.fromEntries(items.map((i) => [i.key, i.value])))}>Discard</button>
            <button type="button" className="admin-button" disabled={errCount > 0} onClick={openConfirm}>Review & save</button>
          </div>
        </div>
      )}

      {confirm && (
        <div className="fr-modal-back" onMouseDown={(e) => e.target === e.currentTarget && !saving && setConfirm(false)}>
          <div className="fr-modal" style={{ maxWidth: 640 }} role="dialog" aria-modal="true" aria-label="Confirm policy changes">
            <header><h2>Confirm policy changes</h2><p>{changes.length} parameter{changes.length === 1 ? "" : "s"} will change. This is written to the audit log.</p></header>
            <form className="fr-body" onSubmit={save}>
              {formErr && <div className="auth-message error">{formErr}</div>}
              <div className="table-wrap" style={{ maxHeight: 240, overflow: "auto" }}>
                <table className="fr-diff">
                  <thead><tr><th>Parameter</th><th>Current</th><th>New</th></tr></thead>
                  <tbody>{changes.map((c) => <tr key={c.key}><td><code>{c.key}</code></td><td className="old">{c.oldValue}</td><td className="new">{c.newValue}</td></tr>)}</tbody>
                </table>
              </div>
              {switchingToEnforce && (
                <div className="fr-info fr-info--warn"><b>Switching to ENFORCE</b>: the engine will start blocking, challenging and escalating real customers instead of only logging.</div>
              )}
              <div className="fr-field">
                <span className="fr-label">Reason for change <small>{reason.trim().length}/{MAX_REASON} · min {MIN_REASON}</small></span>
                <textarea className="fr-area" maxLength={MAX_REASON} value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Why are these values changing?" />
              </div>
              <div className="fr-row end">
                <button type="button" className="admin-button-secondary" disabled={saving} onClick={() => setConfirm(false)}>Back</button>
                <button type="submit" className="admin-button" disabled={saving}>{saving ? "Saving..." : "Apply changes"}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </Shell>
  );
}
