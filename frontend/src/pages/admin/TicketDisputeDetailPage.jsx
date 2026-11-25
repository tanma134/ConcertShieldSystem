import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import AdminShell from "./AdminShell";
import supportCaseApi from "../../api/supportCaseApi";
import ticketApi from "../../api/ticketApi";
import paymentReconciliationApi from "../../api/paymentReconciliationApi";
import { useToast } from "../../components/ToastProvider";
import "./TicketDisputeDetailPage.css";

function unwrap(response) {
  return response?.data?.data ?? response?.data ?? null;
}

function getPageItems(response) {
  const data = unwrap(response);
  return data?.items ?? data?.Items ?? [];
}

function formatDate(value) {
  if (!value) return "-";

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? "-"
    : date.toLocaleString("en-US", {
        dateStyle: "medium",
        timeStyle: "short",
      });
}

function formatMoney(value) {
  if (value == null) return "-";

  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
}

function getEvidenceName(description) {
  const match = description?.match(
    /\[Supporting Evidence Attached:\s*([^\]]+)\]/i
  );

  return match?.[1]?.trim() || null;
}

function cleanDescription(description) {
  return (description || "")
    .replace(/\[Supporting Evidence Attached:\s*[^\]]+\]/gi, "")
    .trim();
}

function isRepairCandidate(item, order) {
  if (!item || !order) return false;

  const isPaymentSuccess =
    String(item.paymentStatus || "").toLowerCase() === "success";
  const isOrderPaid = String(order.status || "").toLowerCase() === "paid";
  const amountMatches =
    Number(item.paymentAmount) === Number(item.orderAmount);

  const missingTickets =
    Number(item.issuedTicketCount) < Number(item.expectedTicketCount);
  const missingQr = Number(item.ticketsWithoutQrCount) > 0;

  return isPaymentSuccess && isOrderPaid && amountMatches &&
    (missingTickets || missingQr);
}

function getTicketDataIssues(ticket) {
  const issues = [];
  const ticketCode = String(ticket.ticketCode || "").trim().toLowerCase();
  if (!ticketCode || ticketCode === "00000000-0000-0000-0000-000000000000") issues.push("Ticket code is missing");
  if (!Number(ticket.orderId)) issues.push("Order ID is missing");
  if (!Number(ticket.eventId) || !ticket.eventName?.trim()) issues.push("Event information is incomplete");
  if (!Number(ticket.ticketTypeId) || !ticket.ticketTypeName?.trim()) issues.push("Ticket type is incomplete");
  if (!ticket.hasOwner || !ticket.ownerName?.trim()) issues.push("Ticket owner information is incomplete");
  if (!ticket.status?.trim()) issues.push("Ticket status is missing");
  if (ticket.unitPrice == null) issues.push("Ticket price is missing");
  if (!ticket.hasQrToken) issues.push("QR code is missing");
  return issues;
}

