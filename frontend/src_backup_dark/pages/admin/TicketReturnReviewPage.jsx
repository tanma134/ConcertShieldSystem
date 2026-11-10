import { useEffect, useState } from "react";
import ticketReturnApi from "../../api/ticketReturnApi";
import { apiErrorMessage } from "../../utils/apiError";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import PanelShell from "../../components/PanelShell";
import AdminShell from "./AdminShell";
import { formatPrice } from "../../utils/format";
import "../../styles/reportPages.css";

// Sidebar của Staff: chỉ có hàng đợi hoàn vé + đường về trang chủ.
const STAFF_LINKS = [
  ["/staff/returns", "Return Requests", true],
  ["/", "← Back to site", true],
];

const MIN_REJECT_NOTE = 5;

// Hàng đợi hoàn vé. Staff là người duyệt chính; Admin xem toàn bộ để giám sát và
// chỉ xử lý thay khi cần (backend cho phép cả hai role, nên ở đây Admin thấy thêm
// cột "Reviewed by" để biết ai đã quyết định).
function ReturnQueue({ isAdmin }) {
  const toast = useToast();
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState("Pending");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [decision, setDecision] = useState(null); // { row, approve, note, error, busy }

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const r = await ticketReturnApi.reviewQueue(status);
      setRows(r.data?.data || []);
    } catch (e) {
      setError(apiErrorMessage(e, "Could not load return requests."));
    } finally {
      setLoading(false);
    }
  };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { load(); }, [status]);

  // Thử hoàn tiền lại cho yêu cầu đã duyệt nhưng hoàn tiền lỗi.
  const retryRefund = async (row) => {
    try {
      const r = await ticketReturnApi.retryRefund(row.ticketReturnRequestId);
      const done = r.data?.data?.status === "Refunded";
      if (done) toast.success(`Return #${row.ticketReturnRequestId} refunded.`);
      else toast.error(r.data?.data?.refundError || "Refund failed again. Please try later.");
      await load();
    } catch (e) {
      toast.error(apiErrorMessage(e, "Could not retry the refund."));
    }
  };

  const openDecision = (row, approve) => setDecision({ row, approve, note: "", error: "", busy: false });

  // Từ chối bắt buộc có lý do để customer biết vì sao; duyệt thì ghi chú tuỳ chọn.
  const confirmDecision = async () => {
    const { row, approve, note } = decision;
    if (!approve && note.trim().length < MIN_REJECT_NOTE) {
      setDecision((d) => ({ ...d, error: `Please give a reason (at least ${MIN_REJECT_NOTE} characters) so the customer knows why.` }));
      return;
    }
    setDecision((d) => ({ ...d, busy: true, error: "" }));
    try {
      const saved = await ticketReturnApi.review(row.ticketReturnRequestId, approve, note.trim());
      const finalStatus = saved.data?.data?.status;
      if (!approve) toast.success(`Return #${row.ticketReturnRequestId} rejected.`);
      else if (finalStatus === "RefundFailed") toast.error(`Return #${row.ticketReturnRequestId} approved and back in inventory, but the refund failed. Use Retry refund.`);
      else toast.success(`Return #${row.ticketReturnRequestId} approved and refunded. The ticket is back in inventory.`);
      setDecision(null);
      await load();
    } catch (e) {
      const message = apiErrorMessage(e, "Could not review this request.");
      setDecision((d) => ({ ...d, busy: false, error: message }));
      toast.error(message);
    }
  };

  return (
    <div className="rp-wrap">
      <div className="od-head">
        <div>
          <p className="od-eyebrow">{isAdmin ? "ADMIN · OVERSIGHT" : "SUPPORT · RETURNS"}</p>
          <h1>Ticket return requests</h1>
        </div>
      </div>
      <div className="rp-section">
        <div className="od-section-head">
          <div>
            <h2>Review queue</h2>
            <p className="ow-hint">
              Refund amount follows the organizer's refund policy for each event (deadline and percentage).
              {isAdmin
                ? " Staff handle the day-to-day review; as Admin you can see every decision and step in when needed."
                : " Only requests of concerts where the organizer assigned you to review returns are listed. Approving releases the seat back to sale and pays the refund; rejecting needs a reason."}
            </p>
          </div>
          <select value={status} onChange={(e) => setStatus(e.target.value)}>
            <option>Pending</option>
            <option>Approved</option>
            <option>Refunded</option>
            <option>RefundFailed</option>
            <option>Rejected</option>
            <option>Cancelled</option>
            <option value="">All</option>
          </select>
        </div>
        {error && <div className="tb-error">{error}</div>}
        {loading ? (
          <div className="tb-loading">Loading...</div>
        ) : rows.length === 0 ? (
          <div className="tb-empty">No return requests.</div>
        ) : (
          <div className="od-table-wrap">
            <table className="ow-table">
              <thead>
                <tr>
                  <th>Request</th><th>Event</th><th>Ticket</th><th>Customer</th><th>Reason</th>
                  <th>Refund</th><th>Status</th>{isAdmin && <th>Reviewed by</th>}<th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.ticketReturnRequestId}>
                    <td>#{r.ticketReturnRequestId}</td>
                    <td><strong>{r.eventName || `Event #${r.eventId}`}</strong><div className="ow-hint">ID #{r.eventId}</div></td>
                    <td>#{r.ticketId} · {r.ticketTypeName}</td>
                    <td>User #{r.requesterUserId}</td>
                    <td>{r.reason}</td>
                    <td>{formatPrice(r.refundAmount || 0)}</td>
                    <td>
                      {r.status}
                      {r.status === "RefundFailed" && r.refundError && <div className="ow-hint">{r.refundError}</div>}
                      {r.status === "Refunded" && r.refundReference && <div className="ow-hint">Ref {r.refundReference}</div>}
                    </td>
                    {isAdmin && <td>{r.reviewedBy ? `User #${r.reviewedBy}` : "-"}</td>}
                    <td>
                      {r.status === "Pending" ? (
                        <>
                          <button className="tb-btn tb-btn-primary" onClick={() => openDecision(r, true)}>Approve</button>{" "}
                          <button className="tb-btn tb-btn-outline" onClick={() => openDecision(r, false)}>Reject</button>
                        </>
                      ) : r.canRetryRefund ? (
                        <button className="tb-btn tb-btn-primary" onClick={() => retryRefund(r)}>Retry refund</button>
                      ) : (
                        r.reviewNote || "-"
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {rows.length > 0 && <p className="ow-hint" style={{ marginTop: 8 }}>{rows.length} request(s)</p>}
      </div>

      {decision && (
        <div className="rp-modal-backdrop" onClick={() => !decision.busy && setDecision(null)}>
          <div className="rp-modal" onClick={(e) => e.stopPropagation()}>
            <h3>{decision.approve ? "Approve" : "Reject"} return #{decision.row.ticketReturnRequestId}</h3>
            <p className="rp-muted">
              {decision.row.eventName} · {decision.row.ticketTypeName}
              <br />
              Refund: <strong>{formatPrice(decision.row.refundAmount || 0)}</strong>
            </p>
            <label className="ow-field">
              <span>{decision.approve ? "Note (optional)" : "Reason for rejection *"}</span>
              <textarea
                rows={3}
                value={decision.note}
                maxLength={500}
                aria-invalid={!!decision.error}
                onChange={(e) => setDecision((d) => ({ ...d, note: e.target.value, error: "" }))}
              />
              {decision.error && <span className="ow-field-error">{decision.error}</span>}
            </label>
            <div className="rp-modal-actions">
              <button className="tb-btn tb-btn-outline" disabled={decision.busy} onClick={() => setDecision(null)}>Cancel</button>
              <button className="tb-btn tb-btn-primary" disabled={decision.busy} onClick={confirmDecision}>
                {decision.busy ? "Saving..." : decision.approve ? "Approve" : "Reject"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default function TicketReturnReviewPage() {
  const { isAdmin, isStaff } = useAuth();

  if (isAdmin) {
    return <AdminShell title="Ticket Returns / Refunds"><ReturnQueue isAdmin /></AdminShell>;
  }
  if (isStaff) {
    return (
      <PanelShell title="Return Requests" links={STAFF_LINKS} roleLabel="Staff" logoutPath="/login" navLabel="Staff navigation">
        <ReturnQueue isAdmin={false} />
      </PanelShell>
    );
  }
  return <div className="tb-empty" style={{ margin: 40 }}>Only Staff or Admin can review ticket returns.</div>;
}
