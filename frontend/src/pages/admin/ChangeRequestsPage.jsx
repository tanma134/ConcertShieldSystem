import {useCallback,useEffect,useState} from 'react';
import AdminShell from './AdminShell';
import {governanceApi as api} from '../../api/governanceApi';
import {validateChangeReview} from '../../utils/governanceRules';
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
  return <tr><td>{label}</td><td>{changed?<s>{current}</s>:current}</td><td>{changed?<strong style={{color:'#34d399'}}>{requested}</strong>:'No change'}</td></tr>;
}

export default function ChangeRequestsPage() {
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

  const decide=async decision=>{
    const problem=validateChangeReview(decision,note);
    if(problem){setError(problem);return;}
    const ask=decision==='Approved'?'Approve this change? Customers will be notified.':'Reject this change request?';
    if(!window.confirm(ask))return;
    setBusy(true);setError('');setNotice('');
    try{
      await api.reviewChange(selected.eventId,selected.id,{decision,notes:note.trim()});
      setNotice(`Change request ${code(selected.id)} ${decision==='Approved'?'approved':'rejected'}.`);
      const fresh=await load();setSelected(fresh.find(i=>i.id===selected.id)||null);
    }catch(err){
      setError(errorText(err));
      const fresh=await load();setSelected(fresh.find(i=>i.id===selected.id)||selected);
    }finally{setBusy(false);}
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
      <tbody>{items.map(i=><tr key={i.id}><td>{code(i.id)}</td><td>{i.eventTitle} (#{i.eventId})</td><td>{i.type}</td><td>Organizer #{i.organizerId}</td><td>{i.status.toUpperCase()}</td><td>{date(i.submittedAt)}</td>
        <td><button type="button" className="tb-btn tb-btn-outline" onClick={()=>open(i)}>{i.status==='Pending'?'Review':'View'}</button></td></tr>)}</tbody></table></div>}
    {selected&&<article className="governance-change" aria-label="Change request detail">
      <h2>{code(selected.id)} · {selected.type} · {selected.status}</h2>
      <p>Event: {selected.eventTitle} (#{selected.eventId}) · Organizer #{selected.organizerId} · Timezone: {selected.timezone}</p>
      <div className="governance-table"><table><thead><tr><th>Field</th><th>Current</th><th>Requested</th></tr></thead><tbody>
        <Row label="Type" current="Scheduled" requested={selected.type==='Postpone'?'Postponed (date to be announced)':'Rescheduled'}/>
        <Row label="Start" current={date(selected.oldStartsAt)} requested={selected.type==='Postpone'?'To be announced':date(selected.newStartsAt)}/>
        <Row label="End" current={date(selected.oldEndsAt)} requested={selected.type==='Postpone'?'To be announced':date(selected.newEndsAt)}/>
      </tbody></table></div>
      <p>Reason: {selected.reason}</p>
      <p>{impact===null?'Calculating affected orders...':impact===undefined?'Affected orders could not be loaded.':`${impact.orders} orders · ${impact.tickets} tickets · Paid ${money(impact.totalPaid)}`}</p>
      <p>Synchronization: {selected.processingStatus}</p>
      {selected.reviewNotes&&<p>Admin #{selected.reviewedBy}: {selected.reviewNotes}</p>}
      {selected.status!=='Rejected'&&<a className="tb-btn tb-btn-outline" href={`/admin/events/${selected.eventId}/governance?request=${selected.id}`}>View affected orders / tickets</a>}
      {pending&&<>
        <label>Admin note (required when rejecting, at least 10 characters)<textarea maxLength={1000} value={note} onChange={e=>setNote(e.target.value)}/></label>
        <button type="button" className="tb-btn tb-btn-primary" disabled={busy} onClick={()=>decide('Approved')}>Approve change</button>{' '}
        <button type="button" className="tb-btn tb-btn-outline" disabled={busy} onClick={()=>decide('Rejected')}>Reject</button>
      </>}
    </article>}
  </section></AdminShell>;
}
