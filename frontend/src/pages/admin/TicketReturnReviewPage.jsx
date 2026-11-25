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
  const [inspectItem, setInspectItem] = useState(null);

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
          <select
            className="modern-select"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
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
          <div className="tb-loading"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
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
                    <td>
                      <div>{r.reason}</div>
                      {r.proofUrl && (
                        <a
                          href={r.proofUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "4px",
                            marginTop: "6px",
                            fontSize: "12px",
                            color: "#2563eb",
                            textDecoration: "underline",
                            fontWeight: 500,
                          }}
                        >
                          View Evidence Document
                        </a>
                      )}
                    </td>
                    <td>{formatPrice(r.refundAmount || 0)}</td>
                    <td>
                      {r.status}
                      {r.status === "RefundFailed" && r.refundError && <div className="ow-hint">{r.refundError}</div>}
                      {r.status === "Refunded" && r.refundReference && <div className="ow-hint">Ref {r.refundReference}</div>}
                    </td>
                    {isAdmin && <td>{r.reviewedBy ? `User #${r.reviewedBy}` : "-"}</td>}
                    <td style={{ whiteSpace: "nowrap" }}>
                      <div style={{ display: "inline-flex", alignItems: "center", gap: "6px" }}>
                        <button
                          type="button"
                          className="tb-btn tb-btn-outline"
                          onClick={() => setInspectItem(r)}
                          title="View complete return request details"
                        >
                          Details
                        </button>
                        {r.status === "Pending" ? (
                          <>
                            <button className="tb-btn tb-btn-primary" onClick={() => openDecision(r, true)}>Approve & refund</button>
                            <button className="tb-btn tb-btn-outline" onClick={() => openDecision(r, false)}>Reject</button>
                          </>
                        ) : r.canRetryRefund ? (
                          <button className="tb-btn tb-btn-primary" onClick={() => retryRefund(r)}>Retry refund</button>
                        ) : (
                          <span className="ow-hint">{r.reviewNote || "-"}</span>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {rows.length > 0 && <p className="ow-hint" style={{ marginTop: 8 }}>{rows.length} request(s)</p>}
      </div>

      {inspectItem && (
        <div className="rp-modal-backdrop" onClick={() => setInspectItem(null)}>
          <div className="rp-modal" style={{ maxWidth: "580px" }} onClick={(e) => e.stopPropagation()}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "14px", borderBottom: "1px solid #e2e8f0", paddingBottom: "10px" }}>
              <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 700 }}>
                Return Request #{inspectItem.ticketReturnRequestId}
              </h3>
              <span className="rp-badge" style={{
                background: inspectItem.status === "Approved" || inspectItem.status === "Refunded" ? "#dcfce7" : inspectItem.status === "Pending" ? "#fef3c7" : "#fee2e2",
                color: inspectItem.status === "Approved" || inspectItem.status === "Refunded" ? "#15803d" : inspectItem.status === "Pending" ? "#b45309" : "#b91c1c",
                padding: "4px 10px",
                borderRadius: "999px",
                fontWeight: 700,
                fontSize: "12px"
              }}>
                {inspectItem.status}
              </span>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "10px", marginBottom: "14px", fontSize: "13px" }}>
              <div style={{ background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "11px", fontWeight: 700 }}>EVENT</span>
                <strong>{inspectItem.eventName || `Event #${inspectItem.eventId}`}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>ID: #{inspectItem.eventId}</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "11px", fontWeight: 700 }}>TICKET DETAILS</span>
                <strong>Ticket #{inspectItem.ticketId}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>Tier: {inspectItem.ticketTypeName || "Standard"}</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "11px", fontWeight: 700 }}>ORDER & CUSTOMER</span>
                <strong>Order #{inspectItem.orderId}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>Requester: User #{inspectItem.requesterUserId}</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "11px", fontWeight: 700 }}>REFUND AMOUNT</span>
                <strong style={{ color: "#7e22ce", fontSize: "15px" }}>{formatPrice(inspectItem.refundAmount || 0)}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>Calculated per pro-rata policy</div>
              </div>
            </div>

            <div style={{ marginBottom: "14px" }}>
              <label style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#475569", marginBottom: "4px" }}>CUSTOMER REASON / EXPLANATION</label>
              <div style={{ padding: "10px 12px", background: "#f1f5f9", borderRadius: "8px", fontSize: "13px", lineHeight: 1.5, whiteSpace: "pre-wrap", border: "1px solid #e2e8f0" }}>
                {inspectItem.reason}
              </div>
            </div>

            <div style={{ marginBottom: "14px" }}>
              <label style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#475569", marginBottom: "4px" }}>SUPPORTING EVIDENCE</label>
              {inspectItem.proofUrl ? (
                <div>
                  <a
                    href={inspectItem.proofUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{ color: "#2563eb", fontSize: "13px", fontWeight: 600, textDecoration: "underline", display: "inline-flex", alignItems: "center", gap: "4px", marginBottom: "8px" }}
                  >
                    Open attachment in new tab
                  </a>
                  {/\.(jpg|jpeg|png|webp|gif)(\?.*)?$/i.test(inspectItem.proofUrl) || inspectItem.proofUrl.includes("cloudinary") ? (
                    <div style={{ border: "1px solid #cbd5e1", borderRadius: "8px", overflow: "hidden", maxHeight: "200px", display: "flex", justifyContent: "center", background: "#f8fafc" }}>
                      <img src={inspectItem.proofUrl} alt="Evidence preview" style={{ maxWidth: "100%", maxHeight: "200px", objectFit: "contain" }} />
                    </div>
                  ) : null}
                </div>
              ) : (
                <div style={{ color: "#94a3b8", fontSize: "13px", fontStyle: "italic" }}>
                  No supporting documents were attached with this request (Optional submission).
                </div>
              )}
            </div>

            {inspectItem.reviewNote && (
              <div style={{ marginBottom: "14px", padding: "10px 12px", background: "#fef3c7", borderRadius: "8px", border: "1px solid #fde68a" }}>
                <span style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#92400e" }}>REVIEW NOTE</span>
                <span style={{ fontSize: "13px", color: "#78350f" }}>{inspectItem.reviewNote}</span>
              </div>
            )}

            <div className="rp-modal-actions" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid #e2e8f0", paddingTop: "12px", marginTop: "16px" }}>
              <button type="button" className="tb-btn tb-btn-outline" onClick={() => setInspectItem(null)}>
                Close
              </button>

              {inspectItem.status === "Pending" && (
                <div style={{ display: "flex", gap: "8px" }}>
                  <button
                    type="button"
                    className="tb-btn tb-btn-outline"
                    onClick={() => {
                      const item = inspectItem;
                      setInspectItem(null);
                      openDecision(item, false);
                    }}
                  >
                    Reject
                  </button>
                  <button
                    type="button"
                    className="tb-btn tb-btn-primary"
                    onClick={() => {
                      const item = inspectItem;
                      setInspectItem(null);
                      openDecision(item, true);
                    }}
                  >
                    Approve & refund
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

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
                {decision.busy ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : decision.approve ? "Approve & refund" : "Reject"}
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
