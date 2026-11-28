import { useEffect, useState } from "react";
import ticketReturnApi from "../../api/ticketReturnApi";
import { apiErrorMessage } from "../../utils/apiError";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import PanelShell from "../../components/PanelShell";
import AdminShell from "./AdminShell";
import StaffShell from "../staff/StaffShell";
import { formatPrice } from "../../utils/format";
import "../../styles/reportPages.css";

// Staff only see the ticket return queue and home link.
const STAFF_LINKS = [
  ["/staff/returns", "Return Requests", true],
  ["/", "← Back to site", true],
];

const MIN_REJECT_NOTE = 5;

function openEvidenceInNewTab(url) {
  if (!url) return;
  if (url.startsWith("data:")) {
    try {
      const [header, base64] = url.split(",");
      const mimeMatch = header.match(/data:([^;]+)/);
      const mime = mimeMatch ? mimeMatch[1] : "application/octet-stream";
      const binary = atob(base64);
      const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i += 1) {
        bytes[i] = binary.charCodeAt(i);
      }
      const blob = new Blob([bytes], { type: mime });
      const blobUrl = URL.createObjectURL(blob);
      window.open(blobUrl, "_blank", "noopener,noreferrer");
      setTimeout(() => URL.revokeObjectURL(blobUrl), 60000);
    } catch {
      window.open(url, "_blank", "noopener,noreferrer");
    }
    return;
  }
  window.open(url, "_blank", "noopener,noreferrer");
}

function getReturnStatusBadge(status) {
  const base = {
    display: "inline-block",
    padding: "3px 10px",
    borderRadius: "6px",
    fontWeight: 700,
    fontSize: "11.5px",
    whiteSpace: "nowrap",
  };
  switch (status) {
    case "Approved":
    case "ApprovedPendingManualRefund":
      return (
        <span style={{ ...base, background: "#fef3c7", color: "#b45309", border: "1px solid #fde68a" }}>
          Awaiting Transfer
        </span>
      );
    case "Refunded":
      return (
        <span style={{ ...base, background: "#dcfce7", color: "#15803d", border: "1px solid #bbf7d0" }}>
          Refunded
        </span>
      );
    case "Pending":
      return (
        <span style={{ ...base, background: "#eff6ff", color: "#1d4ed8", border: "1px solid #bfdbfe" }}>
          Pending Review
        </span>
      );
    case "RefundProcessing":
      return (
        <span style={{ ...base, background: "#f3e8ff", color: "#7e22ce", border: "1px solid #e9d5ff" }}>
          Processing Transfer
        </span>
      );
    case "RefundFailed":
      return (
        <span style={{ ...base, background: "#fee2e2", color: "#b91c1c", border: "1px solid #fecaca" }}>
          Transfer Failed
        </span>
      );
    case "Rejected":
      return (
        <span style={{ ...base, background: "#fee2e2", color: "#991b1b", border: "1px solid #fecaca" }}>
          Rejected
        </span>
      );
    case "Cancelled":
      return (
        <span style={{ ...base, background: "#f1f5f9", color: "#64748b", border: "1px solid #e2e8f0" }}>
          Cancelled
        </span>
      );
    default:
      return <span>{status}</span>;
  }
}

function formatProcessor(userId) {
  if (!userId) return "—";
  if (Number(userId) === 1) return "Admin (#1)";
  return `Staff (#${userId})`;
}

