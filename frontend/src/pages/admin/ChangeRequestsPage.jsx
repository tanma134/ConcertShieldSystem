import {useCallback,useEffect,useState} from 'react';
import AdminShell from './AdminShell';
import {governanceApi as api} from '../../api/governanceApi';
import {validateChangeReview} from '../../utils/governanceRules';
import {useToast} from '../../components/ToastProvider';
import '../../components/GovernancePanel.css';

// Report 3, 3.10.10 Approve Event Change Request: list of requests + detail panel with Approve / Reject.
const date = value => value ? new Date(value).toLocaleString() : 'To be announced';
const money = amount => new Intl.NumberFormat('en-US').format(amount || 0) + ' VND';
const code = id => `CR-${id}`;
const errorText = err => {
  const status = err.response?.status;
  if (status === 404) return 'This request no longer exists.';
  if (status === 409) return err.response?.data?.message?.includes('can no longer') || err.response?.data?.message?.includes('has changed')
    ? 'This change can no longer be applied. Please reject the request and explain the reason.'
    : 'This request has already been handled.';
  if (status === 403) return 'You do not have permission to perform this action.';
  return err.response?.data?.message || err.message || 'Request failed. Please retry.';
};

function Row({label,current,requested}) {
  const changed = requested !== undefined && requested !== null && requested !== current;
  return <tr><td>{label}</td><td>{changed?<s>{current}</s>:current}</td><td>{changed?<strong style={{color:'#047857'}}>{requested}</strong>:'No change'}</td></tr>;
}

