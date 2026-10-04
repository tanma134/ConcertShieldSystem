import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import riskApi from "../../api/riskApi";
import { Shell, Banner, fmt, errMsg, statusPill } from "./frCommon";

const STATUSES = ["", "Active", "Restored", "Overturned"];
const SCOPES = ["", "ACCOUNT", "TICKET", "SESSION", "DEVICE", "IP"];
const EMPTY = { status: "Active", scope: "", userId: "", fraudAlertId: "" };
const NEW_BLOCK = { scope: "ACCOUNT", userId: "", ticketId: "", durationHours: 24, reason: "" };
const MIN_REASON = 10;
const MAX_REASON = 500;

// Risk blocks: list, manual Block (account or ticket) and Restore. Admin only (BR-111, BR-245).
// Automatic blocks (SESSION / DEVICE / IP) are created by the risk engine and can only be restored here.
export default function RiskBlocksPage() {
  const [filters, setFilters] = useState(EMPTY);
  const [applied, setApplied] = useState(EMPTY);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");

  const [blockOpen, setBlockOpen] = useState(false);
  const [form, setForm] = useState(NEW_BLOCK);
  const [saving, setSaving] = useState(false);

  const [restoreId, setRestoreId] = useState(null);
  const [restoreReason, setRestoreReason] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await riskApi.searchBlocks({ ...applied, page, pageSize });
      const data = res.data || {};
      setItems(data.items || []);
      setTotal(data.totalCount ?? (data.items || []).length);
    } catch (e) {
      setError(errMsg(e, "Failed to load blocks."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [applied, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const setF = (k, v) => setFilters((f) => ({ ...f, [k]: v }));
  const setN = (k, v) => setForm((f) => ({ ...f, [k]: v }));

  const submitBlock = async (e) => {
    e.preventDefault();
    const reason = form.reason.trim();
    const hours = Number(form.durationHours);
    const isTicket = form.scope === "TICKET";
    if (!form.userId.trim()) { setError("Enter the User ID: the customer is notified and can appeal."); return; }
    if (isTicket && !form.ticketId.trim()) { setError("Enter the Ticket ID to block."); return; }
    if (!Number.isInteger(hours) || hours < 1 || hours > 720) { setError("Duration must be a whole number of hours between 1 and 720."); return; }
    if (reason.length < MIN_REASON || reason.length > MAX_REASON) { setError(`The reason must be ${MIN_REASON}-${MAX_REASON} characters.`); return; }
    setSaving(true);
    setError("");
    try {
      const res = await riskApi.createBlock({
        scope: form.scope,
        userId: Number(form.userId),
        ticketId: isTicket ? Number(form.ticketId) : undefined,
        durationHours: hours,
        reason,
      });
      setNotice(`Block ${res.data?.decisionCode || ""} created. The customer has been notified and can appeal within 7 days.`);
      setBlockOpen(false);
      setForm(NEW_BLOCK);
      await load();
    } catch (e2) {
      setError(errMsg(e2, "Failed to create block. Please check the information."));
    } finally {
      setSaving(false);
    }
  };

  const restore = async (id) => {
    const text = restoreReason.trim();
    if (text.length < MIN_REASON || text.length > MAX_REASON) {
      setError(`The restore reason must be ${MIN_REASON}-${MAX_REASON} characters.`);
      return;
    }
    setError("");
    try {
      await riskApi.restoreBlock(id, text);
      setRestoreId(null);
      setRestoreReason("");
      setNotice("Restored. The customer has been notified.");
      await load();
    } catch (e) {
      setError(errMsg(e, "Failed to restore. It may have already been restored."));
    }
  };

  return (
    <Shell title="Risk Blocks">
      <Banner error={error} notice={notice} onClearError={() => setError("")} onClearNotice={() => setNotice("")} />

      <div className="fr-top">
        <div>
          <h1>Risk blocks</h1>
          <p>Active and past blocks. Block an account or a ticket manually, or restore a block.</p>
        </div>
        <div className="fr-row">
          <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
          <button type="button" className="admin-button" onClick={() => setBlockOpen((v) => !v)}>
            {blockOpen ? "Close" : "New manual block"}
          </button>
        </div>
      </div>

      {blockOpen && (
        <section className="admin-panel">
          <div className="panel-header"><h2>Block an account or a ticket</h2></div>
          <form className="fr-body" onSubmit={submitBlock}>
            <div className="kyc-admin-fields">
              <label>
                Scope
                <select value={form.scope} onChange={(e) => setN("scope", e.target.value)}>
                  <option value="ACCOUNT">ACCOUNT (end all sessions, lock account)</option>
                  <option value="TICKET">TICKET (ticket cannot be used for entry)</option>
                </select>
              </label>
              <label>
                User ID (owner)
                <input inputMode="numeric" value={form.userId} onChange={(e) => setN("userId", e.target.value)} />
              </label>
              {form.scope === "TICKET" && (
                <label>
                  Ticket ID
                  <input inputMode="numeric" value={form.ticketId} onChange={(e) => setN("ticketId", e.target.value)} />
                </label>
              )}
              <label>
                Duration (hours, 1-720)
                <input type="number" min={1} max={720} value={form.durationHours} onChange={(e) => setN("durationHours", e.target.value)} />
              </label>
              <label>
                Reason
                <textarea rows={3} maxLength={MAX_REASON} value={form.reason}
                  placeholder="Fraud-risk evidence for this block (10-500 characters)"
                  onChange={(e) => setN("reason", e.target.value)} />
              </label>
            </div>
            <p className="kyc-admin-note">
              You cannot block your own account, and a target that is already blocked cannot be blocked again.
              The customer is notified and can appeal within 7 days.
            </p>
            <div className="fr-row end">
              <button type="button" className="admin-button-secondary" onClick={() => setBlockOpen(false)}>Cancel</button>
              <button type="submit" className="admin-button" disabled={saving}>{saving ? "Processing..." : "Confirm block"}</button>
            </div>
          </form>
        </section>
      )}

      <section className="admin-panel">
        <div className="panel-header"><h2>Blocks</h2></div>

        <form className="kyc-admin-toolbar" onSubmit={(e) => { e.preventDefault(); setPage(1); setApplied(filters); }}>
          <select value={filters.status} onChange={(e) => setF("status", e.target.value)}>
            {STATUSES.map((s) => <option key={s} value={s}>{s || "All statuses"}</option>)}
          </select>
          <select value={filters.scope} onChange={(e) => setF("scope", e.target.value)}>
            {SCOPES.map((s) => <option key={s} value={s}>{s || "All scopes"}</option>)}
          </select>
          <input inputMode="numeric" placeholder="User ID" value={filters.userId} onChange={(e) => setF("userId", e.target.value)} />
          <input inputMode="numeric" placeholder="Alert ID" value={filters.fraudAlertId} onChange={(e) => setF("fraudAlertId", e.target.value)} />
          <button type="submit" className="admin-button">Search</button>
          <button type="button" className="admin-button-secondary" onClick={() => { setFilters(EMPTY); setPage(1); setApplied(EMPTY); }}>Reset</button>
        </form>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : items.length === 0 ? (
          <div className="empty-state">No blocks match the current filters.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>Code</th><th>Scope</th><th>User</th><th>Alert</th><th>Status</th>
                  <th>Decided by</th><th>Expires</th><th>Created</th><th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((b) => (
                  <tr key={b.riskDecisionId}>
                    <td style={{ fontFamily: "monospace", fontSize: 12 }}>{b.decisionCode}</td>
                    <td>{b.scope || "—"}</td>
                    <td>{b.userId != null ? `#${b.userId}` : "—"}</td>
                    <td>
                      {b.fraudAlertId != null
                        ? <Link to={`/admin/fraud-alerts/${b.fraudAlertId}`}>#{b.fraudAlertId}</Link>
                        : "—"}
                    </td>
                    <td>
                      <span className={`kyc-pill ${statusPill(b.status)}`}>{b.status}</span>
                      {b.isShadow && <span className="kyc-pill kyc-pill--muted" style={{ marginLeft: 6 }}>Shadow</span>}
                    </td>
                    <td>{b.decidedByType === "STAFF" ? `Admin #${b.decidedBy}` : "System"}</td>
                    <td>{fmt(b.expiresAt)}</td>
                    <td>{fmt(b.createdAt)}</td>
                    <td>
                      <div className="fr-row">
                        <Link className="admin-button-secondary" to={`/admin/risk/decisions/${b.riskDecisionId}`}>Details</Link>
                        {b.status === "Active" && !b.isShadow && (
                          restoreId === b.riskDecisionId ? (
                            <>
                              <input placeholder="Restore reason (10-500 characters)" value={restoreReason}
                                onChange={(e) => setRestoreReason(e.target.value)} style={{ minWidth: 200 }} />
                              <button type="button" className="admin-button" onClick={() => restore(b.riskDecisionId)}>Confirm</button>
                              <button type="button" className="admin-button-secondary" onClick={() => { setRestoreId(null); setRestoreReason(""); }}>Cancel</button>
                            </>
                          ) : (
                            <button type="button" className="admin-button-secondary" onClick={() => setRestoreId(b.riskDecisionId)}>Restore</button>
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

        <div className="kyc-pager">
          <button type="button" className="admin-button-secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Prev</button>
          <span>Page {page} / {totalPages}</span>
          <button type="button" className="admin-button-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </section>
    </Shell>
  );
}
