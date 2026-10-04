import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import riskApi from "../../api/riskApi";
import AdminShell from "./AdminShell";

import "./fraudRisk.css";

// Bọc mọi nội dung trong .fr-page để fraudRisk.css được áp dụng
const Shell = ({ title, children }) => (
  <AdminShell title={title}>
    <div className="fr-page">{children}</div>
  </AdminShell>
);

const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");

const LEVELS = ["", "Critical", "High", "Medium", "Low"];
const TYPES = ["", "Bot", "Fraud"];
const STATUSES = ["", "Open", "InReview", "Resolved", "FalsePositive", "Dismissed"];

const EMPTY_FILTERS = { riskLevel: "", alertType: "", status: "", userId: "", from: "", to: "" };

function errMsg(e, fallback) {
  return e.response?.data?.message || fallback;
}

// "High" -> "kyc-pill--bad" (red), "Medium" -> "pending" (yellow), "Low" -> "ok" (green)...
// Reuses the existing kyc-pill palette; no new colors.
function levelPillClass(level) {
  switch (level) {
    case "Critical":
    case "High":
      return "kyc-pill--bad";
    case "Medium":
      return "kyc-pill--pending";
    case "Low":
      return "kyc-pill--ok";
    default:
      return "kyc-pill--muted";
  }
}

function statusPillClass(status) {
  switch (status) {
    case "Open":
      return "kyc-pill--bad";
    case "InReview":
      return "kyc-pill--pending";
    case "Resolved":
    case "FalsePositive":
      return "kyc-pill--ok";
    default:
      return "kyc-pill--muted";
  }
}

// BR-240: High/Critical alerts must be handled within the SLA (risk.alert.sla_hours, default 2 h).
// Keep this constant in sync with that setting.
const ALERT_SLA_HOURS = 2;
const isOverdue = (a) =>
  (a.status === "Open" || a.status === "InReview") &&
  (a.riskLevel === "High" || a.riskLevel === "Critical") &&
  Date.now() - new Date(a.createdAt).getTime() > ALERT_SLA_HOURS * 36e5;

function SummaryCard({ label, value, tone }) {
  return (
    <div className={`kyc-item-editor`} style={{ minWidth: 160 }}>
      <div className="kyc-admin-note" style={{ margin: 0 }}>{label}</div>
      <div style={{ fontSize: 28, fontWeight: 700, marginTop: 4, color: tone }}>{value}</div>
    </div>
  );
}

export default function FraudAlertDashboardPage() {
  const [filters, setFilters] = useState(EMPTY_FILTERS);
  const [applied, setApplied] = useState(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [summary, setSummary] = useState(null);
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const [summaryRes, listRes] = await Promise.all([
        riskApi.getSummary({ from: applied.from, to: applied.to }),
        riskApi.search({ ...applied, page, pageSize }),
      ]);
      setSummary(summaryRes.data);
      const data = listRes.data || {};
      setItems(data.items || []);
      setTotal(data.totalCount ?? (data.items || []).length);
    } catch (e) {
      setError(errMsg(e, "Failed to load fraud alert data."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [applied, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const applyFilters = (e) => {
    e.preventDefault();
    if (filters.from && filters.to && filters.from > filters.to) {
      setError("The From date must be on or before the To date.");
      return;
    }
    setPage(1);
    setApplied(filters);
  };

  const resetFilters = () => {
    setFilters(EMPTY_FILTERS);
    setPage(1);
    setApplied(EMPTY_FILTERS);
  };

  const setF = (key, value) => setFilters((f) => ({ ...f, [key]: value }));

  const countOf = (list, key) => list?.find((x) => x.key === key)?.count ?? 0;

  return (
    <Shell title="Fraud Alert Dashboard">
      {error && (
        <div className="auth-message error">
          {error}
          <button type="button" onClick={() => setError("")}>Dismiss</button>
        </div>
      )}

      {/* UC_18.1 - View Fraud Alert Dashboard */}
      <section className="admin-panel">
        <div className="panel-header">
          <h2>Fraud alert dashboard</h2>
          <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
        </div>
        <p className="kyc-admin-note">
          Overview of fraud / bot alerts detected by the Risk Engine. Open an alert to view its details,
          block, or restore.
        </p>

        {summary && (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 12, marginBottom: 20 }}>
            <SummaryCard label="Total alerts" value={summary.total} />
            <SummaryCard label="Open High/Critical" value={summary.openHighOrCritical} tone="#fca5a5" />
            <SummaryCard label="SLA overdue" value={summary.overdueSla} tone="#fbbf24" />
            <SummaryCard label="Critical" value={countOf(summary.byLevel, "Critical")} tone="#fca5a5" />
            <SummaryCard label="High" value={countOf(summary.byLevel, "High")} tone="#fb923c" />
            <SummaryCard label="Bot" value={countOf(summary.byType, "Bot")} />
            <SummaryCard label="Fraud" value={countOf(summary.byType, "Fraud")} />
          </div>
        )}

        <form className="kyc-admin-toolbar" onSubmit={applyFilters}>
          <select value={filters.riskLevel} onChange={(e) => setF("riskLevel", e.target.value)}>
            {LEVELS.map((v) => <option key={v} value={v}>{v || "All risk levels"}</option>)}
          </select>
          <select value={filters.alertType} onChange={(e) => setF("alertType", e.target.value)}>
            {TYPES.map((v) => <option key={v} value={v}>{v || "All types"}</option>)}
          </select>
          <select value={filters.status} onChange={(e) => setF("status", e.target.value)}>
            {STATUSES.map((v) => <option key={v} value={v}>{v || "All statuses"}</option>)}
          </select>
          <input
            inputMode="numeric"
            placeholder="User ID"
            value={filters.userId}
            onChange={(e) => setF("userId", e.target.value)}
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
          <div className="empty-state">No alerts match the current filters.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>Time</th><th>Type</th><th>Risk level</th><th>Score</th>
                  <th>User</th><th>Status</th><th>SLA</th><th>Reviewed by</th><th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((a) => (
                  <tr key={a.fraudAlertId}>
                    <td>{fmt(a.createdAt)}</td>
                    <td>{a.alertType}</td>
                    <td><span className={`kyc-pill ${levelPillClass(a.riskLevel)}`}>{a.riskLevel}</span></td>
                    <td>{a.riskScore} <span style={{ opacity: .6, fontSize: 12 }}>(bot {a.botScore} / fraud {a.fraudScore})</span></td>
                    <td>{a.userId != null ? `#${a.userId}` : "—"}</td>
                    <td><span className={`kyc-pill ${statusPillClass(a.status)}`}>{a.status}</span></td>
                    <td>{isOverdue(a) ? <span className="kyc-pill kyc-pill--bad">Overdue</span> : "—"}</td>
                    <td>{a.reviewedBy != null ? `#${a.reviewedBy}` : "—"}</td>
                    <td>
                      <Link className="admin-button-secondary" to={`/admin/fraud-alerts/${a.fraudAlertId}`}>
                        View
                      </Link>
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
