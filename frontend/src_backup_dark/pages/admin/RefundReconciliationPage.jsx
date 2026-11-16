import { useEffect, useState, useMemo } from "react";
import AdminShell from "./AdminShell";
import refundApi from "../../api/refundApi";
import ProcessRefundModal from "../../components/refund/ProcessRefundModal";
import RetryRefundModal from "../../components/refund/RetryRefundModal";
import { formatPrice } from "../../utils/format";
import { useToast } from "../../components/ToastProvider";
import "./RefundReconciliationPage.css";

export default function RefundReconciliationPage() {
  const toast = useToast();

  const [loading, setLoading] = useState(true);
  const [report, setReport] = useState(null);
  const [error, setError] = useState("");

  const [statusFilter, setStatusFilter] = useState("All");
  const [selectedEventId, setSelectedEventId] = useState("All");
  const [search, setSearch] = useState("");

  // Modals
  const [processOrderId, setProcessOrderId] = useState(null);
  const [promptOrderId, setPromptOrderId] = useState(false);
  const [manualOrderIdInput, setManualOrderIdInput] = useState("");
  const [retryRefundItem, setRetryRefundItem] = useState(null);
  const [inspectRefundItem, setInspectRefundItem] = useState(null);

  const fetchReport = async () => {
    try {
      setLoading(true);
      setError("");
      const params = {};
      if (statusFilter !== "All") params.status = statusFilter;
      if (selectedEventId !== "All") params.eventId = selectedEventId;
      if (search.trim()) params.search = search.trim();

      const res = await refundApi.getReconciliationReport(params);
      const data = res.data?.data || res.data;
      setReport(data);
    } catch (err) {
      setError(
        err.response?.data?.message ||
          "Failed to generate reconciliation report. Please try again later."
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchReport();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, selectedEventId]);

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    fetchReport();
  };

  const handleProcessRefundSuccess = (res) => {
    toast.success(
      `Refund processed successfully (${res.reference || "Gateway confirmed"}).`
    );
    fetchReport();
  };

  const handleRetryRefundSuccess = (res) => {
    toast.success(`Refund retried successfully (${res.reference || "Completed"}).`);
    fetchReport();
  };

  const handleOpenProcessOrder = (e) => {
    e.preventDefault();
    if (!manualOrderIdInput.trim()) return;
    setProcessOrderId(manualOrderIdInput.trim());
    setPromptOrderId(false);
    setManualOrderIdInput("");
  };

  const handleExportCsv = () => {
    if (!refunds || refunds.length === 0) {
      toast.error("No refund reconciliation data to export.");
      return;
    }

    const headers = [
      "Refund Reference",
      "Order ID",
      "Customer Name",
      "Event ID",
      "Event Name",
      "Refund Type",
      "Requested Amount (VND)",
      "Gateway Amount (VND)",
      "Status",
      "Reconciliation Match",
      "Reason",
      "Processed By",
      "Gateway Error Code",
      "Created At",
      "Completed At",
    ];

    const escapeCsv = (val) => {
      if (val === null || val === undefined) return '""';
      const str = String(val).replace(/"/g, '""');
      return `"${str}"`;
    };

    const rows = refunds.map((r) => [
      escapeCsv(r.refundReference),
      escapeCsv(r.orderId),
      escapeCsv(r.customerName),
      escapeCsv(r.eventId || ""),
      escapeCsv(r.eventName || ""),
      escapeCsv(r.refundType),
      escapeCsv(r.amount),
      escapeCsv(r.gatewayAmount || 0),
      escapeCsv(r.status),
      escapeCsv(r.matchStatus),
      escapeCsv(r.reason || ""),
      escapeCsv(r.processedByUserName || ""),
      escapeCsv(r.gatewayErrorCode || ""),
      escapeCsv(r.createdAt || ""),
      escapeCsv(r.completedAt || ""),
    ]);

    const csvContent =
      "\uFEFF" +
      [headers.join(","), ...rows.map((row) => row.join(","))].join("\r\n");

    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    const timestamp = new Date().toISOString().slice(0, 10);
    const eventPrefix =
      selectedEventId !== "All" ? `Event_${selectedEventId}_` : "";
    link.setAttribute(
      "download",
      `Refund_Reconciliation_${eventPrefix}${timestamp}.csv`
    );
    link.setAttribute("href", url);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
    toast.success(`Exported ${refunds.length} reconciliation records successfully.`);
  };

  const refunds = report?.refunds || [];
  const availableEvents = report?.availableEvents || [];
  const totalCount = report?.totalRefunds ?? refunds.length;
  const totalAmount = report?.totalAmount ?? 0;
  const pendingCount =
    report?.pendingReconciliation ??
    refunds.filter((r) => r.status === "Pending").length;
  const mismatchedCount =
    report?.mismatchedCount ??
    refunds.filter(
      (r) => r.matchStatus === "Mismatched" || r.status === "Failed"
    ).length;

  const formatMillions = (amount) => {
    if (!amount) return "₫ 0";
    if (amount >= 1_000_000) {
      return `₫ ${(amount / 1_000_000).toFixed(1)}M`;
    }
    return formatPrice(amount);
  };

  const formatDateDisplay = (dateStr) => {
    if (!dateStr) return "-";
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString("en-US", {
        month: "short",
        day: "2-digit",
        year: "numeric",
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <AdminShell title="Refund Reconciliation Report">
      <div className="rr-container">
        {/* Header */}
        <div className="rr-header">
          <div>
            <h1>Refund Reconciliation Report</h1>
            <p>Compare refunds in ConcertShield against VNPay gateway confirmations.</p>
          </div>
          <div className="rr-header-actions">
            <button
              type="button"
              className="rr-btn-export-csv"
              onClick={handleExportCsv}
              title="Export all filtered records to CSV (Excel compatible)"
            >
              📥 Export CSV
            </button>
            <button
              type="button"
              className="rr-btn-new-refund"
              onClick={() => setPromptOrderId(true)}
            >
              + Process Refund
            </button>
          </div>
        </div>

        {/* Figure 144 KPI Metric Cards */}
        <div className="rr-kpi-grid">
          <div className="rr-kpi-card">
            <span className="rr-kpi-label">
              {selectedEventId === "All" ? "Total Refunds" : "Event Refunds"}
            </span>
            <div className="rr-kpi-val">{totalCount}</div>
            <span className="rr-kpi-trend up">
              {selectedEventId === "All" ? "↑ 12% vs last month" : "Selected event scope"}
            </span>
          </div>

          <div className="rr-kpi-card">
            <span className="rr-kpi-label">Total Amount</span>
            <div className="rr-kpi-val green">{formatMillions(totalAmount)}</div>
            <span className="rr-kpi-trend up">
              {selectedEventId === "All" ? "↑ 8% vs last month" : "Reconciled amount"}
            </span>
          </div>

          <div className="rr-kpi-card">
            <span className="rr-kpi-label">Pending / Mismatched</span>
            <div className="rr-kpi-val orange">{pendingCount + mismatchedCount}</div>
            <span className="rr-kpi-sub">
              {mismatchedCount > 0
                ? `${mismatchedCount} mismatches require action`
                : "Awaiting gateway confirmation"}
            </span>
          </div>
        </div>

        {/* Filter & Search Bar */}
        <div className="rr-toolbar">
          <div className="rr-toolbar-left">
            <form onSubmit={handleSearchSubmit} className="rr-search-form">
              <span className="rr-search-icon">🔍</span>
              <input
                type="text"
                placeholder="Search by refundId or orderId..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="rr-search-input"
              />
              {search && (
                <button
                  type="button"
                  className="rr-search-clear"
                  onClick={() => {
                    setSearch("");
                    setTimeout(fetchReport, 50);
                  }}
                >
                  ✕
                </button>
              )}
            </form>

            {/* Event Dropdown Filter */}
            <div className="rr-event-select-wrapper">
              <span className="rr-event-select-icon">🎵</span>
              <select
                value={selectedEventId}
                onChange={(e) => setSelectedEventId(e.target.value)}
                className="rr-event-select"
                title="Filter by Event"
              >
                <option value="All">All Events ({availableEvents.reduce((s, e) => s + e.refundCount, 0)})</option>
                {availableEvents.map((ev) => (
                  <option key={ev.eventId} value={ev.eventId}>
                    {ev.eventName} ({ev.refundCount})
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="rr-filter-tabs">
            {["All", "Pending", "Mismatched", "Completed"].map((tab) => (
              <button
                key={tab}
                type="button"
                className={`rr-filter-btn ${statusFilter === tab ? "active" : ""}`}
                onClick={() => setStatusFilter(tab)}
              >
                {tab}
              </button>
            ))}
          </div>
        </div>

        {/* Error message */}
        {error && (
          <div className="rr-error-alert" role="alert">
            {error}
          </div>
        )}

        {/* Zero Mismatches Banner (A.1) */}
        {!loading && mismatchedCount === 0 && (
          <div className="rr-banner-match">
            <span className="rr-banner-icon">✓</span>
            <span>
              All compared refund records reconcile successfully (0 mismatches detected).
            </span>
          </div>
        )}

        {/* Table Content */}
        <div className="rr-table-wrapper">
          <table className="rr-table">
            <thead>
              <tr>
                <th>Refund ID</th>
                <th>Order / Customer</th>
                <th>Requested</th>
                <th>Status</th>
                <th>Match</th>
                <th>Date</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="7" className="rr-table-empty">
                    Loading reconciliation records...
                  </td>
                </tr>
              ) : refunds.length === 0 ? (
                <tr>
                  <td colSpan="7" className="rr-table-empty">
                    No refund reconciliation records found.
                  </td>
                </tr>
              ) : (
                refunds.map((r) => {
                  const statusClass = (r.status || "").toLowerCase();
                  const matchClass = (r.matchStatus || "").toLowerCase();

                  return (
                    <tr key={r.paymentRefundId}>
                      <td className="rr-cell-id">
                        <strong>{r.refundReference}</strong>
                        {r.refundType && (
                          <span className="rr-type-pill">{r.refundType}</span>
                        )}
                      </td>
                      <td className="rr-cell-customer">
                        <div className="rr-customer-main">
                          <span className="rr-order-tag">#ORD-{r.orderId}</span> •{" "}
                          <span className="rr-name">{r.customerName}</span>
                        </div>
                        {r.eventName && (
                          <div className="rr-event-subtag" title={`Event ID: #${r.eventId}`}>
                            <span className="rr-event-tag-icon">🎵</span> {r.eventName}
                          </div>
                        )}
                      </td>
                      <td className="rr-cell-amount">
                        <strong>{formatPrice(r.amount)}</strong>
                      </td>
                      <td>
                        <span className={`rr-badge status-${statusClass}`}>
                          {r.status}
                        </span>
                      </td>
                      <td>
                        <span className={`rr-badge match-${matchClass}`}>
                          {r.matchStatus}
                        </span>
                      </td>
                      <td className="rr-cell-date">
                        {formatDateDisplay(r.completedAt || r.createdAt)}
                      </td>
                      <td className="rr-cell-actions">
                        <button
                          type="button"
                          className="rr-action-link"
                          onClick={() => setInspectRefundItem(r)}
                        >
                          Inspect
                        </button>
                        {r.status === "Failed" && (
                          <button
                            type="button"
                            className="rr-action-retry-btn"
                            onClick={() => setRetryRefundItem(r)}
                          >
                            Retry
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Prompt for Order ID when clicking "+ Process Refund" */}
      {promptOrderId && (
        <div className="rf-backdrop" onClick={() => setPromptOrderId(false)}>
          <div className="rf-modal" onClick={(e) => e.stopPropagation()}>
            <div className="rf-header">
              <div className="rf-header-title">
                <span className="rf-icon">⟲</span>
                <div>
                  <h2>Select Order to Refund</h2>
                  <p>Enter the Order ID to issue a full or partial refund.</p>
                </div>
              </div>
              <button
                type="button"
                className="rf-close"
                onClick={() => setPromptOrderId(false)}
              >
                ✕
              </button>
            </div>
            <form onSubmit={handleOpenProcessOrder} className="rf-form">
              <div className="rf-group">
                <label>Order ID *</label>
                <input
                  type="number"
                  placeholder="e.g., 22"
                  value={manualOrderIdInput}
                  onChange={(e) => setManualOrderIdInput(e.target.value)}
                  className="rf-input"
                  required
                  autoFocus
                />
              </div>
              <div className="rf-actions">
                <button
                  type="button"
                  className="rf-btn-cancel"
                  onClick={() => setPromptOrderId(false)}
                >
                  Cancel
                </button>
                <button type="submit" className="rf-btn-submit">
                  Continue
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 1: Figure 142 Process Full / Partial Refund */}
      {processOrderId && (
        <ProcessRefundModal
          orderId={processOrderId}
          onClose={() => setProcessOrderId(null)}
          onSuccess={handleProcessRefundSuccess}
        />
      )}

      {/* Modal 2: Figure 143 Retry Failed Refund */}
      {retryRefundItem && (
        <RetryRefundModal
          refund={retryRefundItem}
          onClose={() => setRetryRefundItem(null)}
          onSuccess={handleRetryRefundSuccess}
        />
      )}

      {/* Modal 3: Inspect Refund Record Details */}
      {inspectRefundItem && (
        <div className="rf-backdrop" onClick={() => setInspectRefundItem(null)}>
          <div className="rf-modal" onClick={(e) => e.stopPropagation()}>
            <div className="rf-header">
              <div className="rf-header-title">
                <span className="rf-icon">ℹ</span>
                <div>
                  <h2>Refund Audit Details</h2>
                  <p>{inspectRefundItem.refundReference}</p>
                </div>
              </div>
              <button
                type="button"
                className="rf-close"
                onClick={() => setInspectRefundItem(null)}
              >
                ✕
              </button>
            </div>
            <div className="rf-form">
              <div className="rf-card">
                <div className="rf-card-row">
                  <span className="rf-label">Refund Reference</span>
                  <span className="rf-value mono">
                    {inspectRefundItem.refundReference}
                  </span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Order</span>
                  <span className="rf-value mono">
                    #ORD-{inspectRefundItem.orderId}
                  </span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Customer</span>
                  <span className="rf-value">
                    {inspectRefundItem.customerName}
                  </span>
                </div>
                {inspectRefundItem.eventName && (
                  <div className="rf-card-row">
                    <span className="rf-label">Event</span>
                    <span className="rf-value highlight">
                      🎵 {inspectRefundItem.eventName} (ID: #{inspectRefundItem.eventId})
                    </span>
                  </div>
                )}
                <div className="rf-card-row">
                  <span className="rf-label">Refund Amount</span>
                  <span className="rf-value highlight">
                    {formatPrice(inspectRefundItem.amount)}
                  </span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Gateway Amount</span>
                  <span className="rf-value">
                    {formatPrice(inspectRefundItem.gatewayAmount || 0)}
                  </span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Status</span>
                  <span className="rf-value">{inspectRefundItem.status}</span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Reconciliation Match</span>
                  <span className="rf-value">
                    {inspectRefundItem.matchStatus}
                  </span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Reason</span>
                  <span className="rf-value">{inspectRefundItem.reason || "-"}</span>
                </div>
                <div className="rf-card-row">
                  <span className="rf-label">Processed By</span>
                  <span className="rf-value">
                    {inspectRefundItem.processedByUserName || "System"}
                  </span>
                </div>
                {inspectRefundItem.retryCount > 0 && (
                  <div className="rf-card-row">
                    <span className="rf-label">Retry Attempts</span>
                    <span className="rf-value">
                      {inspectRefundItem.retryCount}
                    </span>
                  </div>
                )}
                {inspectRefundItem.errorMessage && (
                  <div className="rf-card-row" style={{ color: "#ef4444" }}>
                    <span className="rf-label" style={{ color: "#ef4444" }}>
                      Error Details
                    </span>
                    <span className="rf-value" style={{ color: "#fca5a5" }}>
                      {inspectRefundItem.errorMessage}
                    </span>
                  </div>
                )}
              </div>
              <div className="rf-actions">
                {inspectRefundItem.status === "Failed" && (
                  <button
                    type="button"
                    className="rf-btn-submit"
                    onClick={() => {
                      const item = inspectRefundItem;
                      setInspectRefundItem(null);
                      setRetryRefundItem(item);
                    }}
                  >
                    Retry Refund
                  </button>
                )}
                <button
                  type="button"
                  className="rf-btn-cancel"
                  onClick={() => setInspectRefundItem(null)}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </AdminShell>
  );
}
