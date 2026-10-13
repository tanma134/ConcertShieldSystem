import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import appealApi from "../../api/appealApi";
import { Shell, Banner, fmt, errMsg, statusPill, slaInfo } from "./frCommon";

const STATUSES = ["", "Pending", "Accepted", "Rejected"];
const EMPTY = { status: "Pending", userId: "", overdueOnly: false };

// Appeal queue (BR-258: every appeal must be resolved within 48 hours of submission)
// GET api/admin/risk/appeals?status=&userId=&overdueOnly=&page=&pageSize=
export default function AppealAdminListPage() {
  const [filters, setFilters] = useState(EMPTY);
  const [applied, setApplied] = useState(EMPTY);
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
      const res = await appealApi.search({
        status: applied.status,
        userId: applied.userId.trim(),
        overdueOnly: applied.overdueOnly ? true : "",
        page,
        pageSize,
      });
      const data = res.data || {};
      setItems(data.items || []);
      setTotal(data.totalCount ?? (data.items || []).length);
    } catch (e) {
      setError(errMsg(e, "Failed to load appeals."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [applied, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const setF = (k, v) => setFilters((f) => ({ ...f, [k]: v }));

  const apply = (e) => {
    e.preventDefault();
    setPage(1);
    setApplied(filters);
  };
  const reset = () => {
    setFilters(EMPTY);
    setPage(1);
    setApplied(EMPTY);
  };

  return (
    <Shell title="Appeals">
      <Banner error={error} onClearError={() => setError("")} />

      <div className="fr-top">
        <div>
          <h1>Appeals</h1>
          <p>Customer appeals against block / hold decisions. Each one must be resolved within 48 hours.</p>
        </div>
        <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
      </div>

      <section className="admin-panel">
        <div className="panel-header"><h2>Appeal queue</h2></div>

        <form className="kyc-admin-toolbar" onSubmit={apply}>
          <select value={filters.status} onChange={(e) => setF("status", e.target.value)}>
            {STATUSES.map((s) => <option key={s} value={s}>{s || "All statuses"}</option>)}
          </select>
          <input inputMode="numeric" placeholder="User ID" value={filters.userId} onChange={(e) => setF("userId", e.target.value)} />
          <label style={{ display: "flex", gap: 6, alignItems: "center", fontSize: 13 }}>
            <input type="checkbox" checked={filters.overdueOnly} onChange={(e) => setF("overdueOnly", e.target.checked)} />
            Overdue only
          </label>
          <button type="submit" className="admin-button">Search</button>
          <button type="button" className="admin-button-secondary" onClick={reset}>Reset</button>
        </form>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : items.length === 0 ? (
          <div className="empty-state">No appeals match the current filters.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr>
                  <th>Appeal</th><th>Decision</th><th>User</th><th>Status</th>
                  <th>SLA (48 h)</th><th>Submitted</th><th></th>
                </tr>
              </thead>
              <tbody>
                {items.map((a) => {
                  const sla = a.status === "Pending" ? slaInfo(a.slaDueAt) : null;
                  return (
                    <tr key={a.riskAppealId}>
                      <td>#{a.riskAppealId}</td>
                      <td style={{ fontFamily: "monospace", fontSize: 12 }}>{a.decisionCode}</td>
                      <td>#{a.userId}</td>
                      <td><span className={`kyc-pill ${statusPill(a.status)}`}>{a.status}</span></td>
                      <td>
                        {sla
                          ? <span className={`kyc-pill ${a.isOverdue ? "kyc-pill--bad" : sla.cls}`}>{sla.text}</span>
                          : "—"}
                      </td>
                      <td>{fmt(a.createdAt)}</td>
                      <td>
                        <Link className="admin-button-secondary" to={`/admin/risk/appeals/${a.riskAppealId}`}>
                          {a.status === "Pending" ? "Review" : "View"}
                        </Link>
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
          <span>Page {page} / {totalPages}</span>
          <button type="button" className="admin-button-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </section>
    </Shell>
  );
}
