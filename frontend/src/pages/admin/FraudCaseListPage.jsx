import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import riskApi from "../../api/riskApi";
import adminApi from "../../api/adminApi";
import { useAuth } from "../../context/AuthContext";
import { Shell, Banner, fmt, errMsg, levelPill, statusPill, slaInfo } from "./frCommon";

// UC_18.8 View Fraud Case List · UC_18.9 Assign Fraud Case to Analyst · UC_18.10 Resolve Fraud Case
const LEVELS = ["", "Critical", "High", "Medium", "Low"];
const TYPES = ["", "Bot", "Fraud"];
const STATUSES = ["", "Open", "InReview", "Resolved", "FalsePositive", "Dismissed"];
const EMPTY = { keyword: "", status: "", riskLevel: "", alertType: "", from: "", to: "", sortBy: "createdAt", desc: true };
const TABS = [
  ["all", "All cases"],
  ["unassigned", "Unassigned"],
  ["mine", "Assigned to me"],
  ["overdue", "SLA overdue"],
];
const OUTCOMES = [
  ["Confirmed", "Confirmed fraud", "The alert is real. The case is closed as Resolved."],
  ["FalsePositive", "False positive", "The activity was legitimate. Use this to tune the risk rules."],
  ["Dismissed", "Dismissed", "No action needed (duplicate, test or low value)."],
];
const MIN_NOTE = 10;
const MAX_NOTE = 500;
const isOpen = (c) => c.status === "Open" || c.status === "InReview";
const roleNames = (u) =>
  (u.roles?.length ? u.roles : u.roleName ? [u.roleName] : []).map((r) => String(r?.roleName || r).toLowerCase());

function Modal({ title, subtitle, onClose, children }) {
  return (
    <div className="fr-modal-back" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div className="fr-modal" role="dialog" aria-modal="true" aria-label={title}>
        <header><h2>{title}</h2>{subtitle && <p>{subtitle}</p>}</header>
        {children}
      </div>
    </div>
  );
}

function SlaCell({ c }) {
  if (!isOpen(c)) return <span className="fr-hint">—</span>;
  const info = slaInfo(c.slaDueAt);
  const high = c.riskLevel === "High" || c.riskLevel === "Critical";
  if (!high) return <span className="fr-hint">Not tracked</span>;
  return <span className={`kyc-pill ${c.isOverdue ? "kyc-pill--bad" : info?.cls}`}>{c.isOverdue ? "Overdue" : info?.text}</span>;
}

