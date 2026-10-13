import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import reportApi, { saveBlob } from "../../api/reportApi";
import staffApi from "../../api/staffApi";
import { apiErrorMessage } from "../../utils/apiError";
import { formatDateTime } from "../../utils/format";
import "../organizer/OrganizerWizard.css";
import "./OrganizerDashboardPage.css";
import "../../styles/reportPages.css";

// UC_14.2 (export check-in report), UC_14.3 (view staff list) and UC_14.4 (assign / unassign staff).
export default function CheckinManagementPage() {
  const { id } = useParams();
  const [checkin, setCheckin] = useState(null);
  const [staff, setStaff] = useState([]);
  const [candidates, setCandidates] = useState([]);
  const [query, setQuery] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [toast, setToast] = useState(null);
  const [busy, setBusy] = useState(false);
  const [searched, setSearched] = useState(false);
  const [gateByStaff, setGateByStaff] = useState({});
  // Staff được chọn thêm nhiệm vụ duyệt hoàn vé ngay khi gán.
  const [reviewByStaff, setReviewByStaff] = useState({});
  const gates = ["Main Gate", "Gate A", "Gate B", "Gate C"];

  // Shows a short message in the corner and hides it after a few seconds.
  const showToast = (message, type = "ok") => {
    setToast({ message, type });
    window.setTimeout(() => setToast(null), 4000);
  };

  // Loads the check-in figures and the assigned staff together.
  const loadAll = async () => {
    setLoading(true);
    setError("");
    try {
      const [checkinRes, staffRes, candidateRes] = await Promise.all([
        reportApi.getCheckin(id),
        staffApi.list(id),
        staffApi.candidates(id, ""),
      ]);
      setCheckin(checkinRes.data?.data || null);
      setStaff(staffRes.data?.data || []);
      setCandidates(candidateRes.data?.data || []);
      setSearched(true);
    } catch (err) {
      setError(apiErrorMessage(err, "Could not load check-in data."));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
  }, [id]);

  // UC_14.2: downloads the check-in report as a CSV file.
  const exportCsv = async () => {
    setBusy(true);
    try {
      const response = await reportApi.exportCheckin(id);
      saveBlob(response, `checkin-event-${id}.csv`);
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not export the report."), "error");
    } finally {
      setBusy(false);
    }
  };

  // UC_14.4: looks up Staff accounts by name or email.
  const searchCandidates = async () => {
    setBusy(true);
    try {
      const res = await staffApi.candidates(id, query.trim());
      setCandidates(res.data?.data || []);
      setSearched(true);
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not search staff accounts."), "error");
    } finally {
      setBusy(false);
    }
  };

  // UC_14.4: assigns one account, then refreshes both lists.
  const assign = async (candidate) => {
    setBusy(true);
    try {
      await staffApi.assign(id, candidate.userId, gateByStaff[candidate.userId] || "Main Gate", !!reviewByStaff[candidate.userId]);
      showToast(`${candidate.fullName} was assigned to this concert.`);
      await Promise.all([loadAll(), searchCandidates()]);
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not assign this staff member."), "error");
    } finally {
      setBusy(false);
    }
  };

  // Bật/tắt nhiệm vụ duyệt hoàn vé của một staff đã được gán.
  const toggleReturnReview = async (member) => {
    setBusy(true);
    try {
      await staffApi.setReturnReview(id, member.staffUserId, !member.canReviewReturns);
      showToast(member.canReviewReturns
        ? `${member.fullName} can no longer review returns.`
        : `${member.fullName} can now review ticket returns for this concert.`);
      await loadAll();
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not update this staff member."), "error");
    } finally {
      setBusy(false);
    }
  };

  // UC_14.4: removes one staff member after the organizer confirms.
  const unassign = async (member) => {
    if (!window.confirm(`Remove ${member.fullName} from this concert?`)) return;

    setBusy(true);
    try {
      await staffApi.unassign(id, member.staffUserId);
      showToast(`${member.fullName} was removed.`);
      await loadAll();
      if (searched) await searchCandidates();
    } catch (err) {
      showToast(apiErrorMessage(err, "Could not remove this staff member."), "error");
    } finally {
      setBusy(false);
    }
  };

  const summary = checkin?.summary;

  return (
    <OrganizerShell title="Check-in & Staff">

      <div className="tb-container rp-wrap">
        <div className="od-head">
          <div>
            <p className="od-eyebrow">Organizer · Check-in</p>
            <h1>{checkin?.eventTitle || "Check-in & staff"}</h1>
          </div>
          <div style={{ display: "flex", gap: "10px" }}>
            <Link to={`/organizer/events/${id}/revenue`} className="tb-btn tb-btn-outline">
              Revenue
            </Link>
            <Link to="/organizer/dashboard" className="tb-btn tb-btn-outline">
              ← Dashboard
            </Link>
          </div>
        </div>

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && summary && (
          <>
            <div className="rp-section">
              <div className="od-section-head">
                <h2>Check-in report</h2>
                <button type="button" className="tb-btn tb-btn-outline" disabled={busy} onClick={exportCsv}>
                  Export CSV
                </button>
              </div>

              <div className="rp-cards">
                <div className="od-card">
                  <span className="od-card-label">Valid tickets</span>
                  <span className="od-card-value">{summary.totalTickets}</span>
                </div>
                <div className="od-card od-card-highlight">
                  <span className="od-card-label">Checked in</span>
                  <span className="od-card-value">{summary.checkedIn}</span>
                </div>
                <div className="od-card">
                  <span className="od-card-label">Not checked in</span>
                  <span className="od-card-value">{summary.notCheckedIn}</span>
                </div>
                <div className="od-card">
                  <span className="od-card-label">Returned</span>
                  <span className="od-card-value">{summary.returned}</span>
                </div>
              </div>

              <div className="rp-progress" aria-label="Check-in rate">
                <span style={{ width: `${summary.checkInRatePercent}%` }} />
              </div>
              <p className="rp-muted">{summary.checkInRatePercent}% of valid tickets have entered the venue.</p>

              {summary.byTicketType.length > 0 && (
                <div className="od-table-wrap">
                  <table className="ow-table">
                    <thead>
                      <tr>
                        <th>Ticket type</th>
                        <th className="rp-num">Tickets</th>
                        <th className="rp-num">Checked in</th>
                        <th className="rp-num">Rate</th>
                      </tr>
                    </thead>
                    <tbody>
                      {summary.byTicketType.map((type) => (
                        <tr key={type.ticketTypeName}>
                          <td className="ow-td-title">{type.ticketTypeName}</td>
                          <td className="rp-num">{type.total}</td>
                          <td className="rp-num">{type.checkedIn}</td>
                          <td className="rp-num">{type.ratePercent}%</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            <div className="rp-staff-grid">
              <div className="rp-section">
                <h2>Assigned staff ({staff.length})</h2>
                {staff.length === 0 && <div className="tb-empty">No staff assigned to this concert yet.</div>}
                {staff.length > 0 && (
                  <table className="ow-table">
                    <thead>
                      <tr>
                        <th>Staff member</th>
                        <th>Gate</th>
                        <th>Return review</th>
                        <th>Assigned</th>
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      {staff.map((member) => (
                        <tr key={member.staffUserId}>
                          <td>
                            <div className="ow-td-title">{member.fullName}</div>
                            <div className="ow-td-sub">{member.email}</div>
                          </td>
                          <td>{member.gateName || "Main Gate"}</td>
                          <td>
                            <label style={{ display: "flex", gap: 6, alignItems: "center" }}>
                              <input type="checkbox" checked={!!member.canReviewReturns} disabled={busy} onChange={() => toggleReturnReview(member)} />
                              <span className="ow-td-sub">{member.canReviewReturns ? "Can review" : "No"}</span>
                            </label>
                          </td>
                          <td>{formatDateTime(member.assignedAt)}</td>
                          <td>
                            <button type="button" className="ow-link ow-link-danger" disabled={busy} onClick={() => unassign(member)}>
                              Remove
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              <div className="rp-section">
                <h2>Assign staff</h2>
                <div className="rp-toolbar">
                  <label className="ow-field" style={{ flex: 1 }}>
                    <span>Search by name or email</span>
                    <input
                      type="text"
                      value={query}
                      onChange={(event) => setQuery(event.target.value)}
                      onKeyDown={(event) => event.key === "Enter" && searchCandidates()}
                    />
                  </label>
                  <button type="button" className="tb-btn tb-btn-primary" disabled={busy} onClick={searchCandidates}>
                    Search
                  </button>
                </div>

                {searched && candidates.length === 0 && (
                  <div className="tb-empty">No staff accounts found. Staff accounts are created by an Admin.</div>
                )}
                {candidates.length > 0 && (
                  <table className="ow-table">
                    <tbody>
                      {candidates.map((candidate) => (
                        <tr key={candidate.userId}>
                          <td>
                            <div className="ow-td-title">{candidate.fullName}</div>
                            <div className="ow-td-sub">{candidate.email}</div>
                          </td>
                          <td>
                            {candidate.isAssigned ? (
                              <span className="rp-badge rp-badge-active">Assigned</span>
                            ) : (
                              <div style={{ display: "flex", gap: 8, alignItems: "center", justifyContent: "flex-end" }}>
                                <select
                                  value={gateByStaff[candidate.userId] || "Main Gate"}
                                  onChange={(e) => setGateByStaff((prev) => ({ ...prev, [candidate.userId]: e.target.value }))}
                                >
                                  {gates.map((gate) => <option key={gate} value={gate}>{gate}</option>)}
                                </select>
                                <label className="ow-td-sub" style={{ display: "flex", gap: 4, alignItems: "center" }}>
                                  <input
                                    type="checkbox"
                                    checked={!!reviewByStaff[candidate.userId]}
                                    onChange={(e) => setReviewByStaff((prev) => ({ ...prev, [candidate.userId]: e.target.checked }))}
                                  />
                                  Review returns
                                </label>
                                <button type="button" className="tb-btn tb-btn-outline" disabled={busy} onClick={() => assign(candidate)}>
                                  Assign
                                </button>
                              </div>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </>
        )}
      </div>

      {toast && <div className={"rp-toast " + (toast.type === "error" ? "rp-toast-error" : "rp-toast-ok")}>{toast.message}</div>}

      </OrganizerShell>
  );
}