// Two-step refund workflow: Staff approve and revoke the ticket first, then Admin confirms the manual bank transfer.
function ReturnQueue({ isAdmin }) {
  const toast = useToast();
  const [rows, setRows] = useState([]);
  const [status, setStatus] = useState("Pending");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [decision, setDecision] = useState(null); // { row, approve, note, error, busy }
  const [payoutModal, setPayoutModal] = useState(null); // { row, reference, amount, note, error, busy }
  const [inspectItem, setInspectItem] = useState(null);
  const [activeEvidence, setActiveEvidence] = useState(null);

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

  const confirmManualRefund = (row) => {
    setPayoutModal({
      row,
      reference: "",
      amount: String(row.refundAmount || 0),
      note: "",
      error: "",
      busy: false,
    });
  };

  const submitManualRefund = async () => {
    if (!payoutModal) return;
    const { row, reference, amount, note } = payoutModal;
    const trimmedRef = reference.trim();
    const numAmount = Number(amount);

    if (!trimmedRef) {
      setPayoutModal((p) => ({ ...p, error: "Please enter the bank transfer reference / FT code." }));
      return;
    }
    if (!Number.isFinite(numAmount) || numAmount !== Number(row.refundAmount || 0)) {
      setPayoutModal((p) => ({ ...p, error: `Transferred amount must match ${formatPrice(row.refundAmount || 0)}.` }));
      return;
    }

    setPayoutModal((p) => ({ ...p, busy: true, error: "" }));
    try {
      await ticketReturnApi.confirmManualRefund(row.ticketReturnRequestId, {
        reference: trimmedRef,
        amount: numAmount,
        note: note.trim() || undefined,
      });
      toast.success(`Return #${row.ticketReturnRequestId} marked as refunded.`);
      setPayoutModal(null);
      await load();
    } catch (e) {
      const msg = apiErrorMessage(e, "Could not confirm the bank transfer.");
      setPayoutModal((p) => ({ ...p, busy: false, error: msg }));
      toast.error(msg);
    }
  };

  const openDecision = (row, approve) => setDecision({ row, approve, note: "", error: "", busy: false });

  // Rejections require a customer-facing reason; approval notes are optional.
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
      else toast.success(`Return #${row.ticketReturnRequestId} approved. Transfer the refund manually and confirm it afterward.`);
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
              Two-step workflow: (1) Ticketing/Support Staff review eligibility and approve return to release seat inventory; (2) Accounting/Admin execute manual bank payout and log transfer reference.
              {isAdmin
                ? " As Admin, you can oversee both steps, reassign tasks, or confirm payouts directly."
                : " Showing requests assigned for your operational events. Reviewing requires reason and audit accountability."}
            </p>
          </div>
          <select
            className="modern-select"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">All Statuses</option>
            <option value="Pending">Pending Review</option>
            <option value="Approved">Awaiting Bank Transfer</option>
            <option value="Refunded">Refunded</option>
            <option value="Rejected">Rejected</option>
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
                  <th>Request</th>
                  <th>Event</th>
                  <th>Ticket</th>
                  <th>Customer</th>
                  <th>Reason</th>
                  <th style={{ whiteSpace: "nowrap" }}>Refund</th>
                  <th style={{ whiteSpace: "nowrap" }}>Status</th>
                  <th style={{ whiteSpace: "nowrap" }}>Processed By</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.ticketReturnRequestId}>
                    <td style={{ whiteSpace: "nowrap" }}>#{r.ticketReturnRequestId}</td>
                    <td style={{ minWidth: "135px" }}>
                      <strong>{r.eventName || `Event #${r.eventId}`}</strong>
                      <div className="ow-hint">ID #{r.eventId}</div>
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>#{r.ticketId} · {r.ticketTypeName}</td>
                    <td style={{ minWidth: "105px" }}>
                      <strong>{r.customerName || `User #${r.requesterUserId}`}</strong>
                      {r.customerName && <div className="ow-hint">ID #{r.requesterUserId}</div>}
                    </td>
                    <td style={{ minWidth: "150px", maxWidth: "210px" }}>
                      <div>{r.reason}</div>
                    </td>
                    <td style={{ whiteSpace: "nowrap", fontWeight: 600 }}>{formatPrice(r.refundAmount || 0)}</td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      {getReturnStatusBadge(r.status)}
                      {r.status === "RefundFailed" && r.refundError && <div className="ow-hint" style={{ color: "#b91c1c", marginTop: "4px", whiteSpace: "normal", maxWidth: "170px" }}>{r.refundError}</div>}
                      {r.status === "Refunded" && (r.refundReference || r.manualRefundReference) && <div className="ow-hint" style={{ marginTop: "4px" }}>Ref {r.refundReference || r.manualRefundReference}</div>}
                    </td>
                    <td style={{ whiteSpace: "nowrap" }}>
                      <div style={{ fontSize: "12px", lineHeight: 1.45 }}>
                        {r.reviewedBy ? (
                          <div><span style={{ color: "#64748b" }}>Review:</span> <strong>{formatProcessor(r.reviewedBy)}</strong></div>
                        ) : null}
                        {r.manualRefundProcessedBy ? (
                          <div style={{ color: "#166534" }}><span style={{ color: "#64748b" }}>Payout:</span> <strong>{formatProcessor(r.manualRefundProcessedBy)}</strong></div>
                        ) : null}
                        {!r.reviewedBy && !r.manualRefundProcessedBy && <span className="ow-hint">—</span>}
                      </div>
                    </td>
                    <td style={{ whiteSpace: "nowrap", paddingRight: "14px" }}>
                      <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-start", gap: "5px" }}>
                        {r.status === "Pending" ? (
                          <div style={{ display: "inline-flex", alignItems: "center", gap: "5px" }}>
                            <button className="tb-btn tb-btn-primary" style={{ padding: "5px 10px", fontSize: "12px" }} onClick={() => openDecision(r, true)}>Approve</button>
                            <button className="tb-btn tb-btn-outline" style={{ padding: "5px 10px", fontSize: "12px" }} onClick={() => openDecision(r, false)}>Reject</button>
                          </div>
                        ) : r.status === "Approved" || r.status === "ApprovedPendingManualRefund" || r.status === "RefundProcessing" || r.status === "RefundFailed" ? (
                          <button className="tb-btn tb-btn-primary" style={{ padding: "5px 10px", fontSize: "12px" }} onClick={() => confirmManualRefund(r)}>Confirm Transfer</button>
                        ) : null}
                        <button
                          type="button"
                          className="tb-btn tb-btn-outline"
                          style={{ padding: "4px 10px", fontSize: "11.5px" }}
                          onClick={() => setInspectItem(r)}
                          title="View complete return request details"
                        >
                          Details
                        </button>
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
              <div>{getReturnStatusBadge(inspectItem.status)}</div>
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
                <strong>{inspectItem.customerName || `Order #${inspectItem.orderId}`}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>Order #{inspectItem.orderId} · User #{inspectItem.requesterUserId}</div>
              </div>

              <div style={{ background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "11px", fontWeight: 700 }}>REFUND AMOUNT</span>
                <strong style={{ color: "#7e22ce", fontSize: "15px" }}>{formatPrice(inspectItem.refundAmount || 0)}</strong>
                <div style={{ color: "#64748b", fontSize: "12px" }}>Calculated per pro-rata policy</div>
              </div>
            </div>

            <div style={{ marginBottom: "14px", background: "#f8fafc", padding: "8px 12px", borderRadius: "8px", border: "1px solid #e2e8f0", display: "flex", justifyContent: "space-between", fontSize: "12px" }}>
              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "10.5px", fontWeight: 700 }}>STEP 1: STAFF REVIEW</span>
                <span>{inspectItem.reviewedBy ? <strong>Approved by {formatProcessor(inspectItem.reviewedBy)}</strong> : <span style={{ color: "#b45309" }}>Pending Staff Review</span>}</span>
              </div>
              <div style={{ textAlign: "right" }}>
                <span style={{ color: "#64748b", display: "block", fontSize: "10.5px", fontWeight: 700 }}>STEP 2: BANK PAYOUT (ACCOUNTING)</span>
                <span>{inspectItem.manualRefundProcessedBy ? <strong style={{ color: "#166534" }}>Paid by {formatProcessor(inspectItem.manualRefundProcessedBy)}</strong> : <span style={{ color: "#64748b" }}>Awaiting Bank Payout</span>}</span>
              </div>
            </div>

            <div style={{ marginBottom: "14px", background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "8px", padding: "10px 14px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                <span style={{ fontSize: "11px", fontWeight: 700, color: "#166534" }}>CUSTOMER BANK ACCOUNT FOR MANUAL TRANSFER</span>
                {inspectItem.bankAccountNumber && (
                  <button
                    type="button"
                    onClick={() => {
                      navigator.clipboard.writeText(inspectItem.bankAccountNumber);
                      toast.success(`Copied account ${inspectItem.bankAccountNumber} to clipboard!`);
                    }}
                    style={{
                      fontSize: "11.5px",
                      padding: "3px 10px",
                      background: "#ffffff",
                      border: "1px solid #86efac",
                      borderRadius: "6px",
                      color: "#166534",
                      cursor: "pointer",
                      fontWeight: 600
                    }}
                  >
                    Copy Account No.
                  </button>
                )}
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1.2fr 1fr 1fr", gap: "10px", fontSize: "12.5px" }}>
                <div>
                  <span style={{ color: "#15803d", fontSize: "11px", display: "block" }}>Bank Name</span>
                  <strong style={{ color: "#0f172a" }}>{inspectItem.bankName || "—"}</strong>
                </div>
                <div>
                  <span style={{ color: "#15803d", fontSize: "11px", display: "block" }}>Account Number</span>
                  <strong style={{ fontFamily: "monospace", letterSpacing: "0.5px", color: "#0f172a" }}>{inspectItem.bankAccountNumber || "—"}</strong>
                </div>
                <div>
                  <span style={{ color: "#15803d", fontSize: "11px", display: "block" }}>Account Holder</span>
                  <strong style={{ color: "#0f172a" }}>{inspectItem.bankAccountName || "—"}</strong>
                </div>
              </div>
            </div>

            <div style={{ marginBottom: "14px" }}>
              <label style={{ display: "block", fontSize: "11px", fontWeight: 700, color: "#475569", marginBottom: "4px" }}>CUSTOMER REASON / EXPLANATION</label>
              <div style={{ padding: "10px 12px", background: "#f1f5f9", borderRadius: "8px", fontSize: "13px", lineHeight: 1.5, whiteSpace: "pre-wrap", border: "1px solid #e2e8f0" }}>
                {inspectItem.reason}
              </div>
            </div>

            <div style={{ marginBottom: "14px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                <label style={{ fontSize: "11px", fontWeight: 700, color: "#475569", margin: 0 }}>SUPPORTING EVIDENCE</label>
                {inspectItem.proofUrl && (
                  <button
                    type="button"
                    onClick={() => setActiveEvidence(inspectItem.proofUrl)}
                    style={{
                      background: "none",
                      border: "none",
                      color: "#2563eb",
                      fontSize: "12px",
                      fontWeight: 600,
                      cursor: "pointer",
                      padding: 0
                    }}
                  >
                    View in Full Lightbox
                  </button>
                )}
              </div>

              {inspectItem.proofUrl ? (
                <div style={{ background: "#f8fafc", border: "1px solid #cbd5e1", borderRadius: "8px", padding: "10px" }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "8px" }}>
                    <span style={{ fontSize: "12px", color: "#475569", fontWeight: 500 }}>
                      Attached Document
                    </span>
                    <button
                      type="button"
                      onClick={() => openEvidenceInNewTab(inspectItem.proofUrl)}
                      style={{ background: "none", border: "none", padding: 0, cursor: "pointer", color: "#2563eb", fontSize: "12px", fontWeight: 600, textDecoration: "underline" }}
                    >
                      Open in new tab
                    </button>
                  </div>

                  {inspectItem.proofUrl.startsWith("data:image") || /\.(jpg|jpeg|png|webp|gif)(\?.*)?$/i.test(inspectItem.proofUrl) ? (
                    <div
                      onClick={() => setActiveEvidence(inspectItem.proofUrl)}
                      style={{
                        border: "1px solid #e2e8f0",
                        borderRadius: "6px",
                        overflow: "hidden",
                        maxHeight: "180px",
                        display: "flex",
                        justifyContent: "center",
                        background: "#ffffff",
                        cursor: "zoom-in"
                      }}
                      title="Click to view full image in Lightbox"
                    >
                      <img src={inspectItem.proofUrl} alt="Evidence preview" style={{ maxWidth: "100%", maxHeight: "180px", objectFit: "contain" }} />
                    </div>
                  ) : inspectItem.proofUrl.startsWith("data:application/pdf") || inspectItem.proofUrl.endsWith(".pdf") ? (
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", background: "#ffffff", padding: "10px 12px", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                      <span style={{ fontSize: "12px", fontWeight: 600, color: "#334155" }}>[PDF DOCUMENT ATTACHED]</span>
                      <button
                        type="button"
                        className="tb-btn tb-btn-outline"
                        style={{ padding: "4px 10px", fontSize: "12px" }}
                        onClick={() => setActiveEvidence(inspectItem.proofUrl)}
                      >
                        Preview PDF
                      </button>
                    </div>
                  ) : (
                    <div style={{ fontSize: "12.5px", color: "#3b82f6", wordBreak: "break-all" }}>
                      {inspectItem.proofUrl}
                    </div>
                  )}
                </div>
              ) : (
                <div style={{ color: "#94a3b8", fontSize: "13px", fontStyle: "italic", background: "#f8fafc", padding: "10px 12px", borderRadius: "8px", border: "1px dashed #cbd5e1" }}>
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
                    Approve Request
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

      {payoutModal && (
        <div className="rp-modal-backdrop" onClick={() => !payoutModal.busy && setPayoutModal(null)}>
          <div className="rp-modal" style={{ maxWidth: "480px" }} onClick={(e) => e.stopPropagation()}>
            <h3 style={{ margin: "0 0 8px", fontSize: "18px", fontWeight: 700 }}>
              Confirm Bank Transfer — #{payoutModal.row.ticketReturnRequestId}
            </h3>
            <p className="rp-muted" style={{ marginBottom: "12px" }}>
              {payoutModal.row.eventName || `Event #${payoutModal.row.eventId}`} · {payoutModal.row.ticketTypeName}
              <br />
              Customer: <strong>{payoutModal.row.customerName || `User #${payoutModal.row.requesterUserId}`}</strong>
            </p>

            <div style={{ marginBottom: "14px", background: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "8px", padding: "10px 12px", fontSize: "12.5px" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "6px" }}>
                <span style={{ fontSize: "11px", fontWeight: 700, color: "#166534" }}>DESTINATION BANK ACCOUNT</span>
                {payoutModal.row.bankAccountNumber && (
                  <button
                    type="button"
                    onClick={() => {
                      navigator.clipboard.writeText(payoutModal.row.bankAccountNumber);
                      toast.success(`Copied ${payoutModal.row.bankAccountNumber}`);
                    }}
                    style={{ fontSize: "11px", padding: "2px 8px", background: "#fff", border: "1px solid #86efac", borderRadius: "5px", color: "#166534", cursor: "pointer", fontWeight: 600 }}
                  >
                    Copy No.
                  </button>
                )}
              </div>
              <div><strong>{payoutModal.row.bankName || "—"}</strong> · <span style={{ fontFamily: "monospace", fontWeight: 700 }}>{payoutModal.row.bankAccountNumber || "—"}</span></div>
              <div style={{ color: "#15803d", fontSize: "12px" }}>{payoutModal.row.bankAccountName || "—"}</div>
            </div>

            <label className="ow-field" style={{ marginBottom: "10px" }}>
              <span>Bank Transfer Reference (FT Code) *</span>
              <input
                type="text"
                placeholder="e.g. FT2411258899"
                value={payoutModal.reference}
                maxLength={100}
                onChange={(e) => setPayoutModal((p) => ({ ...p, reference: e.target.value, error: "" }))}
              />
            </label>

            <label className="ow-field" style={{ marginBottom: "10px" }}>
              <span>Transferred Amount (VND) *</span>
              <input
                type="number"
                value={payoutModal.amount}
                onChange={(e) => setPayoutModal((p) => ({ ...p, amount: e.target.value, error: "" }))}
              />
              <span className="ow-hint">Must match approved refund: <strong>{formatPrice(payoutModal.row.refundAmount || 0)}</strong></span>
            </label>

            <label className="ow-field">
              <span>Transfer Note (optional)</span>
              <input
                type="text"
                placeholder="e.g. Transferred via Vietcombank"
                value={payoutModal.note}
                maxLength={300}
                onChange={(e) => setPayoutModal((p) => ({ ...p, note: e.target.value }))}
              />
            </label>

            {payoutModal.error && <div className="ow-field-error" style={{ marginTop: "8px" }}>{payoutModal.error}</div>}

            <div className="rp-modal-actions" style={{ marginTop: "16px" }}>
              <button type="button" className="tb-btn tb-btn-outline" disabled={payoutModal.busy} onClick={() => setPayoutModal(null)}>
                Cancel
              </button>
              <button type="button" className="tb-btn tb-btn-primary" disabled={payoutModal.busy} onClick={submitManualRefund}>
                {payoutModal.busy ? "Confirming..." : "Confirm Transfer"}
              </button>
            </div>
          </div>
        </div>
      )}

      {activeEvidence && (
        <div
          className="rp-modal-backdrop"
          style={{ zIndex: 9999, background: "rgba(15, 23, 42, 0.75)" }}
          onClick={() => setActiveEvidence(null)}
        >
          <div
            className="rp-modal"
            style={{
              maxWidth: "920px",
              width: "95vw",
              maxHeight: "92vh",
              display: "flex",
              flexDirection: "column",
              padding: 0,
              overflow: "hidden"
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "14px 20px", borderBottom: "1px solid #e2e8f0", background: "#f8fafc" }}>
              <strong style={{ fontSize: "15px", color: "#0f172a" }}>
                Evidence Document Viewer
              </strong>
              <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
                <button
                  type="button"
                  onClick={() => openEvidenceInNewTab(activeEvidence)}
                  className="tb-btn tb-btn-outline"
                  style={{ padding: "4px 12px", fontSize: "12px" }}
                >
                  Open in New Window
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  style={{ padding: "4px 12px", fontSize: "12px" }}
                  onClick={() => setActiveEvidence(null)}
                >
                  Close
                </button>
              </div>
            </div>

            <div style={{ flex: 1, overflowY: "auto", padding: "16px", display: "flex", justifyContent: "center", alignItems: "center", background: "#f1f5f9", minHeight: "400px" }}>
              {activeEvidence.startsWith("data:application/pdf") || activeEvidence.endsWith(".pdf") ? (
                <iframe
                  src={activeEvidence}
                  title="PDF Evidence Document"
                  style={{ width: "100%", height: "70vh", border: "none", borderRadius: "8px", background: "#ffffff" }}
                />
              ) : (
                <img
                  src={activeEvidence}
                  alt="Full evidence document"
                  style={{ maxWidth: "100%", maxHeight: "72vh", objectFit: "contain", borderRadius: "8px", boxShadow: "0 4px 12px rgba(0,0,0,0.15)", background: "#ffffff" }}
                />
              )}
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
      <StaffShell title="Process Full/Partial Refund — Return Requests">
        <ReturnQueue isAdmin={false} />
      </StaffShell>
    );
  }
  return <div className="tb-empty" style={{ margin: 40 }}>Only Staff or Admin can review ticket returns.</div>;
}