export default function FraudCaseListPage() {
  const { user } = useAuth();
  const myId = Number(user?.userId ?? user?.id) || null;

  const [tab, setTab] = useState("all");
  const [filters, setFilters] = useState(EMPTY);
  const [applied, setApplied] = useState(EMPTY);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [analysts, setAnalysts] = useState([]);
  const [analystsFailed, setAnalystsFailed] = useState(false);

  const [assign, setAssign] = useState(null);   // case being assigned
  const [assignee, setAssignee] = useState("");
  const [assignNote, setAssignNote] = useState("");
  const [resolve, setResolve] = useState(null); // case being resolved
  const [outcome, setOutcome] = useState("");
  const [resolveNote, setResolveNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [formErr, setFormErr] = useState("");

  // Analysts = Admin accounts (only Admin can handle fraud cases, see CanReviewFraud policy)
  useEffect(() => {
    adminApi.getUsers()
      .then((res) => {
        const list = Array.isArray(res.data) ? res.data : res.data?.items || [];
        setAnalysts(list.filter((u) => u.isActive !== false && roleNames(u).includes("admin")));
      })
      .catch(() => setAnalystsFailed(true));
  }, []);
  const nameOf = useMemo(() => {
    const m = new Map(analysts.map((u) => [Number(u.userId), u.fullName || u.email]));
    return (id) => (id == null ? null : m.get(Number(id)) || null);
  }, [analysts]);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await riskApi.searchCases({
        ...applied,
        unassigned: tab === "unassigned",
        overdueOnly: tab === "overdue",
        assignedTo: tab === "mine" ? myId : undefined,
        page,
        pageSize,
      });
      const data = res.data || {};
      setItems(data.items || []);
      setTotal(data.totalCount ?? (data.items || []).length);
    } catch (e) {
      setError(errMsg(e, "Failed to load fraud cases."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [applied, tab, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const setF = (k, v) => setFilters((f) => ({ ...f, [k]: v }));

  const submitFilters = (e) => {
    e.preventDefault();
    if (filters.keyword.trim() && !/^\d+$/.test(filters.keyword.trim())) { setError("Keyword must be a numeric case, user, order or ticket ID."); return; }
    if (filters.from && filters.to && filters.from > filters.to) { setError("The From date must be on or before the To date."); return; }
    setPage(1); setApplied(filters);
  };

  // ---- UC_18.9 Assign
  const openAssign = (c) => { setAssign(c); setAssignee(c.assignedTo ? String(c.assignedTo) : ""); setAssignNote(""); setFormErr(""); };
  const submitAssign = async (e) => {
    e.preventDefault();
    const id = Number(assignee);
    if (!Number.isInteger(id) || id < 1) return setFormErr("Choose an analyst.");
    if (id === assign.assignedTo) return setFormErr("The case is already assigned to this analyst.");
    setBusy(true); setFormErr("");
    try {
      await riskApi.assignCase(assign.caseId, id, assignNote.trim());
      setNotice(`Case #${assign.caseId} assigned to ${nameOf(id) || `#${id}`}. Status is now InReview.`);
      setAssign(null);
      await load();
    } catch (e2) {
      setFormErr(errMsg(e2, "Failed to assign the case."));
    } finally { setBusy(false); }
  };

  // ---- UC_18.10 Resolve
  const openResolve = (c) => { setResolve(c); setOutcome(""); setResolveNote(""); setFormErr(""); };
  const submitResolve = async (e) => {
    e.preventDefault();
    const note = resolveNote.trim();
    if (!outcome) return setFormErr("Select an outcome.");
    if (note.length < MIN_NOTE || note.length > MAX_NOTE) return setFormErr(`The resolution note must be ${MIN_NOTE}-${MAX_NOTE} characters.`);
    setBusy(true); setFormErr("");
    try {
      const res = await riskApi.resolveCase(resolve.caseId, outcome, note);
      setNotice(`Case #${resolve.caseId} closed as ${res.data?.status || outcome}.`);
      setResolve(null);
      await load();
    } catch (e2) {
      setFormErr(errMsg(e2, "Failed to resolve the case."));
    } finally { setBusy(false); }
  };

  const resolveBlock = (c) => (c.assignedTo != null && c.assignedTo !== myId
    ? `Only the assigned analyst (${nameOf(c.assignedTo) || `#${c.assignedTo}`}) can resolve this case.` : "");

  return (
    <Shell title="Fraud Cases">
      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      <div className="fr-top">
        <div>
          <h1>Fraud cases</h1>
          <p>Work queue for fraud / bot alerts. Assign a case to an analyst, then resolve it with an outcome.</p>
        </div>
        <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
      </div>

      <section className="admin-panel">
        <div className="panel-header">
          <h2>Cases</h2>
          <div className="fr-seg" role="tablist">
            {TABS.map(([k, label]) => (
              <button key={k} type="button" role="tab" aria-selected={tab === k} className={tab === k ? "on" : ""}
                onClick={() => { setTab(k); setPage(1); }}>{label}</button>
            ))}
          </div>
        </div>

        <form className="kyc-admin-toolbar" onSubmit={submitFilters}>
          <input inputMode="numeric" placeholder="Case / user / order / ticket ID" value={filters.keyword}
            onChange={(e) => setF("keyword", e.target.value)} style={{ width: 230 }} />
          <select value={filters.status} onChange={(e) => setF("status", e.target.value)}>
            {STATUSES.map((v) => <option key={v} value={v}>{v || "All statuses"}</option>)}
          </select>
          <select value={filters.riskLevel} onChange={(e) => setF("riskLevel", e.target.value)}>
            {LEVELS.map((v) => <option key={v} value={v}>{v || "All risk levels"}</option>)}
          </select>
          <select value={filters.alertType} onChange={(e) => setF("alertType", e.target.value)}>
            {TYPES.map((v) => <option key={v} value={v}>{v || "All types"}</option>)}
          </select>
          <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 13 }}>From
            <input type="date" value={filters.from} onChange={(e) => setF("from", e.target.value)} /></label>
          <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 13 }}>To
            <input type="date" value={filters.to} onChange={(e) => setF("to", e.target.value)} /></label>
          <select value={`${filters.sortBy}:${filters.desc}`}
            onChange={(e) => { const [s, d] = e.target.value.split(":"); setFilters((f) => ({ ...f, sortBy: s, desc: d === "true" })); }}>
            <option value="createdAt:true">Newest first</option>
            <option value="createdAt:false">Oldest first</option>
            <option value="riskScore:true">Highest score</option>
            <option value="riskScore:false">Lowest score</option>
          </select>
          <button type="submit" className="admin-button">Search</button>
          <button type="button" className="admin-button-secondary" onClick={() => { setFilters(EMPTY); setPage(1); setApplied(EMPTY); }}>Reset</button>
        </form>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : items.length === 0 ? (
          <div className="empty-state">No cases match the current filters.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>Case</th><th>User</th><th>Type</th><th>Risk</th><th>Status</th>
                  <th>Assignee</th><th>SLA</th><th>Created</th><th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((c) => {
                  const blocked = resolveBlock(c);
                  return (
                    <tr key={c.caseId} className={c.isOverdue ? "fr-overdue" : ""}>
                      <td><Link to={`/admin/fraud-alerts/${c.caseId}`}>#{c.caseId}</Link></td>
                      <td>{c.userId != null ? `#${c.userId}` : "—"}</td>
                      <td>{c.alertType}</td>
                      <td>
                        <span className={`kyc-pill ${levelPill(c.riskLevel)}`}>{c.riskLevel}</span>{" "}
                        <span style={{ opacity: .7, fontSize: 12 }}>{c.riskScore}</span>
                      </td>
                      <td><span className={`kyc-pill ${statusPill(c.status)}`}>{c.status}</span></td>
                      <td>
                        {c.assignedTo == null ? <span className="fr-hint">Unassigned</span> : (
                          <>
                            {nameOf(c.assignedTo) || `#${c.assignedTo}`}{c.assignedTo === myId && <span className="fr-hint"> (you)</span>}
                            {c.assignedAt && <div className="fr-hint">{fmt(c.assignedAt)}</div>}
                          </>
                        )}
                      </td>
                      <td><SlaCell c={c} /></td>
                      <td>{fmt(c.createdAt)}{c.reviewedAt && <div className="fr-hint">Closed {fmt(c.reviewedAt)}</div>}</td>
                      <td>
                        <div className="fr-row">
                          <Link className="admin-button-secondary" to={`/admin/fraud-alerts/${c.caseId}`}>View</Link>
                          {isOpen(c) && (
                            <>
                              <button type="button" className="admin-button-secondary" onClick={() => openAssign(c)}>
                                {c.assignedTo == null ? "Assign" : "Reassign"}
                              </button>
                              <button type="button" className="admin-button" disabled={!!blocked} title={blocked} onClick={() => openResolve(c)}>Resolve</button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        <div className="kyc-pager">
          <button type="button" className="admin-button-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span>Page {page} / {totalPages} · {total} case{total === 1 ? "" : "s"}</span>
          <button type="button" className="admin-button-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </section>

      {assign && (
        <Modal title={`Assign case #${assign.caseId}`} subtitle={`${assign.riskLevel} · ${assign.alertType} · score ${assign.riskScore}`} onClose={() => !busy && setAssign(null)}>
          <form className="fr-body" onSubmit={submitAssign}>
            {formErr && <div className="auth-message error">{formErr}</div>}
            <div className="fr-field">
              <span className="fr-label">Analyst</span>
              {analystsFailed || analysts.length === 0 ? (
                <input inputMode="numeric" placeholder="Analyst user ID" value={assignee} onChange={(e) => setAssignee(e.target.value)} />
              ) : (
                <select value={assignee} onChange={(e) => setAssignee(e.target.value)}>
                  <option value="">Select an analyst…</option>
                  {analysts.map((u) => (
                    <option key={u.userId} value={u.userId}>{u.fullName || u.email} (#{u.userId}){Number(u.userId) === myId ? " — me" : ""}</option>
                  ))}
                </select>
              )}
              {analystsFailed && <span className="fr-hint">Could not load the analyst list. Enter the user ID manually.</span>}
            </div>
            <div className="fr-field">
              <span className="fr-label">Note <small>{assignNote.length}/500 · optional</small></span>
              <textarea className="fr-area" maxLength={500} value={assignNote} onChange={(e) => setAssignNote(e.target.value)} placeholder="Context for the analyst" />
            </div>
            <div className="fr-info">Assigning moves the case to <b>InReview</b>. Only the assigned analyst can resolve it afterwards.</div>
            <div className="fr-row end">
              <button type="button" className="admin-button-secondary" disabled={busy} onClick={() => setAssign(null)}>Cancel</button>
              <button type="submit" className="admin-button" disabled={busy}>{busy ? "Assigning..." : "Assign case"}</button>
            </div>
          </form>
        </Modal>
      )}

      {resolve && (
        <Modal title={`Resolve case #${resolve.caseId}`} subtitle={`${resolve.riskLevel} · ${resolve.alertType} · ${resolve.userId != null ? `user #${resolve.userId}` : "no user"}`} onClose={() => !busy && setResolve(null)}>
          <form className="fr-body" onSubmit={submitResolve}>
            {formErr && <div className="auth-message error">{formErr}</div>}
            <div className="fr-field">
              <span className="fr-label">Outcome</span>
              <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {OUTCOMES.map(([v, label, hint]) => (
                  <button key={v} type="button" className={`fr-radio${outcome === v ? " on" : ""}`} onClick={() => setOutcome(v)} role="radio" aria-checked={outcome === v}>
                    <span className="dot" /><span><b>{label}</b><p>{hint}</p></span>
                  </button>
                ))}
              </div>
            </div>
            <div className="fr-field">
              <span className="fr-label">Resolution note <small>{resolveNote.trim().length}/{MAX_NOTE} · min {MIN_NOTE}</small></span>
              <textarea className={`fr-area${formErr.includes("note") ? " is-err" : ""}`} maxLength={MAX_NOTE} value={resolveNote} onChange={(e) => setResolveNote(e.target.value)} placeholder="What did you check and why this outcome?" />
            </div>
            <div className="fr-info fr-info--warn">
              A case with a <b>pending appeal</b> cannot be resolved until the appeal is decided. Resolving does not block or restore the account; use Risk Blocks for that.
            </div>
            <div className="fr-row end">
              <button type="button" className="admin-button-secondary" disabled={busy} onClick={() => setResolve(null)}>Cancel</button>
              <button type="submit" className="admin-button" disabled={busy}>{busy ? "Saving..." : "Resolve case"}</button>
            </div>
          </form>
        </Modal>
      )}
    </Shell>
  );
}
