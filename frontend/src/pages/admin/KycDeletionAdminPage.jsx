import { useEffect, useState } from "react";
import kycAdminApi from "../../api/kycAdminApi";
import AdminShell from "./AdminShell";
import "./kycAdmin.css";

const errMsg = (e, fallback) => e.response?.data?.message || fallback;
const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");
const getId = (r) => r.id;
const STATUSES = ["", "Pending", "Completed", "Rejected"];

function Pill({ status }) {
  const key = String(status || "").toLowerCase();
  const cls = ["pending", "completed", "rejected"].includes(key) ? `kyc-pill--${key}` : "kyc-pill--muted";
  return <span className={`kyc-pill ${cls}`}>{status || "—"}</span>;
}

export default function KycDeletionAdminPage() {
  const [status, setStatus] = useState("Pending");
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [action, setAction] = useState(null); // {type: "approve"|"reject", request}
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [modalError, setModalError] = useState("");

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await kycAdminApi.listDeletionRequests({ status, page, pageSize });
      const data = res.data;
      const list = Array.isArray(data) ? data : data?.items || [];
      setItems(list);
      setTotal(data?.total ?? list.length);
    } catch (e) {
      setError(errMsg(e, "Failed to load deletion requests."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); /* eslint-disable-next-line */ }, [status, page]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const openAction = (type, request) => {
    setAction({ type, request });
    setNote("");
    setModalError("");
  };

  const submit = async () => {
    const { type, request } = action;
    if (type === "reject" && !note.trim()) return setModalError("A rejection note is required.");
    setBusy(true);
    setModalError("");
    try {
      if (type === "approve") await kycAdminApi.approveDeletion(getId(request));
      else await kycAdminApi.rejectDeletion(getId(request), note.trim());
      setSuccess(type === "approve" ? "Request approved. The eKYC data has been deleted." : "Request rejected.");
      setAction(null);
      await load();
    } catch (e) {
      setModalError(errMsg(e, `Failed to ${type} the request.`));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminShell title="eKYC Deletion Requests">
      {error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
      {success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}

      <section className="admin-panel">
        <div className="panel-header">
          <h2>Deletion requests</h2>
          <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
        </div>

        <div className="kyc-admin-toolbar">
          <label>Status{" "}
            <select value={status} onChange={(e) => { setPage(1); setStatus(e.target.value); }}>
              {STATUSES.map((s) => <option key={s} value={s}>{s || "All"}</option>)}
            </select>
          </label>
        </div>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : items.length === 0 ? (
          <div className="empty-state">No requests found.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr><th>ID</th><th>User</th><th>Status</th><th>Requested</th><th>Processed</th><th>Processed by</th><th>Note</th><th>Actions</th></tr>
              </thead>
              <tbody>
                {items.map((r) => {
                  const pending = String(r.status).toLowerCase() === "pending";
                  return (
                    <tr key={getId(r)}>
                      <td>#{getId(r)}</td>
                      <td>User #{r.userId}</td>
                      <td><Pill status={r.status} /></td>
                      <td>{fmt(r.requestedAt)}</td>
                      <td>{fmt(r.processedAt)}</td>
                      <td>{r.processedBy != null ? `#${r.processedBy}` : "—"}</td>
                      <td style={{ maxWidth: 220 }}>{r.note || "—"}</td>
                      <td>
                        <div className="table-actions">
                          <button type="button" className="text-button" disabled={!pending} onClick={() => openAction("approve", r)}>Approve</button>
                          <button type="button" className="danger-button" disabled={!pending} onClick={() => openAction("reject", r)}>Reject</button>
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
          <span>Page {page} / {totalPages}</span>
          <button type="button" className="admin-button-secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>Next</button>
        </div>
      </section>

      {action && (
        <div className="modal-overlay" onClick={() => !busy && setAction(null)}>
          <div className="modal-card small" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{action.type === "approve" ? "Approve deletion" : "Reject deletion"}</h3>
            </div>
            <div className="modal-body kyc-admin-fields">
              {modalError && <div className="auth-message error">{modalError}</div>}
              {action.type === "approve" ? (
                <p>Approving permanently deletes this user's eKYC data (including the original images). This can't be undone.</p>
              ) : (
                <label>
                  <span>Rejection note * (shown to the user)</span>
                  <textarea rows={3} value={note} onChange={(e) => setNote(e.target.value)} />
                </label>
              )}
            </div>
            <div className="modal-footer">
              <button type="button" className="admin-button-secondary" onClick={() => setAction(null)} disabled={busy}>Cancel</button>
              <button type="button" className={`admin-button ${action.type === "approve" ? "danger" : ""}`} onClick={submit} disabled={busy}>
                {busy ? "Working..." : action.type === "approve" ? "Approve & delete" : "Reject"}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminShell>
  );
}
