import { useEffect, useState } from "react";
import kycAdminApi from "../../api/kycAdminApi";
import AdminShell from "./AdminShell";
import "./kycAdmin.css";

const errMsg = (e, fallback) => e.response?.data?.message || fallback;
const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");
const ACTOR_TYPES = ["", "User", "Admin", "System"];

const EMPTY_FILTERS = { subjectUserId: "", actorUserId: "", actorType: "", action: "", from: "", to: "" };

// Human-readable label + short explanation for each action code the log can contain.
// Unknown codes fall back to showing the raw code, so a new action added on the
// backend never breaks this page — it just won't have a friendly label yet.
const ACTION_INFO = {
  VIEW_IMAGE: { label: "Viewed image", hint: "Admin opened an original ID/selfie image.", tone: "view" },
  CONSENT_GIVEN: { label: "Consent given", hint: "User accepted the eKYC consent notice.", tone: "ok" },
  DELETION_REQUESTED: { label: "Deletion requested", hint: "User asked to delete their eKYC data.", tone: "pending" },
  DELETION_APPROVED: { label: "Deletion approved", hint: "Admin approved a deletion request.", tone: "ok" },
  DELETION_REJECTED: { label: "Deletion rejected", hint: "Admin rejected a deletion request.", tone: "bad" },
  DATA_PURGED: { label: "Data purged", hint: "An eKYC record's data/files were permanently deleted.", tone: "bad" },
  AUTO_PURGED: { label: "Auto-purged (retention)", hint: "Deleted automatically once the retention period expired.", tone: "bad" },
};

const actionInfo = (action) => ACTION_INFO[action] || { label: action, hint: "", tone: "muted" };

// "requestId=2, records=3" -> "Request #2 · 3 records"
function formatDetails(details) {
  if (!details) return "—";
  const pairs = details.split(",").map((p) => p.trim()).filter(Boolean);
  const parsed = pairs.map((pair) => {
    const [key, value] = pair.split("=").map((s) => s?.trim());
    if (!key || value === undefined) return pair;
    if (key === "requestId") return `Request #${value}`;
    if (key === "records") return `${value} record${value === "1" ? "" : "s"}`;
    if (key === "kind") return `Image: ${value}`;
    return `${key}: ${value}`;
  });
  return parsed.join(" · ");
}

export default function KycAccessLogAdminPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [applied, setApplied] = useState(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await kycAdminApi.searchAccessLogs({ ...applied, page, pageSize });
      const data = res.data || {};
      setItems(data.items || []);
      setTotal(data.total ?? (data.items || []).length);
    } catch (e) {
      setError(errMsg(e, "Failed to load access logs."));
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

  return (
    <AdminShell title="eKYC Access Logs">
      {error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}

      <section className="admin-panel">
        <div className="panel-header">
          <h2>Access log</h2>
          <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
        </div>
        <p className="kyc-admin-note">
          Read-only. Every time an admin or the system views or handles a user's eKYC data, it is recorded here.
          Entries sharing the same <strong>Request #</strong> belong to the same deletion request — e.g. one
          "Deletion approved" followed by several "Data purged" rows, one per deleted record.
        </p>

        <form className="kyc-admin-toolbar" onSubmit={applyFilters}>
          <input
            inputMode="numeric"
            placeholder="Subject user ID"
            value={filters.subjectUserId}
            onChange={(e) => setF("subjectUserId", e.target.value)}
          />
          <input
            inputMode="numeric"
            placeholder="Actor user ID"
            value={filters.actorUserId}
            onChange={(e) => setF("actorUserId", e.target.value)}
          />
          <select value={filters.actorType} onChange={(e) => setF("actorType", e.target.value)}>
            {ACTOR_TYPES.map((t) => <option key={t} value={t}>{t || "Any actor type"}</option>)}
          </select>
          <input
            placeholder="Action (e.g. VIEW_IMAGE)"
            value={filters.action}
            onChange={(e) => setF("action", e.target.value)}
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
                  <th>Time</th><th>Subject user</th><th>eKYC ID</th><th>Actor</th><th>Actor type</th>
                  <th>Action</th><th>IP</th><th>Details</th>
                </tr>
              </thead>
              <tbody>
                {items.map((log) => {
                  const info = actionInfo(log.action);
                  return (
                    <tr key={log.id}>
                      <td>{fmt(log.createdAt)}</td>
                      <td>#{log.subjectUserId}</td>
                      <td>{log.ekycId ?? "—"}</td>
                      <td>{log.actorUserId != null ? `#${log.actorUserId}` : "—"}</td>
                      <td>{log.actorType}</td>
                      <td>
                        <span className={`kyc-pill kyc-pill--${info.tone}`} title={info.hint || log.action}>
                          {info.label}
                        </span>
                      </td>
                      <td>{log.ipAddress || "—"}</td>
                      <td style={{ maxWidth: 260 }}>{formatDetails(log.details)}</td>
                    </tr>
                  );
                })}
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
    </AdminShell>
  );
}