export default function TicketDisputeDetailPage() {
  const { id } = useParams();
  const toast = useToast();

  const [caseItem, setCaseItem] = useState(null);
  const [order, setOrder] = useState(null);
  const [tickets, setTickets] = useState([]);
  const [repairTransaction, setRepairTransaction] = useState(null);

  const [loading, setLoading] = useState(true);
  const [relatedLoading, setRelatedLoading] = useState(false);
  const [error, setError] = useState("");

  const [resolution, setResolution] = useState("");
  const [busyAction, setBusyAction] = useState("");

  const [evidenceUrl, setEvidenceUrl] = useState("");
  const [showEvidence, setShowEvidence] = useState(false);
  const [evidenceLoadFailed, setEvidenceLoadFailed] = useState(false);
  const [confirmation, setConfirmation] = useState(null);
  const confirmationResolver = useRef(null);

  function requestConfirmation(details) {
    return new Promise((resolve) => {
      confirmationResolver.current = resolve;
      setConfirmation(details);
    });
  }

  function settleConfirmation(confirmed) {
    const resolve = confirmationResolver.current;
    confirmationResolver.current = null;
    setConfirmation(null);
    resolve?.(confirmed);
  }

  const loadDetail = useCallback(async () => {
    setLoading(true);
    setError("");
    setOrder(null);
    setTickets([]);
    setRepairTransaction(null);

    try {
      const caseResponse = await supportCaseApi.getCaseDetail(id);
      const loadedCase = unwrap(caseResponse);

      if (!loadedCase) {
        throw new Error("Ticket dispute case was not found.");
      }

      setCaseItem(loadedCase);

      const evidenceName = getEvidenceName(loadedCase.description);

      if (evidenceName) {
        const localImage =
          localStorage.getItem(`cs_evidence_case_${loadedCase.caseId}`) ||
          localStorage.getItem(`cs_evidence_${loadedCase.caseNumber}`) ||
          localStorage.getItem(`cs_evidence_${evidenceName}`) ||
          localStorage.getItem(
            `cs_evidence_order_${loadedCase.orderId}`
          ) ||
          localStorage.getItem(`evidence_${evidenceName}`);

        setEvidenceUrl(
          localImage ||
            `/uploads/${encodeURIComponent(evidenceName)}`
        );
      } else {
        setEvidenceUrl("");
      }

      const orderId = loadedCase.orderId;
      const ticketId = loadedCase.ticketId;
      const isPaymentDispute =
        loadedCase.category === "Payment Dispute";

      if (!orderId && !ticketId) {
        return;
      }

      setRelatedLoading(true);

      const [orderResult, ticketResult, exceptionResult] =
        await Promise.allSettled([
          orderId
            ? ticketApi.getOrderById(orderId)
            : Promise.resolve(null),

          orderId
            ? ticketApi.getAdminTickets({
                orderId,
                page: 1,
                pageSize: 100,
              })
            : ticketId
              ? ticketApi.getAdminTicketDetails(ticketId)
              : Promise.resolve(null),

          orderId && isPaymentDispute
            ? paymentReconciliationApi.getExceptions({
                orderId,
                page: 1,
                pageSize: 100,
              })
            : Promise.resolve(null),
        ]);

      let loadedOrder = null;

      if (orderResult.status === "fulfilled" && orderResult.value) {
        loadedOrder = unwrap(orderResult.value);
        setOrder(loadedOrder);
      }

      let loadedTickets = [];

      if (ticketResult.status === "fulfilled" && ticketResult.value) {
        const ticketData = unwrap(ticketResult.value);

        if (Array.isArray(ticketData)) {
          loadedTickets = ticketData;
        } else if (ticketData?.items || ticketData?.Items) {
          loadedTickets = getPageItems(ticketResult.value);
        } else if (ticketData?.ticketId) {
          loadedTickets = [ticketData];
        }
      }

      if (
        ticketId &&
        !loadedTickets.some(
          (ticket) => Number(ticket.ticketId) === Number(ticketId)
        )
      ) {
        try {
          const specificTicket =
            await ticketApi.getAdminTicketDetails(ticketId);
          const ticket = unwrap(specificTicket);

          if (ticket) {
            loadedTickets = [ticket, ...loadedTickets];
          }
        } catch {
          // Keep any tickets already loaded for the order.
        }
      }

      setTickets(loadedTickets);

      if (
        exceptionResult.status === "fulfilled" &&
        exceptionResult.value &&
        loadedOrder
      ) {
        const exceptions = getPageItems(exceptionResult.value);
        const candidate = exceptions.find((item) =>
          isRepairCandidate(item, loadedOrder)
        );

        setRepairTransaction(candidate || null);
      }
    } catch (requestError) {
      const message =
        requestError?.response?.data?.message ||
        requestError?.message ||
        "Could not load the ticket dispute.";

      setError(message);
      setCaseItem(null);
      toast.error(message);
    } finally {
      setRelatedLoading(false);
      setLoading(false);
    }
  }, [id, toast]);

  useEffect(() => {
    loadDetail();
  }, [loadDetail]);

  useEffect(() => {
    if (!confirmation) return undefined;

    const handleKeyDown = (event) => {
      if (event.key === "Escape") {
        settleConfirmation(false);
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [confirmation]);

  async function closeCase(resolutionText) {
    const response = await supportCaseApi.resolveCase(id, {
      resolution: resolutionText,
    });

    setCaseItem(unwrap(response));
    setResolution("");
    toast.success("Ticket dispute resolved and closed.");
  }

  async function handleDismissCase() {
    const reason = resolution.trim();
    if (!reason) {
      toast.error("Enter the investigation reason before dismissing this report.");
      return;
    }

    const confirmed = await requestConfirmation({
      eyebrow: "Close report without repair",
      title: "Dismiss this report?",
      message: "The report will be marked Dismissed and removed from the active ticket dispute list. The reason will be saved in its history.",
      reference: caseItem.caseNumber,
      confirmLabel: "Dismiss report",
      tone: "danger",
      icon: "×",
    });
    if (!confirmed) return;

    setBusyAction("dismiss");
    try {
      const response = await supportCaseApi.dismissCase(id, { reason });
      setCaseItem(unwrap(response));
      setResolution("");
      toast.success("Report dismissed and removed from active reports.");
    } catch (requestError) {
      toast.error(requestError?.response?.data?.message || "Could not dismiss this report.");
    } finally {
      setBusyAction("");
    }
  }

  async function handleTicketAction(ticket, action) {
    const reason = resolution.trim();

    if (!reason) {
      toast.error("Enter the resolution details first.");
      return;
    }

    const confirmed = await requestConfirmation({
      eyebrow: "Replace incomplete ticket",
      title: "Reissue this ticket?",
      message: "The ticket data review found missing information. The current ticket and QR code will be revoked and a replacement ticket will be created.",
      reference: `Ticket #${ticket.ticketId}`,
      confirmLabel: "Reissue ticket",
      tone: "primary",
      icon: "↻",
    });

    if (!confirmed) return;

    setBusyAction(`${action}-${ticket.ticketId}`);

    try {
      if (action === "reissue") {
        const response = await ticketApi.reissueTicket(
          ticket.ticketId,
          reason
        );

        const replacement = unwrap(response);

        await closeCase(
          `${reason}\n\nAction: Reissued ticket #${ticket.ticketId}` +
            (replacement?.ticketId
              ? ` with replacement ticket #${replacement.ticketId}.`
              : ".")
        );
      }

      await loadDetail();
    } catch (requestError) {
      toast.error(
        requestError?.response?.data?.message ||
          "Could not complete the ticket action."
      );
    } finally {
      setBusyAction("");
    }
  }

  async function handleIssueMissingTickets() {
    const reason = resolution.trim();

    if (!reason) {
      toast.error("Enter the resolution details first.");
      return;
    }

    if (!repairTransaction) {
      toast.error(
        "There is no verified successful payment eligible for ticket repair."
      );
      return;
    }

    const confirmed = await requestConfirmation({
      eyebrow: "Paid order recovery",
      title: "Issue missing tickets?",
      message:
        "The system will create the missing tickets or QR codes for this verified paid order, then close the dispute.",
      reference: `Order #${caseItem.orderId}`,
      confirmLabel: "Issue missing tickets",
      tone: "primary",
      icon: "↻",
    });

    if (!confirmed) return;

    setBusyAction("repair");

    try {
      await paymentReconciliationApi.resolveException(
        repairTransaction.paymentTransactionId,
        {
          resolutionAction: "RepairPaidOrderArtifacts",
          resolutionStatus: "Resolved",
          resolutionNote: reason,
        }
      );

      await closeCase(
        `${reason}\n\nAction: Issued missing tickets or QR codes for order #${caseItem.orderId}.`
      );

      await loadDetail();
    } catch (requestError) {
      toast.error(
        requestError?.response?.data?.message ||
          "Could not issue the missing tickets."
      );
    } finally {
      setBusyAction("");
    }
  }

  if (loading) {
    return (
      <AdminShell title="Ticket Dispute Details">
        <main className="ticket-dispute-detail-page">
          <div className="ticket-dispute-detail-state">
            <span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
          
          
        </main>
      </AdminShell>
    );
  }

  if (error || !caseItem) {
    return (
      <AdminShell title="Ticket Dispute Details">
        <main className="ticket-dispute-detail-page">
          <Link to="/admin/ticket-disputes" className="ticket-dispute-back">
            ← Back to Ticket Disputes
          </Link>

          <div className="ticket-dispute-detail-state error">
            {error || "Ticket dispute case not found."}
          </div>
        </main>
      </AdminShell>
    );
  }

  const isClosed = ["closed", "resolved", "dismissed"].includes(
    String(caseItem.status || "").toLowerCase()
  );

  const isInvalidTicket = caseItem.category === "Invalid Ticket";
  const isPaymentDispute = caseItem.category === "Payment Dispute";
  const ticketIssuesById = new Map(tickets.map((ticket) => [Number(ticket.ticketId), getTicketDataIssues(ticket)]));
  const evidenceName = getEvidenceName(caseItem.description);

  return (
    <AdminShell title={`Ticket Dispute ${caseItem.caseNumber}`}>
      <main className="ticket-dispute-detail-page">

        <header className="ticket-dispute-detail-header">
          <div className="ticket-dispute-header-copy">
            <p className="ticket-dispute-eyebrow">Support case · investigation</p>
            <h1>{caseItem.title}</h1>
            <p className="ticket-dispute-header-description">
              Review the customer report and related ticket records, then take
              the appropriate action for this dispute.
            </p>
          </div>

          <div className="ticket-dispute-badges" aria-label="Case attributes">
            <span className="ticket-dispute-case-number">{caseItem.caseNumber}</span>
            <span className="ticket-dispute-badge">{caseItem.category}</span>
            <span className={`ticket-dispute-priority ${String(caseItem.priority || "").toLowerCase()}`}>
              <i aria-hidden="true" /> {caseItem.priority}
            </span>
            <span className={`ticket-dispute-status ${isClosed ? "closed" : "open"}`}>
              <i aria-hidden="true" /> {caseItem.status}
            </span>
          </div>
        </header>

        <div className="ticket-dispute-detail-grid">
          <div className="ticket-dispute-main-column">
            <section className="ticket-dispute-card report-card">
              <div className="ticket-dispute-card-heading">
                <div>
                  <span className="ticket-dispute-section-kicker">01 · Report</span>
                  <h2>Customer report</h2>
                </div>
                {evidenceName && evidenceUrl && (
                  <button
                    type="button"
                    className="ticket-dispute-link-button"
                    onClick={() => {
                      setEvidenceLoadFailed(false);
                      setShowEvidence(true);
                    }}
                  >
                    View evidence <span aria-hidden="true">↗</span>
                  </button>
                )}
              </div>
              <p className="ticket-dispute-description">
                {cleanDescription(caseItem.description) || "No description was provided."}
              </p>
              {evidenceName && evidenceUrl && (
                <div className="ticket-dispute-evidence-file">
                  <span className="ticket-dispute-file-icon" aria-hidden="true">IMG</span>
                  <span className="ticket-dispute-evidence-file-copy">
                    <small>Supporting evidence</small>
                    <strong>{evidenceName}</strong>
                  </span>
                  <span className="ticket-dispute-file-type">Image</span>
                </div>
              )}
            </section>

            <section className="ticket-dispute-card">
              <div className="ticket-dispute-card-heading">
                <div>
                  <span className="ticket-dispute-section-kicker">02 · Order context</span>
                  <h2>Order information</h2>
                </div>
                {order?.orderId && <span className="ticket-dispute-reference">Order #{order.orderId}</span>}
              </div>

              {relatedLoading ? (
                <p className="ticket-dispute-inline-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></p>
              ) : order ? (
                <>
                  <dl className="ticket-dispute-order-summary">
                    <div>
                      <dt>Order status</dt>
                      <dd><span className={`ticket-dispute-order-status ${String(order.status || "").toLowerCase()}`}>{order.status || "Unknown"}</span></dd>
                    </div>
                    <div><dt>Order date</dt><dd>{formatDate(order.orderDate)}</dd></div>
                    <div><dt>Final amount</dt><dd className="ticket-dispute-money">{formatMoney(order.finalAmount)}</dd></div>
                    <div><dt>Payment method</dt><dd>{order.paymentMethod || "Not available"}</dd></div>
                  </dl>

                  <div className="ticket-dispute-subsection-heading">
                    <h3>Ordered ticket types</h3>
                    <span>{order.orderDetails?.length || 0} types</span>
                  </div>
                  {order.orderDetails?.length ? (
                    <div className="ticket-dispute-table-wrap">
                      <table className="ticket-dispute-table">
                        <thead><tr><th>Ticket type</th><th>Quantity</th><th>Unit price</th></tr></thead>
                        <tbody>
                          {order.orderDetails.map((detail) => (
                            <tr key={detail.orderDetailId}>
                              <td><strong>{detail.ticketTypeName || "Unknown type"}</strong></td>
                              <td>{detail.quantity}</td>
                              <td>{formatMoney(detail.unitPrice)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : <p className="ticket-dispute-empty-inline">No order ticket details found.</p>}
                </>
              ) : <div className="ticket-dispute-empty-panel"><strong>No linked order</strong><span>This report does not include an order reference.</span></div>}
            </section>

            {isInvalidTicket && (
              <section className="ticket-dispute-card">
                <div className="ticket-dispute-card-heading">
                  <div>
                    <span className="ticket-dispute-section-kicker">03 · Ticket review</span>
                    <h2>Tickets to inspect</h2>
                  </div>
                  {tickets.length > 0 && <span className="ticket-dispute-count-pill">{tickets.length} {tickets.length === 1 ? "ticket" : "tickets"}</span>}
                </div>
                <div className="ticket-dispute-info-banner">
                  <span aria-hidden="true">i</span>
                  <p>Every ticket is checked for its code, order, event, ticket type, owner, status, price, and QR code. Reissue is available only when at least one required item is missing. Otherwise, dismiss the report.</p>
                </div>
                {relatedLoading ? (
                  <p className="ticket-dispute-inline-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></p>
                ) : tickets.length === 0 ? (
                  <div className="ticket-dispute-empty-panel"><strong>No ticket records found</strong><span>No tickets could be loaded for this order.</span></div>
                ) : (
                  <div className="ticket-dispute-table-wrap">
                    <table className="ticket-dispute-table ticket-dispute-ticket-table">
                      <thead><tr><th>Ticket</th><th>Type / seat</th><th>Attendee</th><th>Status</th><th>Check-in</th>{!isClosed && <th>Data review / action</th>}</tr></thead>
                      <tbody>
                        {tickets.map((ticket) => (
                          <tr key={ticket.ticketId}>
                            {(() => {
                              const issues = ticketIssuesById.get(Number(ticket.ticketId)) || [];
                              return <>
                            <td><strong>#{ticket.ticketId}</strong><small>{ticket.ticketCode || "Code unavailable"}</small></td>
                            <td><strong>{ticket.ticketTypeName || "Unknown type"}</strong><small>Seat {ticket.seatId ?? "Not assigned"}</small></td>
                            <td>{ticket.ownerName || "Not assigned"}</td>
                            <td><span className={`ticket-dispute-ticket-status ${String(ticket.status || "unknown").toLowerCase()}`}>{ticket.status || "Unknown"}</span></td>
                            <td>{formatDate(ticket.checkedInAt)}</td>
                            {!isClosed && (
                              <td>
                                <div className="ticket-dispute-row-actions">
                                  <div className="ticket-dispute-data-review">
                                    {issues.length ? <><strong>Missing information</strong><small>{issues.join(" · ")}</small></> : <><strong>Complete</strong><small>All required ticket information is present.</small></>}
                                  </div>
                                  {issues.length > 0 && <button type="button" className="ticket-dispute-action-button" disabled={!resolution.trim() || Boolean(busyAction)} onClick={() => handleTicketAction(ticket, "reissue")}>
                                    {busyAction === `reissue-${ticket.ticketId}` ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Reissue"}
                                  </button>}
                                </div>
                              </td>
                            )}
                              </>;
                            })()}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>
            )}

            {isPaymentDispute && (
              <section className="ticket-dispute-card ticket-dispute-recovery-card">
                <div className="ticket-dispute-card-heading">
                  <div>
                    <span className="ticket-dispute-section-kicker">03 · Payment recovery</span>
                    <h2>Missing ticket recovery</h2>
                  </div>
                  <span className={`ticket-dispute-eligibility ${repairTransaction ? "eligible" : "unavailable"}`}>
                    <i aria-hidden="true" /> {repairTransaction ? "Verified for recovery" : "Eligibility check"}
                  </span>
                </div>
                <div className="ticket-dispute-recovery-metrics">
                  <div><span>Tickets issued</span><strong>{tickets.length} <small>/ {order?.orderDetails?.reduce((total, item) => total + Number(item.quantity || 0), 0) || 0}</small></strong></div>
                  <div><span>Order status</span><strong>{order?.status || "Not available"}</strong></div>
                </div>
                {isClosed ? (
                  <div className="ticket-dispute-empty-panel"><strong>Dispute closed</strong><span>Recovery actions are unavailable for a closed dispute.</span></div>
                ) : (
                  <>
                    {!repairTransaction && <p className="ticket-dispute-warning">No matching verified successful payment exception was found. Ticket recovery is unavailable until payment and order eligibility are confirmed.</p>}
                    <button type="button" className="ticket-dispute-action-button recovery-action" disabled={!resolution.trim() || !repairTransaction || Boolean(busyAction)} onClick={handleIssueMissingTickets}>
                      {busyAction === "repair" ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : <>Issue missing tickets <span aria-hidden="true">→</span></>}
                    </button>
                  </>
                )}
              </section>
            )}

            <section className="ticket-dispute-card">
              <div className="ticket-dispute-card-heading">
                <div>
                  <span className="ticket-dispute-section-kicker">04 · Activity</span>
                  <h2>Investigation history</h2>
                </div>
                <span className="ticket-dispute-count-pill">{caseItem.notes?.length || 0} updates</span>
              </div>
              {caseItem.notes?.length ? (
                <ol className="ticket-dispute-timeline">
                  {caseItem.notes.map((note) => (
                    <li key={note.noteId}>
                      <span className="ticket-dispute-timeline-dot" aria-hidden="true" />
                      <div className="ticket-dispute-timeline-entry">
                        <div className="ticket-dispute-timeline-meta">
                          <strong>{note.authorName}</strong>
                          <span>{note.authorRole} · {note.noteType}</span>
                          <time>{formatDate(note.createdAt)}</time>
                        </div>
                        <p>{note.content}</p>
                      </div>
                    </li>
                  ))}
                </ol>
              ) : <div className="ticket-dispute-empty-panel"><strong>No investigation updates yet</strong><span>Case notes and actions will appear here.</span></div>}
            </section>
          </div>

          <aside className="ticket-dispute-side-column">
            <section className="ticket-dispute-card ticket-dispute-case-card">
              <div className="ticket-dispute-card-heading compact-heading">
                <div><span className="ticket-dispute-section-kicker">Case overview</span><h2>Case details</h2></div>
              </div>
              <dl className="ticket-dispute-side-details">
                <div><dt>Case number</dt><dd className="mono">{caseItem.caseNumber}</dd></div>
                <div><dt>Category</dt><dd>{caseItem.category}</dd></div>
                <div><dt>Priority</dt><dd><span className={`ticket-dispute-priority-text ${String(caseItem.priority || "").toLowerCase()}`}>{caseItem.priority}</span></dd></div>
                <div><dt>Reported</dt><dd>{formatDate(caseItem.createdAt)}</dd></div>
              </dl>
            </section>

            <section className="ticket-dispute-card">
              <div className="ticket-dispute-card-heading compact-heading">
                <div><span className="ticket-dispute-section-kicker">Reporter</span><h2>Customer</h2></div>
              </div>
              <div className="ticket-dispute-customer-identity">
                <span className="ticket-dispute-avatar" aria-hidden="true">{(caseItem.customerName || "C").trim().charAt(0).toUpperCase()}</span>
                <div><strong>{caseItem.customerName || "Unknown customer"}</strong><span>{caseItem.customerEmail || "Email unavailable"}</span></div>
              </div>
              <dl className="ticket-dispute-customer-meta">
                <div><dt>Phone</dt><dd>{caseItem.customerPhone || "Not provided"}</dd></div>
                {caseItem.orderId && <div><dt>Related order</dt><dd>#{caseItem.orderId}</dd></div>}
                {caseItem.ticketId && <div><dt>Reported ticket</dt><dd>#{caseItem.ticketId}</dd></div>}
              </dl>
            </section>

            {!isClosed ? (
              <section className="ticket-dispute-card ticket-dispute-resolution-card">
                <div className="ticket-dispute-card-heading compact-heading">
                  <div><span className="ticket-dispute-section-kicker">Required for actions</span><h2>Resolution note</h2></div>
                </div>
                <p className="ticket-dispute-help">Record what was checked and why the selected ticket action is appropriate.</p>
                <textarea
                  className="ticket-dispute-resolution-input"
                  rows={5}
                  maxLength={2000}
                  value={resolution}
                  onChange={(event) => setResolution(event.target.value)}
                  placeholder="Investigation findings and action reason…"
                  aria-label="Resolution note"
                />
                <div className="ticket-dispute-char-count"><span>Required before taking action</span><span>{resolution.length}/2000</span></div>
                <div className="ticket-dispute-resolution-hint"><span aria-hidden="true">i</span> Your note is saved with the selected ticket action.</div>
                <button type="button" className="ticket-dispute-action-button danger ticket-dispute-dismiss-button" disabled={!resolution.trim() || Boolean(busyAction)} onClick={handleDismissCase}>
                  {busyAction === "dismiss" ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Dismiss report"}
                </button>
              </section>
            ) : caseItem.resolution ? (
              <section className="ticket-dispute-card ticket-dispute-recorded-resolution">
                <div className="ticket-dispute-card-heading compact-heading">
                  <div><span className="ticket-dispute-section-kicker">Completed</span><h2>Recorded resolution</h2></div>
                </div>
                <p>{caseItem.resolution}</p>
                <div className="ticket-dispute-resolution-byline"><strong>{caseItem.resolvedByName || "Admin"}</strong><span>{formatDate(caseItem.resolvedAt)}</span></div>
              </section>
            ) : null}
          </aside>
        </div>

        {showEvidence && (
          <div
            className="ticket-dispute-evidence-overlay"
            onClick={() => setShowEvidence(false)}
          >
            <section
              className="ticket-dispute-evidence-modal"
              role="dialog"
              aria-modal="true"
              onClick={(event) => event.stopPropagation()}
            >
              <header>
                <strong>{evidenceName}</strong>
                <button
                  type="button"
                  onClick={() => setShowEvidence(false)}
                  aria-label="Close evidence preview"
                >
                  ×
                </button>
              </header>

              {evidenceLoadFailed ? (
                <p className="ticket-dispute-warning">
                  Could not load the evidence image on this device.
                </p>
              ) : (
                <img
                  src={evidenceUrl}
                  alt={`Evidence ${evidenceName}`}
                  onError={() => setEvidenceLoadFailed(true)}
                />
              )}
            </section>
          </div>
        )}

        {confirmation && (
          <div
            className="ticket-dispute-confirm-overlay"
            onMouseDown={(event) => {
              if (event.target === event.currentTarget) {
                settleConfirmation(false);
              }
            }}
          >
            <section
              className="ticket-dispute-confirm-dialog"
              role="alertdialog"
              aria-modal="true"
              aria-labelledby="ticket-dispute-confirm-title"
              aria-describedby="ticket-dispute-confirm-message"
            >
              <span className={`ticket-dispute-confirm-icon ${confirmation.tone}`} aria-hidden="true">
                {confirmation.icon}
              </span>
              <p className="ticket-dispute-confirm-eyebrow">{confirmation.eyebrow}</p>
              <h2 id="ticket-dispute-confirm-title">{confirmation.title}</h2>
              <p id="ticket-dispute-confirm-message" className="ticket-dispute-confirm-message">
                {confirmation.message}
              </p>
              <span className="ticket-dispute-confirm-reference">{confirmation.reference}</span>
              <div className="ticket-dispute-confirm-actions">
                <button
                  type="button"
                  className="ticket-dispute-confirm-cancel"
                  onClick={() => settleConfirmation(false)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className={`ticket-dispute-confirm-submit ${confirmation.tone}`}
                  onClick={() => settleConfirmation(true)}
                >
                  {confirmation.confirmLabel}
                </button>
              </div>
            </section>
          </div>
        )}
      </main>
    </AdminShell>
  );
}
