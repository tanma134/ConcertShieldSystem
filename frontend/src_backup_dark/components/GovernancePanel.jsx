import {useCallback,useEffect,useState} from 'react';
import {governanceApi as api} from '../api/governanceApi';
import {validateChange,validateReview,canPublish,fileError,localToUtc} from '../utils/governanceRules';
import './GovernancePanel.css';
const message = err => err.response?.data?.message || err.message || 'Request failed. Please retry.';
const money = amount => new Intl.NumberFormat('en-US').format(amount || 0) + ' VND';
const date = value => value ? new Date(value).toLocaleString() : 'To be announced';

export default function GovernancePanel({event,admin=false,onUpdated,onCompliance}) {
  const id=event.eventId;
  const [record,setRecord]=useState(null),[changes,setChanges]=useState([]);
  const [error,setError]=useState(''),[notice,setNotice]=useState(''),[busy,setBusy]=useState(false),[loading,setLoading]=useState(true);
  const [type,setType]=useState(''),[file,setFile]=useState(null),[fileKey,setFileKey]=useState(0);
  const [decision,setDecision]=useState('Approved'),[notes,setNotes]=useState('');
  const [form,setForm]=useState({type:'Reschedule',reason:'',newStartsAt:'',newEndsAt:''}),[preview,setPreview]=useState(null);
  const [request,setRequest]=useState(null),[affected,setAffected]=useState(null),[reading,setReading]=useState(false);
  const [filters,setFilters]=useState({tab:'orders',status:'',orderCode:'',userId:'',page:1,pageSize:10});
  const [search,setSearch]=useState({status:'',orderCode:'',userId:''});
  const load=useCallback(async()=>{
    setLoading(true);
    try {
      const [c,r]=await Promise.all([api.compliance(id),api.changes(id)]);
      setRecord(c.data.data);setChanges(r.data.data);setType(t=>t||c.data.data.requiredTypes[0]||'');
      onCompliance?.(canPublish(c.data.data));
    } catch(err){setError(message(err));}finally{setLoading(false);}
  },[id,onCompliance]);
  useEffect(()=>{load();},[load]);
  // Opened from the Change requests page ("View affected orders / tickets"): ?request=<id> selects that request.
  const [autoOpened,setAutoOpened]=useState(false);
  useEffect(()=>{
    if(autoOpened||changes.length===0)return;
    const wanted=Number(new URLSearchParams(window.location.search).get('request'));
    const match=changes.find(c=>c.id===wanted&&c.status!=='Rejected');
    setAutoOpened(true);
    if(match){setRequest(match);setFilters({tab:'orders',status:'',orderCode:'',userId:'',page:1,pageSize:10});setSearch({status:'',orderCode:'',userId:''});}
  },[changes,autoOpened]);
  const run=async action=>{setBusy(true);setError('');setNotice('');try{await action();await load();}catch(err){setError(message(err));}finally{setBusy(false);}};
  const download=doc=>run(async()=>{
    const response=await api.download(id,doc.id),url=URL.createObjectURL(response.data);
    const link=document.createElement('a');link.href=url;link.download=doc.fileName;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  });
  const editable=!admin&&['Draft','Rejected'].includes(event.status);
  const current=record?.documents.filter(d=>d.version===record.version)||[];
  const changeable=!admin&&['Published','Postponed'].includes(event.status)&&!changes.some(c=>c.status==='Pending');
  const buildInput=()=>{
    const input={...form,reason:form.reason.trim(),newStartsAt:form.type==='Postpone'?null:localToUtc(form.newStartsAt,event.timezone),newEndsAt:form.type==='Postpone'?null:localToUtc(form.newEndsAt,event.timezone)};
    const problem=validateChange(input);if(problem)throw new Error(problem);return input;
  };
  const updateForm=(key,value)=>{setForm(f=>({...f,[key]:value}));setPreview(null);};
  useEffect(()=>{
    if(!request)return;let active=true;setReading(true);setAffected(null);
    api.affected(id,request.id,filters).then(r=>{if(active)setAffected(r.data.data);}).catch(err=>{if(active)setError(message(err));}).finally(()=>{if(active)setReading(false);});
    return()=>{active=false;};
  },[id,request,filters]);
  return <section className="governance-panel ow-panel">
    <h2>Compliance documents</h2>
    {error&&<div className="ow-error" role="alert">{error}</div>}{notice&&<p role="status">{notice}</p>}
    <button className="tb-btn tb-btn-outline" disabled={busy||loading} onClick={load}>Refresh</button>
    {loading?<p>Loading governance records...</p>:record&&<>
      <p>Event #{id} · {event.title} · <strong>{record.status}</strong> · Version {record.version}</p>
      <p>Required: {record.requiredTypes.join(', ')}. PDF / PNG / JPEG, up to {Math.round(record.maxBytes/1048576)} MB. These are platform requirements.</p>
      {current.length===0?<p>No documents submitted.</p>:<ul>{current.map(doc=><li key={doc.id}>{doc.documentType}: {doc.fileName} · {date(doc.submittedAt)} <button disabled={busy} onClick={()=>download(doc)}>Download</button></li>)}</ul>}
      {editable&&<form onSubmit={e=>{e.preventDefault();run(async()=>{const problem=fileError(file,record.maxBytes);if(problem)throw new Error(problem);await api.upload(id,type,file);setFile(null);setFileKey(k=>k+1);setNotice('Document saved. Submit the concert when the required set is complete.');});}}>
        <label>Document type<select value={type} onChange={e=>setType(e.target.value)}>{record.requiredTypes.map(t=><option key={t}>{t}</option>)}</select></label>
        <label>File<input key={fileKey} type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={e=>setFile(e.target.files[0]||null)}/></label>
        <button className="tb-btn tb-btn-primary" disabled={busy||!file}>{busy?'Uploading document…':'Upload document'}</button>
      </form>}
      {!editable&&!admin&&<p>Documents are read-only while this concert is {event.status}.</p>}
      {admin&&event.status==='Pending'&&record.status==='PendingReview'&&<form onSubmit={e=>{e.preventDefault();run(async()=>{const problem=validateReview(decision,notes);if(problem)throw new Error(problem);await api.reviewCompliance(id,{version:record.version,decision,notes:notes.trim()});setNotes('');setNotice('Compliance decision saved. Concert approval is a separate action.');onUpdated?.();});}}>
        <label>Compliance decision<select value={decision} onChange={e=>setDecision(e.target.value)}>{['Approved','Rejected','RequestMoreInfo'].map(d=><option key={d}>{d}</option>)}</select></label>
        <label>Review notes<textarea required maxLength={2000} value={notes} onChange={e=>setNotes(e.target.value)}/></label>
        <button className="tb-btn tb-btn-primary" disabled={busy}>Submit review</button>
      </form>}
      <details><summary>Document and review history</summary>
        <ul>{record.documents.filter(d=>d.version!==record.version).map(d=><li key={d.id}>Version {d.version} · {d.documentType} · {d.fileName} <button disabled={busy} onClick={()=>download(d)}>Download</button></li>)}</ul>
        {record.reviews.map(r=><p key={r.id}>Version {r.version} · {r.decision} · Admin #{r.reviewedBy} · {date(r.reviewedAt)}: {r.notes}</p>)}
      </details>
    </>}
    <h2>Postpone / Reschedule</h2>
    <p>Current schedule: {event.status==='Postponed'?'To be announced':`${date(event.startsAt)} – ${date(event.endsAt)}`} · Timezone: {event.timezone}</p>
    {changeable&&<form onSubmit={e=>{e.preventDefault();run(async()=>{const input=buildInput(),r=await api.preview(id,input);setPreview({...r.data.data,input});});}}>
      <label>Change type<select value={form.type} onChange={e=>updateForm('type',e.target.value)}><option>Reschedule</option>{event.status!=='Postponed'&&<option>Postpone</option>}</select></label>
      {form.type==='Reschedule'&&<><label>New start in concert timezone<input type="datetime-local" required value={form.newStartsAt} onChange={e=>updateForm('newStartsAt',e.target.value)}/></label><label>New end in concert timezone<input type="datetime-local" required value={form.newEndsAt} onChange={e=>updateForm('newEndsAt',e.target.value)}/></label></>}
      <label>Reason<textarea required minLength={10} maxLength={1000} value={form.reason} onChange={e=>updateForm('reason',e.target.value)}/></label>
      <button className="tb-btn tb-btn-outline" disabled={busy}>Review change</button>
      {preview&&<div><p>Requested: {date(preview.input.newStartsAt)} – {date(preview.input.newEndsAt)}</p><p>Estimated impact: {preview.affected.summary.orders} orders · {preview.affected.summary.tickets} tickets · {money(preview.affected.summary.totalPaid)}</p><p>The current schedule stays live until Admin approval.</p><button type="button" className="tb-btn tb-btn-primary" disabled={busy} onClick={()=>run(async()=>{await api.submitChange(id,preview.input);setPreview(null);setNotice('Change request submitted. Waiting for Admin review.');})}>Submit for approval</button></div>}
    </form>}
    {changes.length===0?<p>No schedule change requests.</p>:changes.map(change=><article key={change.id} className="governance-change">
      <strong>CR-{change.id} · {change.type} · {change.status}</strong><p>{change.reason}</p><p>{date(change.oldStartsAt)} → {date(change.newStartsAt)} · Synchronization: {change.processingStatus}</p>
      {change.reviewNotes&&<p>Admin #{change.reviewedBy}: {change.reviewNotes}</p>}
      {change.status!=='Rejected'&&<button disabled={busy} onClick={()=>{setRequest(change);setFilters({tab:'orders',status:'',orderCode:'',userId:'',page:1,pageSize:10});setSearch({status:'',orderCode:'',userId:''});}}>View affected orders / tickets</button>}
      {admin&&change.status==='Pending'&&<p><a href={`/admin/change-requests?request=${change.id}`}>Decide on the Change requests page</a></p>}
    </article>)}
    {request&&<section aria-label="Affected orders and tickets"><h3>CR-{request.id} · Event #{id} · {event.title}</h3><button onClick={()=>setRequest(null)}>Close</button>
      <p>{request.status==='Pending'?'Estimated impact before approval':'Applied change records'}</p>
      {affected&&<p>{affected.summary.orders} orders · {affected.summary.tickets} tickets · Paid {money(affected.summary.totalPaid)} · Confirmed refunded {money(affected.summary.refunded)}</p>}
      <div>{['orders','tickets'].map(tab=><button key={tab} disabled={reading} onClick={()=>setFilters(f=>({...f,tab,page:1}))}>{tab}</button>)}</div>
      <form onSubmit={e=>{e.preventDefault();if(search.userId&&(!/^\d+$/.test(search.userId)||Number(search.userId)<1)){setError('User ID must be a positive integer.');return;}setFilters(f=>({...f,...search,page:1}));}}>
        <label>Status<select value={search.status} onChange={e=>setSearch(s=>({...s,status:e.target.value}))}><option value="">All</option>{['Pending','Notified','RefundPending','Refunded','RefundFailed','Returned','Active'].map(s=><option key={s}>{s}</option>)}</select></label>
        <label>Order code<input value={search.orderCode} onChange={e=>setSearch(s=>({...s,orderCode:e.target.value}))}/></label><label>User ID<input inputMode="numeric" value={search.userId} onChange={e=>setSearch(s=>({...s,userId:e.target.value}))}/></label>
        <button disabled={reading}>Search</button><button type="button" disabled={reading} onClick={()=>{setSearch({status:'',orderCode:'',userId:''});setFilters(f=>({...f,status:'',orderCode:'',userId:'',page:1}));}}>Reset</button>
      </form>
      {reading?<p>Loading affected records...</p>:affected&&<><div className="governance-table"><table><thead><tr><th>Order</th><th>User</th><th>{filters.tab==='orders'?'Tickets':'Ticket'}</th><th>{filters.tab==='orders'?'Paid amount':'Ticket status'}</th><th>Processing</th></tr></thead><tbody>{affected.items.map((row,i)=><tr key={row.ticketId||row.orderId||i}><td>ORD-{row.orderId}</td><td>User #{row.customerId}</td><td>{filters.tab==='orders'?row.tickets:row.ticketCode}</td><td>{filters.tab==='orders'?money(row.amount):row.ticketStatus}</td><td>{row.status}</td></tr>)}</tbody></table></div>{affected.items.length===0&&<p>No affected records found.</p>}<button disabled={affected.page<=1} onClick={()=>setFilters(f=>({...f,page:affected.page-1}))}>Prev</button> Page {affected.page} / {affected.totalPages} <button disabled={affected.page>=affected.totalPages} onClick={()=>setFilters(f=>({...f,page:affected.page+1}))}>Next</button><button onClick={()=>setFilters(f=>({...f}))}>Retry / Refresh</button></>}
    </section>}
  </section>;
}
