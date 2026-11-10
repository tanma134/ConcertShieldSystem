import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import reportApi, { saveBlob } from "../../api/reportApi";
import { apiErrorMessage } from "../../utils/apiError";
import { formatDate, formatPrice } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./OrganizerDashboardPage.css";
import "../../styles/reportPages.css";

const EMPTY_FILTERS = { from: "", to: "", ticketTypeId: "" };

// A "yyyy-MM-dd" day from the API as a readable date, parsed in local time.
const formatDay = (value) => formatDate(`${value}T00:00:00`);

// Money cell: refunds and discounts are shown as negative amounts.
const money = (value, negative = false) => {
  const amount = Number(value || 0);
  if (!amount) return "0";
  return (negative ? "-" : "") + amount.toLocaleString("en-US");
};

// UC_13: revenue dashboard of one event with date / ticket type filters and CSV export.
export default function RevenueDashboardPage() {
  const { id } = useParams();
  const [draft, setDraft] = useState(EMPTY_FILTERS);
  const [applied, setApplied] = useState(EMPTY_FILTERS);
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filterError, setFilterError] = useState("");
  const [exporting, setExporting] = useState(false);

  // UC_13.1 / UC_13.2: loads the report for the filters that were applied.
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError("");

    reportApi
      .getRevenue(id, applied)
      .then((res) => {
        if (!cancelled) setReport(res.data?.data || null);
      })
      .catch((err) => {
        if (!cancelled) setError(apiErrorMessage(err, "Could not load the revenue report."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id, applied]);

  // Updates one filter field in the draft (nothing is requested until Apply).
  const setField = (name, value) => {
    setDraft((current) => ({ ...current, [name]: value }));
    setFilterError("");
  };

  // UC_13.2: validates the dates, then applies the draft filters.
  const applyFilters = () => {
    if (draft.from && draft.to && draft.from > draft.to) {
      setFilterError("The start date must not be after the end date.");
      return;
    }
    setApplied(draft);
  };

  // Clears every filter and reloads the full report.
  const resetFilters = () => {
    setDraft(EMPTY_FILTERS);
    setApplied(EMPTY_FILTERS);
    setFilterError("");
  };

  // UC_13.3: downloads the CSV for the applied filters.
  const exportCsv = async () => {
    setExporting(true);
    try {
      const response = await reportApi.exportRevenue(id, applied);
      saveBlob(response, `revenue-event-${id}.csv`);
    } catch (err) {
      setFilterError(apiErrorMessage(err, "Could not export the report."));
    } finally {
      setExporting(false);
    }
  };

  const summary = report?.summary;
  const days = summary?.byDay || [];
  const types = summary?.byTicketType || [];
  const maxDayNet = Math.max(1, ...days.map((day) => Math.abs(day.net)));

  return (
    <OrganizerShell title="Revenue Dashboard">

      <div className="tb-container rp-wrap">
        <div className="od-head">
          <div>
            <p className="od-eyebrow">Organizer · Revenue</p>
            <h1>{report?.eventTitle || "Revenue dashboard"}</h1>
          </div>
          <div style={{ display: "flex", gap: "10px" }}>
            <Link to={`/organizer/events/${id}/staff`} className="tb-btn tb-btn-outline">
              Check-in &amp; staff
            </Link>
            <Link to="/organizer/dashboard" className="tb-btn tb-btn-outline">
              ← Dashboard
            </Link>
          </div>
        </div>

        <div className="rp-toolbar">
          <label className="ow-field">
            <span>From</span>
            <input type="date" value={draft.from} onChange={(event) => setField("from", event.target.value)} />
          </label>
          <label className="ow-field">
            <span>To</span>
            <input type="date" value={draft.to} onChange={(event) => setField("to", event.target.value)} />
          </label>
          <label className="ow-field">
            <span>Ticket type</span>
            <select value={draft.ticketTypeId} onChange={(event) => setField("ticketTypeId", event.target.value)}>
              <option value="">All ticket types</option>
              {(report?.ticketTypes || []).map((type) => (
                <option key={type.ticketTypeId} value={type.ticketTypeId}>
                  {type.name}
                </option>
              ))}
            </select>
          </label>
          <button type="button" className="tb-btn tb-btn-primary" onClick={applyFilters}>
            Apply
          </button>
          <button type="button" className="tb-btn tb-btn-outline" onClick={resetFilters}>
            Reset
          </button>
          <span className="rp-toolbar-spacer" />
          <button type="button" className="tb-btn tb-btn-outline" disabled={exporting} onClick={exportCsv}>
            {exporting ? "Exporting..." : "Export CSV"}
          </button>
        </div>
        {filterError && <div className="tb-error" style={{ marginBottom: 14 }}>{filterError}</div>}

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && summary && (
          <>
            <div className="rp-cards">
              <div className="od-card">
                <span className="od-card-label">Gross revenue</span>
                <span className="od-card-value">{formatPrice(summary.grossRevenue)}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Discounts</span>
                <span className="od-card-value">{formatPrice(summary.discounts)}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Refunded</span>
                <span className="od-card-value">{formatPrice(summary.refunded)}</span>
              </div>
              <div className="od-card od-card-highlight">
                <span className="od-card-label">Net revenue</span>
                <span className="od-card-value">{formatPrice(summary.netRevenue)}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Orders</span>
                <span className="od-card-value">{summary.ordersCount}</span>
              </div>
              <div className="od-card">
                <span className="od-card-label">Tickets sold</span>
                <span className="od-card-value">{summary.ticketsSold}</span>
              </div>
            </div>

            {days.length === 0 && <div className="tb-empty">No paid orders match these filters.</div>}

            {days.length > 0 && (
              <div className="rp-section">
                <h2>Revenue by day</h2>
                <div className="od-table-wrap">
                  <table className="ow-table">
                    <thead>
                      <tr>
                        <th>Date</th>
                        <th className="rp-num">Orders</th>
                        <th className="rp-num">Tickets</th>
                        <th className="rp-num">Gross</th>
                        <th className="rp-num">Discounts</th>
                        <th className="rp-num">Refunded</th>
                        <th className="rp-num">Net</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {days.map((day) => (
                        <tr key={day.date}>
                          <td>{formatDay(day.date)}</td>
                          <td className="rp-num">{day.orders}</td>
                          <td className="rp-num">{day.tickets}</td>
                          <td className="rp-num">{money(day.gross)}</td>
                          <td className="rp-num">{money(day.discounts, true)}</td>
                          <td className="rp-num rp-negative">{money(day.refunded, true)}</td>
                          <td className="rp-num">{money(day.net)}</td>
                          <td className="rp-bar-cell">
                            <div className="rp-bar-track">
                              <div className="rp-bar" style={{ width: `${(Math.max(day.net, 0) / maxDayNet) * 100}%` }} />
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {types.length > 0 && (
              <div className="rp-section">
                <h2>Revenue by ticket type</h2>
                <div className="od-table-wrap">
                  <table className="ow-table">
                    <thead>
                      <tr>
                        <th>Ticket type</th>
                        <th className="rp-num">Tickets</th>
                        <th className="rp-num">Gross</th>
                        <th className="rp-num">Discounts</th>
                        <th className="rp-num">Refunded</th>
                        <th className="rp-num">Net</th>
                      </tr>
                    </thead>
                    <tbody>
                      {types.map((type) => (
                        <tr key={type.ticketTypeId}>
                          <td className="ow-td-title">{type.ticketTypeName}</td>
                          <td className="rp-num">{type.tickets}</td>
                          <td className="rp-num">{money(type.gross)}</td>
                          <td className="rp-num">{money(type.discounts, true)}</td>
                          <td className="rp-num rp-negative">{money(type.refunded, true)}</td>
                          <td className="rp-num">{money(type.net)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </>
        )}
      </div>

      </OrganizerShell>
  );
}
