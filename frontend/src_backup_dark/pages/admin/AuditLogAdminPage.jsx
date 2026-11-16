import { useEffect, useState } from "react";
import auditLogApi from "../../api/auditLogApi";
import AdminShell from "./AdminShell";

import "./fraudRisk.css";

const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");

const ACTOR_TYPES = ["", "user", "admin", "system"];

const EMPTY_FILTERS = {
  userId: "",
  actorType: "",
  action: "",
  entityType: "",
  entityId: "",
  from: "",
  to: "",
};

// Reads the backend's { message } error even when the response came back as a
// Blob (export requests use responseType: "blob", so axios can't auto-parse JSON).
async function errMsg(e, fallback) {
  const data = e.response?.data;
  if (data instanceof Blob) {
    try {
      const text = await data.text();
      const parsed = JSON.parse(text);
      return parsed.message || fallback;
    } catch {
      return fallback;
    }
  }
  return data?.message || fallback;
}

function JsonCell({ value }) {
  if (!value) return <span>—</span>;
  let pretty = value;
  try {
    pretty = JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    // not JSON, show as-is
  }
  return (
    <details>
      <summary style={{ cursor: "pointer", color: "var(--kyc-accent, #c026d3)" }}>View</summary>
      <pre style={{ whiteSpace: "pre-wrap", fontSize: 12, margin: "6px 0 0", maxWidth: 320 }}>{pretty}</pre>
    </details>
  );
}

export default function AuditLogAdminPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [applied, setApplied] = useState(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [exporting, setExporting] = useState(false);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await auditLogApi.search({ ...applied, page, pageSize });
      const data = res.data || {};
      setItems(data.items || []);
      setTotal(data.totalCount ?? (data.items || []).length);
    } catch (e) {
      setError(await errMsg(e, "Failed to load activity logs."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [applied, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const applyFilters = (e) => {
    e.preventDefault();
    setPage(1);
    setApplied(filters);
  };

  const resetFilters = () => {
    setFilters(EMPTY_FILTERS);
    setPage(1);
    setApplied(EMPTY_FILTERS);
  };

  const setF = (key, value) => setFilters((f) => ({ ...f, [key]: value }));

  const handleExport = async () => {
    if (!filters.from || !filters.to) {
      setError("Choose a 'From' and 'To' date before exporting.");
      return;
    }
    setExporting(true);
    setError("");
    try {
      const res = await auditLogApi.export(filters, "csv");
      const url = URL.createObjectURL(res.data);
      const a = document.createElement("a");
      a.href = url;
      a.download = `audit-logs_${filters.from}_${filters.to}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      setError(await errMsg(e, "Failed to export audit logs."));
    } finally {
      setExporting(false);
    }
  };

  return (
    <AdminShell title="Activity Log">
      <div className="fr-page">
        {error && (
          <div className="auth-message error">
            {error}
            <button type="button" onClick={() => setError("")}>Dismiss</button>
          </div>
        )}

        <section className="admin-panel">
          <div className="panel-header">
            <h2>Activity log</h2>
            <div style={{ display: "flex", gap: 8 }}>
              <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
              <button type="button" className="admin-button" onClick={handleExport} disabled={exporting}>
                {exporting ? "Exporting..." : "Export CSV"}
              </button>
            </div>
          </div>
          <p className="kyc-admin-note">
            Read-only. Every significant action taken by an admin, a user, or the system across the platform
            (logins, approvals, deletions, configuration changes...) is recorded here. Exporting requires a
            "From" and "To" date so the file stays a manageable size.
          </p>

          <form className="kyc-admin-toolbar" onSubmit={applyFilters}>
            <input
              inputMode="numeric"
              placeholder="User ID"
              value={filters.userId}
              onChange={(e) => setF("userId", e.target.value)}
            />
            <select value={filters.actorType} onChange={(e) => setF("actorType", e.target.value)}>
              {ACTOR_TYPES.map((t) => <option key={t} value={t}>{t || "Any actor type"}</option>)}
            </select>
            <input
              placeholder="Action (e.g. DELETION_APPROVED)"
              value={filters.action}
              onChange={(e) => setF("action", e.target.value)}
            />
            <input
              placeholder="Entity type (e.g. KycConsentVersion)"
              value={filters.entityType}
              onChange={(e) => setF("entityType", e.target.value)}
            />
            <input
              placeholder="Entity ID"
              value={filters.entityId}
              onChange={(e) => setF("entityId", e.target.value)}
            />
            <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 13 }}>
              From <input type="date" value={filters.from} onChange={(e) => setF("from", e.target.value)} />
            </label>
            <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 13 }}>
              To <input type="date" value={filters.to} onChange={(e) => setF("to", e.target.value)} />
            </label>
            <button type="submit" className="admin-button">Search</button>
            <button type="button" className="admin-button-secondary" onClick={resetFilters}>Reset</button>
          </form>

          {loading ? (
            <div className="empty-state">Loading...</div>
          ) : items.length === 0 ? (
            <div className="empty-state">No log entries found.</div>
          ) : (
            <div className="table-wrap">
              <table className="user-table">
                <thead>
                  <tr>
                    <th>Time</th><th>User</th><th>Actor type</th><th>Action</th>
                    <th>Entity</th><th>Old value</th><th>New value</th><th>IP</th><th>Request ID</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((log) => (
                    <tr key={log.auditLogId}>
                      <td>{fmt(log.createdAt)}</td>
                      <td>{log.userId != null ? `#${log.userId}` : "—"}</td>
                      <td>
                        <span className={`kyc-pill kyc-pill--${log.actorType === "system" ? "muted" : "view"}`}>
                          {log.actorType}
                        </span>
                      </td>
                      <td>{log.action}</td>
                      <td>
                        {log.entityType}
                        {log.entityId ? ` #${log.entityId}` : ""}
                      </td>
                      <td><JsonCell value={log.oldValue} /></td>
                      <td><JsonCell value={log.newValue} /></td>
                      <td>{log.ipAddress || "—"}</td>
                      <td>{log.requestId || "—"}</td>
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
      </div>
    </AdminShell>
  );
}
