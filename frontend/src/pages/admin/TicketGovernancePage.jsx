import React, { useState, useEffect, useMemo } from "react";
import AdminShell from "./AdminShell";
import ticketApi from "../../api/ticketApi";
import "./TicketGovernancePage.css";

export default function TicketGovernancePage() {
  const [activeTab, setActiveTab] = useState("BULK_REVOKE"); // "BULK_REVOKE", "REVOCATION_HISTORY", "REISSUE_HISTORY"
  const [stats, setStats] = useState({
    totalRevokedTickets: 0,
    totalBulkEvents: 0,
    totalReissuedTickets: 0,
    totalRefundQueued: 0,
  });
  const [loadingStats, setLoadingStats] = useState(false);

  // Tab 1: Bulk Revoke States
  const [events, setEvents] = useState([]);
  const [loadingEvents, setLoadingEvents] = useState(false);
  const [selectedEventId, setSelectedEventId] = useState("");
  const [reasonType, setReasonType] = useState("EVENT_CANCELLED");
  const [reasonNote, setReasonNote] = useState("");
  const [notifyAttendees, setNotifyAttendees] = useState(true);
  const [submittingBulk, setSubmittingBulk] = useState(false);
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [bulkSuccessResult, setBulkSuccessResult] = useState(null);
  const [reconciliations, setReconciliations] = useState([]);
  const [loadingReconciliations, setLoadingReconciliations] = useState(false);

  // Dedicated Pop-up Modals (replacing all window.alert)
  const [noticeDialog, setNoticeDialog] = useState(null); // { type: "WARNING" | "ERROR", title: "", message: "" }
  const [successResultModal, setSuccessResultModal] = useState(null); // Full bulk revoke success details modal

  // Tab 2: Revocation History States
  const [revocations, setRevocations] = useState([]);
  const [revocationTotal, setRevocationTotal] = useState(0);
  const [revocationPage, setRevocationPage] = useState(1);
  const [revocationTotalPages, setRevocationTotalPages] = useState(1);
  const [revocationSearch, setRevocationSearch] = useState("");
  const [revocationReasonFilter, setRevocationReasonFilter] = useState("ALL");
  const [revocationEventFilter, setRevocationEventFilter] = useState("");
  const [loadingRevocations, setLoadingRevocations] = useState(false);

  // Tab 3: Reissue History States
  const [reissues, setReissues] = useState([]);
  const [reissueTotal, setReissueTotal] = useState(0);
  const [reissuePage, setReissuePage] = useState(1);
  const [reissueTotalPages, setReissueTotalPages] = useState(1);
  const [reissueSearch, setReissueSearch] = useState("");
  const [reissueEventFilter, setReissueEventFilter] = useState("");
  const [loadingReissues, setLoadingReissues] = useState(false);

  // Load initial stats & events on mount
  useEffect(() => {
    fetchStats();
    fetchEvents();
    fetchReconciliations();
    fetchRevocations();
    fetchReissues();
  }, []);

  // Fetch when Tab 2 filters/page changes
  useEffect(() => {
    if (activeTab === "REVOCATION_HISTORY") {
      fetchRevocations();
    }
  }, [activeTab, revocationPage, revocationReasonFilter, revocationEventFilter]);

  // Fetch when Tab 3 filters/page changes
  useEffect(() => {
    if (activeTab === "REISSUE_HISTORY") {
      fetchReissues();
    }
  }, [activeTab, reissuePage, reissueEventFilter]);

  const fetchStats = async () => {
    setLoadingStats(true);
    try {
      const res = await ticketApi.getGovernanceStats();
      if (res?.data?.data) {
        setStats(res.data.data);
      }
    } catch (err) {
      console.warn("Could not fetch governance stats:", err);
    } finally {
      setLoadingStats(false);
    }
  };

  const fetchEvents = async () => {
    setLoadingEvents(true);
    try {
      const res = await ticketApi.getGovernanceEvents();
      if (res?.data?.data) {
        const allEvents = res.data.data;
        setEvents(allEvents);
        if (allEvents.length > 0) {
          setSelectedEventId(allEvents[0].eventId);
        } else {
          setSelectedEventId("");
        }
      }
    } catch (err) {
      console.warn("Could not fetch governance events:", err);
    } finally {
      setLoadingEvents(false);
    }
  };

  const fetchReconciliations = async () => {
    setLoadingReconciliations(true);
    try {
      const res = await ticketApi.getReconciliationRecords();
      if (res?.data?.data) {
        setReconciliations(res.data.data);
      }
    } catch (err) {
      console.warn("Could not fetch reconciliations:", err);
    } finally {
      setLoadingReconciliations(false);
    }
  };

  const fetchRevocations = async () => {
    setLoadingRevocations(true);
    try {
      const params = {
        page: revocationPage,
        pageSize: 10,
        search: revocationSearch,
        reasonType: revocationReasonFilter,
      };
      if (revocationEventFilter) params.eventId = revocationEventFilter;

      const res = await ticketApi.getRevocationHistory(params);
      if (res?.data?.data) {
        setRevocations(res.data.data.items || []);
        setRevocationTotal(res.data.data.totalCount || 0);
        setRevocationTotalPages(res.data.data.totalPages || 1);
      }
    } catch (err) {
      console.error("Error fetching revocation history:", err);
    } finally {
      setLoadingRevocations(false);
    }
  };

  const fetchReissues = async () => {
    setLoadingReissues(true);
    try {
      const params = {
        page: reissuePage,
        pageSize: 10,
        search: reissueSearch,
      };
      if (reissueEventFilter) params.eventId = reissueEventFilter;

      const res = await ticketApi.getReissueHistory(params);
      if (res?.data?.data) {
        setReissues(res.data.data.items || []);
        setReissueTotal(res.data.data.totalCount || 0);
        setReissueTotalPages(res.data.data.totalPages || 1);
      }
    } catch (err) {
      console.error("Error fetching reissue history:", err);
    } finally {
      setLoadingReissues(false);
    }
  };

  const handleBulkRevokeSubmit = async () => {
    if (!selectedEventId) {
      setNoticeDialog({
        type: "WARNING",
        title: "Event Required",
        message: "Please select an event with active tickets to revoke.",
      });
      return;
    }
    if (!reasonNote.trim()) {
      setNoticeDialog({
        type: "WARNING",
        title: "Audit Reason Required",
        message: "Please enter detailed justification notes for the reconciliation audit record.",
      });
      return;
    }

    setSubmittingBulk(true);
    try {
      const actualReasonType = selectedEventObj?.status?.toLowerCase().includes("postpone")
        ? "EVENT_POSTPONED"
        : "EVENT_CANCELLED";

      const payload = {
        eventId: Number(selectedEventId),
        reasonType: actualReasonType,
        reasonNote: reasonNote.trim(),
        notifyAttendees,
      };

      const res = await ticketApi.bulkRevokeTickets(payload);
      if (res?.data?.data) {
        setShowConfirmModal(false);
        setReasonNote("");

        // Immediate UI update for Reconciliation Records
        const newRec = {
          reconciliationId: res.data.data.reconciliationId,
          eventId: res.data.data.eventId,
          eventTitle: res.data.data.eventTitle,
          adminId: 1,
          adminName: res.data.data.adminName || "Platform Administrator",
          totalTicketsRevoked: res.data.data.totalTicketsRevoked,
          totalTicketsAffected: res.data.data.totalTicketsRevoked,
          reasonType: res.data.data.reasonType || reasonType,
          reason: res.data.data.revocationReason || reasonNote.trim(),
          notes: res.data.data.revocationReason || reasonNote.trim(),
          executedAt: res.data.data.executedAt || new Date().toISOString(),
          createdAt: res.data.data.executedAt || new Date().toISOString(),
          status: "REVOKED",
        };
        setReconciliations((prev) => [
          newRec,
          ...prev.filter((r) => r.reconciliationId !== newRec.reconciliationId),
        ]);

        const currentEvt = events.find((e) => e.eventId === Number(selectedEventId));
        setSuccessResultModal({
          ...res.data.data,
          eventTitle: currentEvt?.title || res.data.data.eventTitle,
        });
        await Promise.all([
          fetchStats(),
          fetchEvents(),
          fetchReconciliations(),
          fetchRevocations(),
        ]);
      }
    } catch (err) {
      console.error("Bulk revoke error:", err);
      setShowConfirmModal(false);
      setNoticeDialog({
        type: "ERROR",
        title: "Revocation Failed",
        message: err.response?.data?.message || err.message || "An error occurred while executing the bulk revocation transaction.",
      });
    } finally {
      setSubmittingBulk(false);
    }
  };

  const eligibleEvents = events;
  const selectedEventObj = eligibleEvents.find((e) => String(e.eventId) === String(selectedEventId));

  // Automatically update reasonType to match event status (Cancelled or Postponed)
  useEffect(() => {
    if (selectedEventObj) {
      if (selectedEventObj.status?.toLowerCase().includes("postpone")) {
        setReasonType("EVENT_POSTPONED");
      } else {
        setReasonType("EVENT_CANCELLED");
      }
    }
  }, [selectedEventId, events]);

  // Historical events that have revoked / reissued tickets or exist in governance audit logs
  const historicalEventsList = useMemo(() => {
    const map = new Map();

    // 1. Events from active/eligible events state
    (events || []).forEach((e) => {
      if (e.eventId) {
        map.set(Number(e.eventId), {
          eventId: Number(e.eventId),
          title: e.title || `Event ${e.eventId}`,
        });
      }
    });

    // 2. Events that have reconciliation records (Audit Trail)
    (reconciliations || []).forEach((r) => {
      if (r.eventId) {
        map.set(Number(r.eventId), {
          eventId: Number(r.eventId),
          title: r.eventTitle || `Event ${r.eventId}`,
        });
      }
    });

    // 3. Events from revocation records
    (revocations || []).forEach((rv) => {
      if (rv.eventId) {
        map.set(Number(rv.eventId), {
          eventId: Number(rv.eventId),
          title: rv.eventTitle || `Event ${rv.eventId}`,
        });
      }
    });

    // 3.5. Events from reissue records
    (reissues || []).forEach((ri) => {
      if (ri.eventId) {
        map.set(Number(ri.eventId), {
          eventId: Number(ri.eventId),
          title: ri.eventTitle || `Event ${ri.eventId}`,
        });
      }
    });

    // 4. Known events on platform for comprehensive filtering
    const platformEvents = [
      { eventId: 1, title: "Anh Trai Say Hi - Mùa 1 Grand Finale (Cancelled)" },
      { eventId: 16, title: "AMEE - E11EVEN Tour 2027 (Postponed)" },
      { eventId: 20, title: "Acoustic Night" },
      { eventId: 21, title: 'Hà Anh Tuấn - "Chân Trời Rực Rỡ" Live Concert' },
      { eventId: 17, title: "Hoàng Thùy Linh - See Tình Tour" },
      { eventId: 18, title: "Wren Evans - Call Me Tour" },
    ];
    platformEvents.forEach((pe) => {
      if (!map.has(pe.eventId)) {
        map.set(pe.eventId, pe);
      }
    });

    return Array.from(map.values()).sort((a, b) => a.eventId - b.eventId);
  }, [events, reconciliations, revocations, reissues]);

  return (
    <AdminShell title="Ticket Governance & Dynamic QR Revocation Auditing">
      <div className="ticket-gov-container">
        {/* KPI Summary Cards */}
        <div className="gov-stats-grid">
          <div className="gov-stat-card stat-danger">
            <div className="stat-info">
              <span className="stat-label">Total Revoked Tickets</span>
              <strong className="stat-val">
                {loadingStats ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : stats.totalRevokedTickets.toLocaleString()}
              </strong>
              <span className="stat-sub">System-wide invalidations</span>
            </div>
          </div>

          <div className="gov-stat-card stat-warning">
            <div className="stat-info">
              <span className="stat-label">Bulk Event Disruptions</span>
              <strong className="stat-val">
                {loadingStats ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : stats.totalBulkEvents}
              </strong>
              <span className="stat-sub">Reconciliation audit records</span>
            </div>
          </div>

          <div className="gov-stat-card stat-success">
            <div className="stat-info">
              <span className="stat-label">Reissued Tickets</span>
              <strong className="stat-val">
                {loadingStats ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : stats.totalReissuedTickets}
              </strong>
              <span className="stat-sub">1-to-1 traceability: Old to New</span>
            </div>
          </div>

          <div className="gov-stat-card stat-info-theme">
            <div className="stat-info">
              <span className="stat-label">Notifications</span>
              <strong className="stat-val">Enabled</strong>
              <span className="stat-sub">Auto-notifies ticket owners</span>
            </div>
          </div>
        </div>

        {/* Tab Navigation */}
        <div className="gov-tabs-bar">
          <button
            type="button"
            className={`gov-tab-btn ${activeTab === "BULK_REVOKE" ? "active" : ""}`}
            onClick={() => setActiveTab("BULK_REVOKE")}
          >
            Bulk Revocation
          </button>
          <button
            type="button"
            className={`gov-tab-btn ${activeTab === "REVOCATION_HISTORY" ? "active" : ""}`}
            onClick={() => {
              setActiveTab("REVOCATION_HISTORY");
              fetchRevocations();
            }}
          >
            Revocation History
          </button>
          <button
            type="button"
            className={`gov-tab-btn ${activeTab === "REISSUE_HISTORY" ? "active" : ""}`}
            onClick={() => {
              setActiveTab("REISSUE_HISTORY");
              fetchReissues();
            }}
          >
            Reissue History
          </button>
        </div>

        {/* TAB 1: BULK REVOKE */}
        {activeTab === "BULK_REVOKE" && (
          <div className="gov-tab-content">
            <div className="gov-bulk-layout">
              {/* Form Bulk Revoke */}
              <div className="gov-card gov-bulk-form-card">
                <div className="gov-card-header">
                  <h3>Bulk Revoke Tickets (Event Disruption)</h3>
                  <p className="gov-card-desc">
                    Revoke and invalidate all active tickets for an event due to cancellation or postponement.
                    Executes an atomic database transaction and immediately invalidates ticket identifiers in the gate validation cache.
                  </p>
                </div>

                {bulkSuccessResult && (
                  <div className="gov-alert gov-alert-success">
                    <div className="alert-header">
                      <strong>Bulk Revocation Succeeded!</strong>
                    </div>
                    <p>{bulkSuccessResult.message}</p>
                    <div className="alert-details">
                      <span>Record ID: <strong>{bulkSuccessResult.reconciliationId}</strong></span>
                      <span>Total Revoked: <strong>{bulkSuccessResult.totalRevokedTickets}</strong></span>
                      <span>Gate Cache: <strong>Invalidated</strong></span>
                      <span>Notification: <strong>Sent to Users</strong></span>
                    </div>
                    <button
                      type="button"
                      className="btn-dismiss"
                      onClick={() => setBulkSuccessResult(null)}
                    >
                      Dismiss Notice
                    </button>
                  </div>
                )}

                <div className="form-group">
                  <label className="form-label">
                    Select Event for Bulk Revocation <span className="text-danger">*</span>
                  </label>
                  <select
                    className="form-control"
                    value={selectedEventId}
                    onChange={(e) => setSelectedEventId(e.target.value)}
                    disabled={loadingEvents || submittingBulk || eligibleEvents.length === 0}
                  >
                    {eligibleEvents.length === 0 ? (
                      <option value="">-- No events with active tickets available for revocation --</option>
                    ) : (
                      eligibleEvents.map((ev) => (
                        <option key={ev.eventId} value={ev.eventId}>
                          {ev.title} [{ev.status.toUpperCase()}] ({Number(ev.activeTicketsCount).toLocaleString()} active tickets)
                        </option>
                      ))
                    )}
                  </select>
                  {eligibleEvents.length === 0 && !loadingEvents && (
                    <div className="gov-empty-events-warning">
                      <span>Currently there are no cancelled or rescheduled events with active tickets. All tickets have already been revoked or reconciled.</span>
                    </div>
                  )}
                  {selectedEventObj && eligibleEvents.length > 0 && (
                    <>
                      <div className="event-meta-pill">
                        <span>Venue: <strong>{selectedEventObj.location}</strong></span>
                        <span>Active Tickets: <strong>{Number(selectedEventObj.activeTicketsCount).toLocaleString()} tickets</strong></span>
                        <span>Date: <strong>{new Date(selectedEventObj.startDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}</strong></span>
                        <span>Status: <strong>{selectedEventObj.status}</strong></span>
                      </div>

                      {/* Prominent warning box for tickets to be revoked */}
                      <div className="gov-warning-box-prominent" style={{
                        marginTop: "14px",
                        padding: "18px",
                        backgroundColor: "#fef2f2",
                        border: "1.5px solid #fecaca",
                        borderRadius: "10px"
                      }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "6px" }}>
                          <strong style={{ fontSize: "15px", color: "#991b1b" }}>
                            WARNING: EVENT TICKETS PENDING BULK REVOCATION ({selectedEventObj.status.toUpperCase()})
                          </strong>
                        </div>
                        <p style={{ margin: "6px 0 12px 0", fontSize: "13.5px", color: "#7f1d1d", lineHeight: 1.5 }}>
                          Event <strong>"{selectedEventObj.title}"</strong> is undergoing event disruption / reschedule processing. Executing bulk revocation will immediately invalidate all active tickets, flush gate validation cache (Redis), and trigger refund / reconciliation procedures.
                        </p>
                        <div style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "8px",
                          padding: "8px 16px",
                          backgroundColor: "#ffffff",
                          border: "1.5px solid #f87171",
                          borderRadius: "8px",
                          color: "#dc2626",
                          fontWeight: "800",
                          fontSize: "14px"
                        }}>
                          <span>Total Tickets to be Revoked: <strong>{Number(selectedEventObj.activeTicketsCount).toLocaleString()} tickets</strong></span>
                        </div>
                      </div>
                    </>
                  )}
                </div>

                <div className="form-group">
                  <label className="form-label">
                    Revocation Justification Reason <span className="text-danger">*</span>
                  </label>
                  <textarea
                    className="form-control"
                    rows={3}
                    placeholder='Mandatory revocation reason (e.g., "Event cancelled due to storm weather per Official Notice #12", "Postponed to next month per approved change request CR-12")...'
                    value={reasonNote}
                    onChange={(e) => setReasonNote(e.target.value)}
                    disabled={submittingBulk}
                  />
                  {!reasonNote.trim() && (
                    <small style={{ color: "#dc2626", display: "block", marginTop: "6px", fontWeight: "600" }}>
                      * Revocation reason (Audit Justification) is required before confirming.
                    </small>
                  )}
                </div>

                <div className="form-group checkboxes-group">
                  <label className="checkbox-item">
                    <input
                      type="checkbox"
                      checked={notifyAttendees}
                      onChange={(e) => setNotifyAttendees(e.target.checked)}
                      disabled={submittingBulk}
                    />
                    <span>
                      <strong>Trigger automated in-app notification to all ticket holders</strong>
                      <small className="help-text">Automatically dispatches notifications to each ticket owner's account.</small>
                    </span>
                  </label>
                </div>

                <div className="form-actions">
                  <button
                    type="button"
                    className="btn btn-danger-action"
                    disabled={submittingBulk || !selectedEventId || !reasonNote.trim() || eligibleEvents.length === 0}
                    onClick={() => {
                      if (!selectedEventId || eligibleEvents.length === 0) {
                        setNoticeDialog({
                          type: "WARNING",
                          title: "Event Required",
                          message: "There are no events with active tickets available to revoke.",
                        });
                        return;
                      }
                      if (!reasonNote.trim()) {
                        setNoticeDialog({
                          type: "WARNING",
                          title: "Revocation Reason Required",
                          message: "Revocation reason is required (e.g. 'Show postponed due to weather') before confirmation.",
                        });
                        return;
                      }
                      setShowConfirmModal(true);
                    }}
                  >
                    Confirm & Execute Bulk Revocation
                  </button>
                </div>
              </div>

              {/* Reconciliation Records History Panel */}
              <div className="gov-card gov-reconcile-card">
                <div className="gov-card-header">
                  <h3>Reconciliation Records (Audit Trail)</h3>
                  <p className="gov-card-desc">
                    Historical event-level bulk revocation logs recorded for financial reconciliation and compliance auditing.
                  </p>
                </div>

                {loadingReconciliations ? (
                  <div className="loading-spinner-box"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
                ) : reconciliations.length === 0 ? (
                  <div className="empty-state">No bulk revocation reconciliation records found.</div>
                ) : (
                  <div className="reconcile-list">
                    {reconciliations.map((r) => (
                      <div key={r.reconciliationId} className="reconcile-item">
                        <div className="reconcile-top">
                          <span className="reconcile-id">Record #{r.reconciliationId}</span>
                          <span className={`badge-reason ${r.reasonType}`}>
                            {r.reasonType === "EVENT_CANCELLED" ? "CANCELLED" : "POSTPONED"}
                          </span>
                          <span className="badge-refund-status">{r.refundStatus}</span>
                        </div>
                        <h4 className="reconcile-title">{r.eventTitle}</h4>
                        <div className="reconcile-meta">
                          <span>Admin: <strong>{r.adminName}</strong></span>
                          <span>Revoked Tickets: <strong>{r.totalTicketsRevoked} tickets</strong></span>
                          <span>Date: <strong>{new Date(r.createdAt).toLocaleString("en-US")}</strong></span>
                        </div>
                        <p className="reconcile-note">"{r.notes}"</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            {/* Confirmation Modal */}
            {showConfirmModal && (
              <div className="gov-modal-overlay">
                <div className="gov-modal">
                  <div className="gov-modal-header modal-danger">
                    <h3>Confirm Bulk Ticket Revocation</h3>
                    <button
                      type="button"
                      className="close-btn"
                      onClick={() => setShowConfirmModal(false)}
                      disabled={submittingBulk}
                    >
                      &times;
                    </button>
                  </div>
                  <div className="gov-modal-body">
                    <div className="modal-alert-box">
                      <p>
                        You are executing an <strong>ADMINISTRATIVE ATOMIC TRANSACTION</strong> with legal and financial impact:
                      </p>
                      <ul>
                        <li>
                          All active tickets for <strong>{selectedEventObj?.title}</strong> will immediately transition to <code>REVOKED</code> status.
                        </li>
                        <li>
                          <strong>INSTANTLY INVALIDATES</strong> ticket QR identifiers across Gate Validation Cache (Redis) to block entry at all terminals.
                        </li>
                        <li>
                          Generates system reconciliation audit records and dispatches notifications directly to all ticket holders.
                        </li>
                      </ul>
                      <p className="text-danger fw-bold" style={{ margin: 0, fontWeight: 700 }}>
                        This action cannot be undone directly. Are you sure you wish to proceed?
                      </p>
                    </div>
                    <div className="modal-summary-table">
                      <div><span>Event:</span> <strong>{selectedEventObj?.title}</strong></div>
                      <div><span>Event Status:</span> <strong>{selectedEventObj?.status?.toUpperCase()}</strong></div>
                      <div><span>Active Tickets to Revoke:</span> <strong>{selectedEventObj?.activeTicketsCount} tickets</strong></div>
                      <div><span>Audit Justification:</span> <strong>{reasonNote}</strong></div>
                    </div>
                  </div>
                  <div className="gov-modal-footer">
                    <button
                      type="button"
                      className="btn btn-secondary"
                      onClick={() => setShowConfirmModal(false)}
                      disabled={submittingBulk}
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      className="btn btn-danger"
                      onClick={handleBulkRevokeSubmit}
                      disabled={submittingBulk}
                    >
                      {submittingBulk ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "I Confirm & Revoke All Tickets"}
                    </button>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {/* TAB 2: REVOCATION HISTORY */}
        {activeTab === "REVOCATION_HISTORY" && (
          <div className="gov-tab-content">
            <div className="gov-card">
              <div className="gov-card-header">
                <h3>Ticket Revocation Audit Trail</h3>
                <p className="gov-card-desc">
                  Inspect all revoked or invalidated tickets across the platform for auditing, dispute resolution, and compliance.
                </p>
              </div>

              {/* Filters */}
              <div className="gov-filter-bar">
                <div className="filter-item search-box">
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Search by Ticket Code, Event, Admin, Reason..."
                    value={revocationSearch}
                    onChange={(e) => setRevocationSearch(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        setRevocationPage(1);
                        fetchRevocations();
                      }
                    }}
                  />
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => {
                      setRevocationPage(1);
                      fetchRevocations();
                    }}
                  >
                    Search
                  </button>
                </div>

                <div className="filter-item">
                  <select
                    className="form-control"
                    value={revocationReasonFilter}
                    onChange={(e) => {
                      setRevocationReasonFilter(e.target.value);
                      setRevocationPage(1);
                    }}
                  >
                    <option value="ALL">All Reason Types</option>
                    <option value="EVENT_CANCELLED">Event Cancelled (Bulk)</option>
                    <option value="EVENT_POSTPONED">Event Postponed (Bulk)</option>
                    <option value="FRAUD_PREVENTION">Fraud Prevention / Scalper Bot</option>
                    <option value="TICKET_EXCHANGE">Ticket Exchange / Upgrade</option>
                    <option value="INDIVIDUAL_REVOCATION">Individual Revocation (Single)</option>
                  </select>
                </div>

                <div className="filter-item">
                  <select
                    className="form-control"
                    value={revocationEventFilter}
                    onChange={(e) => {
                      setRevocationEventFilter(e.target.value);
                      setRevocationPage(1);
                    }}
                  >
                    <option value="">All Events</option>
                    {historicalEventsList.map((e) => (
                      <option key={e.eventId} value={e.eventId}>
                        {e.title}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Revocations Table */}
              {loadingRevocations ? (
                <div className="loading-spinner-box"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
              ) : revocations.length === 0 ? (
                <div className="empty-state">No revoked tickets match the selected filters.</div>
              ) : (
                <div className="gov-table-wrapper">
                  <table className="gov-table">
                    <thead>
                      <tr>
                        <th>Ticket Code</th>
                        <th>Event</th>
                        <th>Attendee / Seat</th>
                        <th>Revoked By</th>
                        <th>Revoked At</th>
                        <th>Batch Type</th>
                        <th>Reason & Audit Notes</th>
                        <th>Refund Queue</th>
                      </tr>
                    </thead>
                    <tbody>
                      {revocations.map((item) => (
                        <tr key={item.revocationId}>
                          <td>
                            <code className="ticket-code-tag">{item.ticketCode}</code>
                          </td>
                          <td className="event-name-cell">{item.eventTitle}</td>
                          <td>
                            <div><strong>{item.customerName || "N/A"}</strong></div>
                            <small className="text-muted">{item.seatOrZone || "General Admission"}</small>
                          </td>
                          <td>
                            <span className="badge-admin">{item.revokedByName}</span>
                          </td>
                          <td className="time-cell">
                            {new Date(item.revokedAt).toLocaleString("en-US")}
                          </td>
                          <td>
                            {item.isBulkRevocation ? (
                              <span className="badge-pill pill-bulk">Bulk Batch</span>
                            ) : (
                              <span className="badge-pill pill-single">Individual</span>
                            )}
                          </td>
                          <td className="reason-cell">
                            <span className={`reason-type-tag ${item.reasonType}`}>
                              {item.reasonType}
                            </span>
                            <div className="reason-note-text">{item.reasonNote}</div>
                          </td>
                          <td>
                            {item.refundQueued ? (
                              <span className="badge-status-ok">Enqueued</span>
                            ) : (
                              <span className="badge-status-neutral">No</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Pagination */}
              <div className="gov-pagination-bar">
                <span>
                  Showing page <strong>{revocationPage}</strong> of <strong>{revocationTotalPages}</strong> (Total <strong>{revocationTotal}</strong> tickets)
                </span>
                <div className="pagination-btns">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline"
                    disabled={revocationPage <= 1}
                    onClick={() => setRevocationPage((p) => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm btn-outline"
                    disabled={revocationPage >= revocationTotalPages}
                    onClick={() => setRevocationPage((p) => Math.min(revocationTotalPages, p + 1))}
                  >
                    Next
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: REISSUE HISTORY */}
        {activeTab === "REISSUE_HISTORY" && (
          <div className="gov-tab-content">
            <div className="gov-card">
              <div className="gov-card-header">
                <h3>Ticket Reissue Traceability (1-to-1 Audit)</h3>
                <p className="gov-card-desc">
                  1-to-1 reconciliation trail linking Old Revoked Tickets to New Replacement Tickets with dynamic QR tokens.
                  Ensures complete accountability against unauthorized ticket generation.
                </p>
              </div>

              {/* Filters */}
              <div className="gov-filter-bar">
                <div className="filter-item search-box">
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Search by Old/New Code, Attendee, Email, Reason..."
                    value={reissueSearch}
                    onChange={(e) => setReissueSearch(e.target.value)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") {
                        setReissuePage(1);
                        fetchReissues();
                      }
                    }}
                  />
                  <button
                    type="button"
                    className="btn btn-primary"
                    onClick={() => {
                      setReissuePage(1);
                      fetchReissues();
                    }}
                  >
                    Search
                  </button>
                </div>

                <div className="filter-item">
                  <select
                    className="form-control"
                    value={reissueEventFilter}
                    onChange={(e) => {
                      setReissueEventFilter(e.target.value);
                      setReissuePage(1);
                    }}
                  >
                    <option value="">All Events</option>
                    {historicalEventsList.map((e) => (
                      <option key={e.eventId} value={e.eventId}>
                        {e.title}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {/* Reissue Table */}
              {loadingReissues ? (
                <div className="loading-spinner-box"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
              ) : reissues.length === 0 ? (
                <div className="empty-state">No ticket reissue records match the selected filters.</div>
              ) : (
                <div className="gov-table-wrapper">
                  <table className="gov-table">
                    <thead>
                      <tr>
                        <th>Old Ticket (Revoked)</th>
                        <th>Replaced By</th>
                        <th>New Ticket (Active)</th>
                        <th>Event</th>
                        <th>Beneficiary Attendee</th>
                        <th>Authorized By</th>
                        <th>Timestamp</th>
                        <th>Reissue Reason</th>
                        <th>QR Status</th>
                      </tr>
                    </thead>
                    <tbody>
                      {reissues.map((item) => (
                        <tr key={item.reissueId}>
                          <td>
                            <div className="ticket-trace-box old-ticket">
                              <span className="trace-label">OLD TICKET</span>
                              <code className="trace-code revoked">{item.oldTicketCode}</code>
                              <span className="badge-revoked-tag">Revoked</span>
                            </div>
                          </td>
                          <td className="arrow-cell">➔</td>
                          <td>
                            <div className="ticket-trace-box new-ticket">
                              <span className="trace-label">NEW TICKET</span>
                              <code className="trace-code active">{item.newTicketCode}</code>
                              <span className="badge-active-tag">Dynamic QR</span>
                            </div>
                          </td>
                          <td className="event-name-cell">{item.eventTitle}</td>
                          <td>
                            <div><strong>{item.customerName}</strong></div>
                            <small className="text-muted">{item.customerEmail}</small>
                            <div className="seat-subtag">{item.seatOrZone}</div>
                          </td>
                          <td>
                            <span className="badge-admin">{item.reissuedByName}</span>
                          </td>
                          <td className="time-cell">
                            {new Date(item.reissuedAt).toLocaleString("en-US")}
                          </td>
                          <td className="reason-cell">
                            <span className="reissue-reason-text">{item.reason}</span>
                          </td>
                          <td>
                            <span className="badge-qr-active">Active</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {/* Pagination */}
              <div className="gov-pagination-bar">
                <span>
                  Showing page <strong>{reissuePage}</strong> of <strong>{reissueTotalPages}</strong> (Total <strong>{reissueTotal}</strong> reissued tickets)
                </span>
                <div className="pagination-btns">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline"
                    disabled={reissuePage <= 1}
                    onClick={() => setReissuePage((p) => Math.max(1, p - 1))}
                  >
                    Previous
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm btn-outline"
                    disabled={reissuePage >= reissueTotalPages}
                    onClick={() => setReissuePage((p) => Math.min(reissueTotalPages, p + 1))}
                  >
                    Next
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Pop-up Dialog: Notice / Alert */}
        {noticeDialog && (
          <div className="gov-modal-overlay">
            <div className="gov-dialog-modal">
              <div className={`gov-dialog-header ${noticeDialog.type?.toLowerCase()}`}>
                <h3>{noticeDialog.title}</h3>
                <button
                  type="button"
                  className="close-btn"
                  onClick={() => setNoticeDialog(null)}
                >
                  &times;
                </button>
              </div>
              <div className="gov-dialog-body">
                <p>{noticeDialog.message}</p>
              </div>
              <div className="gov-dialog-footer">
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => setNoticeDialog(null)}
                >
                  Acknowledge
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Pop-up Dialog: Bulk Revoke Success Audit Modal */}
        {successResultModal && (
          <div className="gov-modal-overlay">
            <div className="gov-modal success-summary-modal">
              <div className="gov-modal-header modal-success">
                <h3>Bulk Revocation Completed Successfully!</h3>
                <button
                  type="button"
                  className="close-btn"
                  onClick={() => setSuccessResultModal(null)}
                >
                  &times;
                </button>
              </div>
              <div className="gov-modal-body">
                {(() => {
                  const evObj = events.find((e) => e.eventId === successResultModal.eventId);
                  const rawTitle = successResultModal.eventTitle || evObj?.title || `Event #${successResultModal.eventId}`;
                  const formattedTitle = rawTitle.startsWith("Event #") && evObj?.title
                    ? `Event #${successResultModal.eventId}: ${evObj.title}`
                    : (rawTitle.includes(":") || rawTitle.startsWith("Event #") ? rawTitle : `Event #${successResultModal.eventId}: ${rawTitle}`);

                  return (
                    <div className="success-banner">
                      <span className="success-badge">ATOMIC TRANSACTION VERIFIED</span>
                      <h4>{formattedTitle}</h4>
                      <p>
                        Successfully revoked {Number(successResultModal.totalRevokedTickets).toLocaleString()} tickets for event &lsquo;{formattedTitle}&rsquo;. Reconciliation record #{successResultModal.reconciliationId} created.
                      </p>
                    </div>
                  );
                })()}
                <div className="modal-summary-table">
                  <div>
                    <span>Reconciliation Record ID:</span>
                    <strong>#{successResultModal.reconciliationId}</strong>
                  </div>
                  <div>
                    <span>Total Tickets Revoked:</span>
                    <strong className="text-danger" style={{ fontSize: "1.1rem" }}>
                      {Number(successResultModal.totalRevokedTickets).toLocaleString()} tickets
                    </strong>
                  </div>
                  <div>
                    <span>Authorized By:</span>
                    <strong>{successResultModal.adminName || "Admin System"}</strong>
                  </div>
                  <div>
                    <span>Execution Timestamp:</span>
                    <strong>{new Date(successResultModal.revokedAt || Date.now()).toLocaleString("en-US")}</strong>
                  </div>
                  <div>
                    <span>Gate Scanner Cache (Redis):</span>
                    <strong className="text-success">Flushed Redis & gate memory</strong>
                  </div>
                  <div>
                    <span>Attendee Notifications:</span>
                    <strong className={successResultModal.notificationSent ? "text-success" : "text-muted"}>
                      {successResultModal.notificationSent ? "In-app notification sent to all ticket owners" : "Skipped"}
                    </strong>
                  </div>
                </div>
              </div>
              <div className="gov-modal-footer" style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={() => {
                    setSuccessResultModal(null);
                    setActiveTab("REVOCATION_HISTORY");
                  }}
                >
                  View Revocation History
                </button>
                <button
                  type="button"
                  className="btn btn-success"
                  onClick={() => setSuccessResultModal(null)}
                >
                  Confirm & Close
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminShell>
  );
}