export default function ChangeRequestsPage() {
  const toast = useToast();
  const [items,setItems]=useState([]),[loading,setLoading]=useState(true),[error,setError]=useState(''),[notice,setNotice]=useState('');
  const [filter,setFilter]=useState(''),[selected,setSelected]=useState(null),[impact,setImpact]=useState(null),[note,setNote]=useState(''),[busy,setBusy]=useState(false);

  const load=useCallback(async()=>{
    setLoading(true);setError('');
    try{const r=await api.allChanges(filter);setItems(r.data.data);return r.data.data;}
    catch(err){setError(errorText(err));return [];}finally{setLoading(false);}
  },[filter]);
  useEffect(()=>{load();},[load]);

  // Deep link from the event page: /admin/change-requests?request=<id>
  useEffect(()=>{
    const wanted=Number(new URLSearchParams(window.location.search).get('request'));
    if(wanted&&items.length&&!selected){const hit=items.find(i=>i.id===wanted);if(hit)open(hit);}
    // eslint-disable-next-line react-hooks/exhaustive-deps
  },[items]);

  const open=async item=>{
    setSelected(item);setNote('');setNotice('');setError('');setImpact(null);
    try{
      const r=await api.affected(item.eventId,item.id,{tab:'orders',page:1,pageSize:1});
      setImpact(r.data.data.summary);
    }catch{setImpact(undefined);}
  };

  const [confirmAction, setConfirmAction] = useState(null);

  const decide = (decision) => {
    const problem = validateChangeReview(decision, note);
    if (problem) { setError(problem); return; }
    const title = decision === 'Approved' ? 'Approve Change Request' : 'Reject Change Request';
    const message = decision === 'Approved' 
      ? 'Are you sure you want to approve this change? Customers will be notified immediately.' 
      : 'Are you sure you want to reject this change request?';
    setConfirmAction({ decision, title, message });
  };

  const executeDecision = async () => {
    if (!confirmAction) return;
    const { decision } = confirmAction;
    setConfirmAction(null);
    setBusy(true); setError(''); setNotice('');
    try {
      await api.reviewChange(selected.eventId, selected.id, { decision, notes: note.trim() });
      setNotice(`Change request ${code(selected.id)} ${decision === 'Approved' ? 'approved' : 'rejected'}.`);
      const fresh = await load(); setSelected(fresh.find(i => i.id === selected.id) || null);
    } catch (err) {
      setError(errorText(err));
      const fresh = await load(); setSelected(fresh.find(i => i.id === selected.id) || selected);
    } finally { setBusy(false); }
  };

  const pending=selected?.status==='Pending';
  return <AdminShell title="Change requests"><section className="governance-panel">
    <h1>Change requests</h1><p>Review requests from organizers.</p>
    {error&&<div role="alert" className="ow-error">{error}</div>}
    {notice&&<div role="status">{notice}</div>}
    <label>Status <select value={filter} onChange={e=>{setFilter(e.target.value);setSelected(null);}}><option value="">All</option><option>Pending</option><option>Approved</option><option>Rejected</option></select></label>{' '}
    <button type="button" className="tb-btn tb-btn-outline" disabled={loading} onClick={load}>Refresh</button>
    {loading?<p>Loading change requests...</p>:items.length===0?<p>No change requests.</p>:
    <div className="governance-table"><table><thead><tr><th>Request</th><th>Event</th><th>Type</th><th>Organizer</th><th>Status</th><th>Submitted</th><th></th></tr></thead>
      <tbody>{items.map(i=><tr key={i.id}><td>{code(i.id)}</td><td>{i.eventTitle} (#{i.eventId})</td><td><span style={{display:'inline-block',padding:'2px 8px',borderRadius:'6px',fontSize:'11px',fontWeight:700,background:i.type==='Cancel'?'#fee2e2':i.type==='Postpone'?'#fef3c7':'#e0e7ff',color:i.type==='Cancel'?'#b91c1c':i.type==='Postpone'?'#b45309':'#3730a3'}}>{i.type.toUpperCase()}</span></td><td>Organizer #{i.organizerId}</td><td>{i.status.toUpperCase()}</td><td>{date(i.submittedAt)}</td>
        <td><button type="button" className="tb-btn tb-btn-outline" onClick={()=>open(i)}>{i.status==='Pending'?'Review':'View'}</button></td></tr>)}</tbody></table></div>}
    {selected && (
      <div
        className="admin-confirm-modal-overlay"
        onClick={() => setSelected(null)}
        style={{
          position: "fixed",
          inset: 0,
          backgroundColor: "rgba(15, 23, 42, 0.65)",
          backdropFilter: "blur(4px)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          zIndex: 9999,
          padding: "20px",
        }}
      >
        <div
          className="admin-confirm-modal"
          onClick={(e) => e.stopPropagation()}
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "16px",
            maxWidth: "780px",
            width: "100%",
            maxHeight: "90vh",
            overflowY: "auto",
            boxShadow: "0 25px 50px -12px rgba(0, 0, 0, 0.25)",
            padding: "24px 28px",
            position: "relative",
          }}
        >
          {/* Header */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              borderBottom: "1px solid #f1f5f9",
              paddingBottom: "14px",
              marginBottom: "16px",
            }}
          >
            <div>
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <span
                  style={{
                    fontSize: "20px",
                    fontWeight: 800,
                    color: "#0f172a",
                  }}
                >
                  {code(selected.id)} · {selected.type}
                </span>
                <span
                  style={{
                    fontSize: "12px",
                    fontWeight: 700,
                    padding: "3px 10px",
                    borderRadius: "6px",
                    textTransform: "uppercase",
                    backgroundColor:
                      selected.status === "Approved"
                        ? "#dcfce7"
                        : selected.status === "Rejected"
                        ? "#fee2e2"
                        : "#fef3c7",
                    color:
                      selected.status === "Approved"
                        ? "#15803d"
                        : selected.status === "Rejected"
                        ? "#b91c1c"
                        : "#b45309",
                  }}
                >
                  {selected.status}
                </span>
              </div>
              <p
                style={{
                  margin: "4px 0 0 0",
                  fontSize: "13px",
                  color: "#64748b",
                }}
              >
                Event: <strong>{selected.eventTitle}</strong> (#{selected.eventId}) · Organizer #{selected.organizerId} · Timezone: {selected.timezone}
              </p>
            </div>
            <button
              type="button"
              onClick={() => setSelected(null)}
              style={{
                background: "#f1f5f9",
                border: "none",
                fontSize: "16px",
                color: "#64748b",
                cursor: "pointer",
                width: "34px",
                height: "34px",
                borderRadius: "50%",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontWeight: 700,
              }}
              title="Close modal"
            >
              Close
            </button>
          </div>

          {/* Comparison Table */}
          <div className="governance-table" style={{ marginBottom: "16px" }}>
            <table style={{ width: "100%" }}>
              <thead>
                <tr>
                  <th style={{ width: "22%" }}>Field</th>
                  <th style={{ width: "39%" }}>Current</th>
                  <th style={{ width: "39%" }}>Requested</th>
                </tr>
              </thead>
              <tbody>
                <Row
                  label="Type"
                  current="Scheduled"
                  requested={
                    selected.type === "Postpone"
                      ? "Postponed (date to be announced)"
                      : selected.type === "Cancel"
                      ? "Cancelled (Permanent Event Cancellation)"
                      : "Rescheduled"
                  }
                />
                <Row
                  label="Start"
                  current={date(selected.oldStartsAt)}
                  requested={
                    selected.type === "Postpone" || selected.type === "Cancel"
                      ? (selected.type === "Cancel" ? "Cancelled" : "To be announced")
                      : date(selected.newStartsAt)
                  }
                />
                <Row
                  label="End"
                  current={date(selected.oldEndsAt)}
                  requested={
                    selected.type === "Postpone" || selected.type === "Cancel"
                      ? (selected.type === "Cancel" ? "Cancelled" : "To be announced")
                      : date(selected.newEndsAt)
                  }
                />
              </tbody>
            </table>
          </div>

          {/* Official Reason */}
          <div
            style={{
              background: "#f8fafc",
              padding: "12px 16px",
              borderRadius: "10px",
              border: "1px solid #e2e8f0",
              marginBottom: "16px",
            }}
          >
            <div
              style={{
                fontSize: "11px",
                fontWeight: 700,
                color: "#64748b",
                textTransform: "uppercase",
                letterSpacing: "0.5px",
                marginBottom: "4px",
              }}
            >
              Official Reason from Organizer
            </div>
            <p
              style={{
                margin: 0,
                fontSize: "14px",
                color: "#1e293b",
                lineHeight: 1.5,
              }}
            >
              {selected.reason}
            </p>
          </div>

          {/* Supporting Legal Document */}
          {selected.documentUrl && (
            <div
              style={{
                marginBottom: "16px",
                padding: "12px 16px",
                background: "#eff6ff",
                borderRadius: "10px",
                border: "1px solid #bfdbfe",
                display: "flex",
                alignItems: "center",
                justifyContent: "space-between",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                <div>
                  <div
                    style={{
                      fontSize: "13px",
                      fontWeight: 700,
                      color: "#1e40af",
                    }}
                  >
                    Legal Evidence / Venue Addendum Document
                  </div>
                  <div style={{ fontSize: "12px", color: "#3b82f6" }}>
                    {selected.documentUrl.split("/").pop()}
                  </div>
                </div>
              </div>
              <button
                type="button"
                onClick={async (e) => {
                  e.preventDefault();
                  try {
                    const res = await api.downloadChangeDocument(selected.eventId, selected.id);
                    const blob = new Blob([res.data], { type: 'application/pdf' });
                    const url = URL.createObjectURL(blob);
                    window.open(url, '_blank');
                  } catch (err) {
                    toast.error('Could not open document: ' + (err.response?.data?.message || err.message));
                  }
                }}
                style={{
                  padding: "8px 14px",
                  background: "#2563eb",
                  color: "#ffffff",
                  border: "none",
                  borderRadius: "8px",
                  fontSize: "12px",
                  fontWeight: 600,
                  cursor: "pointer",
                  display: "inline-flex",
                  alignItems: "center",
                  gap: "4px",
                  boxShadow: "0 2px 4px rgba(37, 99, 235, 0.2)",
                }}
              >
                Open Document ↗
              </button>
            </div>
          )}

          {/* Impact Summary Cards */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(3, 1fr)",
              gap: "12px",
              marginBottom: "16px",
            }}
          >
            <div
              style={{
                background: "#f8fafc",
                padding: "12px",
                borderRadius: "10px",
                textAlign: "center",
                border: "1px solid #e2e8f0",
              }}
            >
              <div
                style={{ fontSize: "11px", color: "#64748b", fontWeight: 600 }}
              >
                AFFECTED ORDERS
              </div>
              <div
                style={{
                  fontSize: "18px",
                  fontWeight: 800,
                  color: "#0f172a",
                  marginTop: "2px",
                }}
              >
                {impact === null ? "..." : impact?.orders ?? "N/A"}
              </div>
            </div>
            <div
              style={{
                background: "#f8fafc",
                padding: "12px",
                borderRadius: "10px",
                textAlign: "center",
                border: "1px solid #e2e8f0",
              }}
            >
              <div
                style={{ fontSize: "11px", color: "#64748b", fontWeight: 600 }}
              >
                AFFECTED TICKETS
              </div>
              <div
                style={{
                  fontSize: "18px",
                  fontWeight: 800,
                  color: "#0f172a",
                  marginTop: "2px",
                }}
              >
                {impact === null ? "..." : impact?.tickets ?? "N/A"}
              </div>
            </div>
            <div
              style={{
                background: "#f8fafc",
                padding: "12px",
                borderRadius: "10px",
                textAlign: "center",
                border: "1px solid #e2e8f0",
              }}
            >
              <div
                style={{ fontSize: "11px", color: "#64748b", fontWeight: 600 }}
              >
                TOTAL PAID AMOUNT
              </div>
              <div
                style={{
                  fontSize: "18px",
                  fontWeight: 800,
                  color: "#16a34a",
                  marginTop: "2px",
                }}
              >
                {impact === null ? "..." : money(impact?.totalPaid)}
              </div>
            </div>
          </div>

          {/* Synchronization & Admin notes */}
          <div
            style={{
              fontSize: "12px",
              color: "#64748b",
              marginBottom: "16px",
              display: "flex",
              justifyContent: "space-between",
              padding: "0 2px",
            }}
          >
            <span>
              Synchronization: <strong>{selected.processingStatus}</strong>
            </span>
            {selected.reviewNotes && (
              <span>
                Reviewed by Admin #{selected.reviewedBy}: <em>"{selected.reviewNotes}"</em>
              </span>
            )}
          </div>

          {/* View affected button */}
          {selected.status !== "Rejected" && (
            <div style={{ marginBottom: "16px" }}>
              <a
                className="tb-btn tb-btn-outline"
                style={{
                  width: "100%",
                  textAlign: "center",
                  display: "block",
                  padding: "9px",
                  fontSize: "13px",
                  fontWeight: 600,
                  borderRadius: "8px",
                  boxSizing: "border-box",
                }}
                href={`/admin/events/${selected.eventId}/governance?request=${selected.id}`}
              >
                View affected orders & tickets in detail →
              </a>
            </div>
          )}

          {/* Review actions if Pending */}
          {pending && (
            <div
              style={{
                borderTop: "1px solid #f1f5f9",
                paddingTop: "16px",
                marginTop: "10px",
              }}
            >
              <label
                style={{
                  display: "block",
                  fontSize: "13px",
                  fontWeight: 600,
                  color: "#374151",
                  marginBottom: "6px",
                }}
              >
                Admin Review Note (required when rejecting, at least 10 characters):
              </label>
              <textarea
                maxLength={1000}
                rows={2}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder="Enter approval note or explanation for rejection..."
                style={{
                  width: "100%",
                  padding: "10px 12px",
                  border: "1px solid #d1d5db",
                  borderRadius: "8px",
                  boxSizing: "border-box",
                  fontSize: "13px",
                  marginBottom: "14px",
                }}
              />
              <div
                style={{
                  display: "flex",
                  justifyContent: "flex-end",
                  gap: "10px",
                }}
              >
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  onClick={() => setSelected(null)}
                  disabled={busy}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  style={{ color: "#dc2626", borderColor: "#fca5a5" }}
                  disabled={busy}
                  onClick={() => decide("Rejected")}
                >
                  Reject Request
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary"
                  style={{ background: "#16a34a", borderColor: "#16a34a" }}
                  disabled={busy}
                  onClick={() => decide("Approved")}
                >
                  Approve Change
                </button>
              </div>
            </div>
          )}

          {!pending && (
            <div
              style={{
                display: "flex",
                justifyContent: "flex-end",
                borderTop: "1px solid #f1f5f9",
                paddingTop: "14px",
              }}
            >
              <button
                type="button"
                className="tb-btn tb-btn-outline"
                style={{ minWidth: "90px" }}
                onClick={() => setSelected(null)}
              >
                Close
              </button>
            </div>
          )}
        </div>
      </div>
    )}

    {confirmAction && (
      <div
        className="admin-confirm-modal-overlay"
        onClick={() => setConfirmAction(null)}
        style={{
          position: "fixed",
          inset: 0,
          backgroundColor: "rgba(15, 23, 42, 0.75)",
          backdropFilter: "blur(4px)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          zIndex: 10000,
          padding: "20px",
        }}
      >
        <div
          className="admin-confirm-modal"
          onClick={(e) => e.stopPropagation()}
          style={{
            background: "#ffffff",
            borderRadius: "14px",
            padding: "24px",
            maxWidth: "460px",
            width: "100%",
            boxShadow: "0 20px 40px -10px rgba(0, 0, 0, 0.25)",
            border: "1px solid #e2e8f0",
          }}
        >
          <h3
            style={{
              margin: "0 0 10px 0",
              fontSize: "17px",
              fontWeight: 700,
              color: confirmAction.decision === "Approved" ? "#16a34a" : "#dc2626",
            }}
          >
            {confirmAction.title}
          </h3>
          <p
            style={{
              margin: "0 0 20px 0",
              fontSize: "14px",
              color: "#4b5563",
              lineHeight: 1.5,
            }}
          >
            {confirmAction.message}
          </p>
          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              gap: "10px",
            }}
          >
            <button
              type="button"
              className="tb-btn tb-btn-outline"
              onClick={() => setConfirmAction(null)}
              disabled={busy}
            >
              Cancel
            </button>
            <button
              type="button"
              className="tb-btn tb-btn-primary"
              style={{
                background: confirmAction.decision === "Approved" ? "#16a34a" : "#dc2626",
                borderColor: confirmAction.decision === "Approved" ? "#16a34a" : "#dc2626",
                color: "#ffffff",
              }}
              disabled={busy}
              onClick={executeDecision}
            >
              {confirmAction.decision === "Approved" ? "Yes, Approve Change" : "Yes, Reject Request"}
            </button>
          </div>
        </div>
      </div>
    )}
  </section></AdminShell>;
}
