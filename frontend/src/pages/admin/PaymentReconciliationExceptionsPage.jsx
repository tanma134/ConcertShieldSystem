import { useEffect, useState } from "react";
import AdminShell from "./AdminShell";
import paymentReconciliationApi from "../../api/paymentReconciliationApi";
import "./PaymentReconciliationExceptionsPage.css";

const PAGE_SIZE = 20;

function formatDate(value) {
  if (!value) return "—";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "—";

  return date.toLocaleString("en-US", {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function formatMoney(value) {
  if (value == null) return "—";

  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
}

function getErrorMessage(error) {
  return (
    error.response?.data?.message ||
    "Could not complete the request. Please try again."
  );
}

function getResolutionAction(item) {
  const reasons = Array.isArray(item?.reasons) ? item.reasons : [];

  // Duplicate successful payments require staff review. Do not automatically
  // confirm payment or repair order artifacts for this exception.
  const hasMultipleSuccessfulPayments = reasons.some((reason) =>
    String(reason).includes(
      "More than one successful payment transaction exists for this order."
    )
  );

  if (hasMultipleSuccessfulPayments) return null;

  const paymentStatus = String(item?.paymentStatus || "").toLowerCase();
  const orderStatus = String(item?.orderStatus || "").toLowerCase();

  if (
    paymentStatus === "success" &&
    orderStatus === "pending" &&
    item.paymentAmount === item.orderAmount
  ) {
    return "ConfirmPayment";
  }

  const hasTicketCounts =
    item?.expectedTicketCount != null && item?.issuedTicketCount != null;
  const missingTickets =
    hasTicketCounts &&
    Number(item.issuedTicketCount) < Number(item.expectedTicketCount);
  const missingQr = Number(item?.ticketsWithoutQrCount) > 0;

  if (
    paymentStatus === "success" &&
    orderStatus === "paid" &&
    (missingTickets || missingQr)
  ) {
    return "RepairPaidOrderArtifacts";
  }

  return null;
}

function getResolutionActionLabel(action) {
  if (action === "ConfirmPayment") return "Confirm payment and issue tickets";
  if (action === "RepairPaidOrderArtifacts") return "Repair missing tickets / QR";
  if (action === "ManualReview") return "Manual review";
  return "Manual review required";
}

export default function PaymentReconciliationExceptionsPage() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [successMessage, setSuccessMessage] = useState("");

  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [appliedFilters, setAppliedFilters] = useState({});

  const [selectedException, setSelectedException] = useState(null);
  const [resolutionAction, setResolutionAction] = useState(null);
  const [resolutionStatus, setResolutionStatus] = useState("Resolved");
  const [resolutionNote, setResolutionNote] = useState("");
  const [savingResolution, setSavingResolution] = useState(false);
  const [resolutionError, setResolutionError] = useState("");

  async function loadExceptions(nextPage, filters = appliedFilters) {
    try {
      setLoading(true);
      setError("");

      const params = {
        page: nextPage,
        pageSize: PAGE_SIZE,
        from: filters.from
          ? new Date(filters.from).toISOString()
          : undefined,
        to: filters.to
          ? new Date(filters.to).toISOString()
          : undefined,
      };

      const response =
        await paymentReconciliationApi.getExceptions(params);

      const result = response.data?.data ?? response.data;

      setItems(Array.isArray(result?.items) ? result.items : []);
      setPage(Number(result?.page) || nextPage);
      setTotalCount(Number(result?.totalCount) || 0);
    } catch (requestError) {
      setError(
        requestError.response?.data?.message ||
          "Could not load payment reconciliation exceptions."
      );
      setItems([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadExceptions(1, {});
    // Load the initial list once.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function handleApplyFilters(event) {
    event.preventDefault();

    const filters = { from, to };
    setAppliedFilters(filters);
    setSuccessMessage("");
    loadExceptions(1, filters);
  }

  function handleClearFilters() {
    setFrom("");
    setTo("");
    setAppliedFilters({});
    setSuccessMessage("");
    loadExceptions(1, {});
  }

  function openResolveModal(exception) {
    setSelectedException(exception);
    setResolutionAction(
      getResolutionAction(exception) ?? "ManualReview"
    );
    setResolutionStatus("Resolved");
    setResolutionNote("");
    setResolutionError("");
  }

  function closeResolveModal() {
    if (savingResolution) return;

    setSelectedException(null);
    setResolutionError("");
  }

  async function handleResolve(event) {
    event.preventDefault();

    if (!selectedException) return;

    if (!resolutionAction) {
      setResolutionError("No resolution action is selected.");
      return;
    }

    if (!resolutionNote.trim()) {
      setResolutionError("Please enter a resolution note.");
      return;
    }

    try {
      setSavingResolution(true);
      setResolutionError("");
      setSuccessMessage("");

      await paymentReconciliationApi.resolveException(
        selectedException.paymentTransactionId,
        {
          resolutionAction,
          resolutionStatus:
            resolutionAction === "ManualReview"
              ? resolutionStatus
              : "Resolved",
          resolutionNote: resolutionNote.trim(),
        }
      );

      const completedStatus =
        resolutionAction === "ManualReview"
          ? resolutionStatus
          : "Resolved";

      setSelectedException(null);
      setResolutionNote("");
      setResolutionStatus("Resolved");
      setSuccessMessage(
        `Transaction #${selectedException.paymentTransactionId}: ${getResolutionActionLabel(
          resolutionAction
        )} ${completedStatus.toLowerCase()}.`
      );

      // Resolved exceptions are excluded from the list, so reload from page 1.
      await loadExceptions(1, appliedFilters);
    } catch (requestError) {
      setResolutionError(getErrorMessage(requestError));
    } finally {
      setSavingResolution(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <AdminShell title="Payment Reconciliation Exceptions">
      <div className="pre-page">
        <header className="pre-heading">
          <div>
            <p className="pre-eyebrow">Payment operations</p>
            <h1>Payment Reconciliation Exceptions</h1>
            <p>
              Review payment transactions that do not match their related
              orders.
            </p>
          </div>

          <div className="pre-total">
            <strong>{totalCount.toLocaleString("en-US")}</strong>
            <span>exceptions</span>
          </div>
        </header>

        <form className="pre-filters" onSubmit={handleApplyFilters}>
          <label>
            <span>Requested from</span>
            <input
              type="datetime-local"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>

          <label>
            <span>Requested to</span>
            <input
              type="datetime-local"
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </label>

          <div className="pre-filter-actions">
            <button type="submit" className="pre-button pre-button-primary">
              Apply filters
            </button>

            <button
              type="button"
              className="pre-button pre-button-secondary"
              onClick={handleClearFilters}
            >
              Clear
            </button>
          </div>
        </form>

        {error && (
          <div className="pre-error" role="alert">
            {error}
          </div>
        )}

        {successMessage && (
          <div className="pre-success" role="status">
            {successMessage}
          </div>
        )}

        <section className="pre-card">
          <div className="pre-card-heading">
            <div>
              <h2>Exception list</h2>
              <p>Each row shows a payment transaction and its order status.</p>
            </div>

            <span>
              Page {page} of {totalPages}
            </span>
          </div>

          {loading ? (
            <div className="pre-state">Loading exceptions...</div>
          ) : items.length === 0 ? (
            <div className="pre-state">
              No payment reconciliation exceptions found.
            </div>
          ) : (
            <div className="pre-table-scroll">
              <table className="pre-table">
                <thead>
                  <tr>
                    <th>Requested</th>
                    <th>Order</th>
                    <th>Event</th>
                    <th>Gateway</th>
                    <th>Payment amount</th>
                    <th>Order amount</th>
                    <th>Payment status</th>
                    <th>Order status</th>
                    <th>Exception reasons</th>
                    <th>Action</th>
                  </tr>
                </thead>

                <tbody>
                  {items.map((item) => {
                    const automaticAction = getResolutionAction(item);

                    return (
                      <tr key={item.paymentTransactionId}>
                        <td>{formatDate(item.requestedAt)}</td>
                        <td>#{item.orderId}</td>
                        <td>{item.eventId ?? "—"}</td>

                        <td>
                          <div>{item.gateway || "—"}</div>
                          {item.gatewayTransactionRef && (
                            <small className="pre-reference">
                              Ref: {item.gatewayTransactionRef}
                            </small>
                          )}
                        </td>

                        <td>{formatMoney(item.paymentAmount)}</td>
                        <td>{formatMoney(item.orderAmount)}</td>

                        <td>
                          <span
                            className={`pre-status pre-status-${String(
                              item.paymentStatus || "unknown"
                            ).toLowerCase()}`}
                          >
                            {item.paymentStatus || "Unknown"}
                          </span>
                        </td>

                        <td>{item.orderStatus || "Order not found"}</td>

                        <td>
                          <ul className="pre-reasons">
                            {(item.reasons || []).map((reason, index) => (
                              <li key={`${item.paymentTransactionId}-${index}`}>
                                {reason}
                              </li>
                            ))}
                          </ul>
                        </td>

                        <td>
                          <button
                            type="button"
                            className={`pre-button ${
                              automaticAction
                                ? "pre-button-resolve"
                                : "pre-button-review"
                            }`}
                            onClick={() => openResolveModal(item)}
                          >
                            {automaticAction ? "Resolve" : "Review"}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          <footer className="pre-pagination">
            <span>
              {totalCount > 0
                ? `Showing ${(page - 1) * PAGE_SIZE + 1}–${Math.min(
                    page * PAGE_SIZE,
                    totalCount
                  )} of ${totalCount}`
                : "0 exceptions"}
            </span>

            <div>
              <button
                type="button"
                className="pre-button pre-button-secondary"
                disabled={loading || page <= 1}
                onClick={() => loadExceptions(page - 1)}
              >
                Previous
              </button>

              <button
                type="button"
                className="pre-button pre-button-secondary"
                disabled={loading || page >= totalPages}
                onClick={() => loadExceptions(page + 1)}
              >
                Next
              </button>
            </div>
          </footer>
        </section>
      </div>

      {selectedException && (
        <div
          className="pre-modal-backdrop"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeResolveModal();
            }
          }}
        >
          <section
            className="pre-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="pre-modal-title"
          >
            <header className="pre-modal-header">
              <div>
                <p className="pre-eyebrow">Payment operations</p>
                <h2 id="pre-modal-title">
                  {resolutionAction === "ManualReview"
                    ? "Review / resolve exception"
                    : "Resolve exception"}
                </h2>
                <p>
                  Review transaction #{selectedException.paymentTransactionId}
                  {" "}for order #{selectedException.orderId}.
                </p>
              </div>

              <button
                type="button"
                className="pre-modal-close"
                aria-label="Close"
                disabled={savingResolution}
                onClick={closeResolveModal}
              >
                ×
              </button>
            </header>

            <div className="pre-modal-body">
              <div className="pre-detail-grid">
                <div>
                  <span>Gateway</span>
                  <strong>{selectedException.gateway || "—"}</strong>
                </div>
                <div>
                  <span>Gateway reference</span>
                  <strong>
                    {selectedException.gatewayTransactionRef || "—"}
                  </strong>
                </div>
                <div>
                  <span>Payment amount</span>
                  <strong>{formatMoney(selectedException.paymentAmount)}</strong>
                </div>
                <div>
                  <span>Order amount</span>
                  <strong>{formatMoney(selectedException.orderAmount)}</strong>
                </div>
                <div>
                  <span>Payment status</span>
                  <strong>{selectedException.paymentStatus || "Unknown"}</strong>
                </div>
                <div>
                  <span>Order status</span>
                  <strong>
                    {selectedException.orderStatus || "Order not found"}
                  </strong>
                </div>
                <div>
                  <span>Requested at</span>
                  <strong>{formatDate(selectedException.requestedAt)}</strong>
                </div>
                <div>
                  <span>Responded at</span>
                  <strong>{formatDate(selectedException.respondedAt)}</strong>
                </div>
                <div>
                  <span>Tickets issued</span>
                  <strong>
                    {selectedException.issuedTicketCount == null ||
                    selectedException.expectedTicketCount == null
                      ? "Not available"
                      : `${selectedException.issuedTicketCount} / ${selectedException.expectedTicketCount}`}
                  </strong>
                </div>
                <div>
                  <span>Tickets without QR</span>
                  <strong>
                    {selectedException.ticketsWithoutQrCount ?? "Not available"}
                  </strong>
                </div>
              </div>

              <div className="pre-modal-reasons">
                <h3>Exception reasons</h3>
                <ul className="pre-reasons">
                  {(selectedException.reasons || []).map((reason, index) => (
                    <li key={index}>{reason}</li>
                  ))}
                </ul>
              </div>

              <form className="pre-resolution-form" onSubmit={handleResolve}>
                <div className="pre-selected-action">
                  <span>Action to perform</span>
                  <strong>
                    {getResolutionActionLabel(resolutionAction)}
                  </strong>

                  {resolutionAction === "ManualReview" && (
                    <small>
                      This records your review and note. It does not confirm
                      payment or issue tickets.
                    </small>
                  )}
                </div>

                {resolutionAction === "ManualReview" && (
                  <label>
                    <span>Resolution status</span>
                    <select
                      value={resolutionStatus}
                      onChange={(event) =>
                        setResolutionStatus(event.target.value)
                      }
                      disabled={savingResolution}
                    >
                      <option value="Resolved">Resolved</option>
                      <option value="Dismissed">Dismissed</option>
                    </select>
                  </label>
                )}

                <label>
                  <span>Resolution note</span>
                  <textarea
                    value={resolutionNote}
                    onChange={(event) => setResolutionNote(event.target.value)}
                    placeholder="Describe what you checked and why you chose this resolution."
                    rows={4}
                    maxLength={2000}
                    required
                    disabled={savingResolution}
                  />
                  <small>{resolutionNote.length}/2000</small>
                </label>

                {resolutionError && (
                  <div className="pre-error" role="alert">
                    {resolutionError}
                  </div>
                )}

                <footer className="pre-modal-actions">
                  <button
                    type="button"
                    className="pre-button pre-button-secondary"
                    disabled={savingResolution}
                    onClick={closeResolveModal}
                  >
                    Cancel
                  </button>

                  <button
                    type="submit"
                    className="pre-button pre-button-primary"
                    disabled={savingResolution || !resolutionNote.trim()}
                  >
                    {savingResolution
                      ? "Processing..."
                      : resolutionAction === "ManualReview"
                        ? resolutionStatus === "Dismissed"
                          ? "Dismiss exception"
                          : "Resolve manually"
                        : "Run action and resolve"}
                  </button>
                </footer>
              </form>
            </div>
          </section>
        </div>
      )}
    </AdminShell>
  );
}