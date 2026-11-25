import { useCallback, useEffect, useState } from 'react';
import { governanceApi as api } from '../api/governanceApi';
import { validateChange, validateReview, canPublish, fileError, localToUtc } from '../utils/governanceRules';
import { useToast } from './ToastProvider';
import './GovernancePanel.css';

const message = err => err.response?.data?.message || err.message || 'Request failed. Please retry.';

const money = amount => new Intl.NumberFormat('en-US').format(amount || 0) + ' VND';

const date = value => value ? new Date(value).toLocaleString() : 'To be announced';

const REASON_CATEGORIES = [
  'Natural Disaster / Extreme Weather',
  'Authority Directive / Public Security',
  'Artist Medical Emergency',
  'Venue Structural Safety Hazard',
  'Technical / Logistical Emergency',
  'Other Genuine Force Majeure'
];

export default function GovernancePanel({ event, admin = false, onUpdated, onCompliance }) {
  const toast = useToast();
  const id = event.eventId;

  const [record, setRecord] = useState(null);
  const [changes, setChanges] = useState([]);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);

  const [type, setType] = useState('');
  const [file, setFile] = useState(null);
  const [fileKey, setFileKey] = useState(0);

  const [decision, setDecision] = useState('Approved');
  const [notes, setNotes] = useState('');

  const [category, setCategory] = useState(REASON_CATEGORIES[0]);
  const [form, setForm] = useState({ type: 'Reschedule', reason: '', newStartsAt: '', newEndsAt: '' });
  const [preview, setPreview] = useState(null);
  const [changeEvidenceFile, setChangeEvidenceFile] = useState(null);

  const [request, setRequest] = useState(null);
  const [affected, setAffected] = useState(null);
  const [reading, setReading] = useState(false);
  const [filters, setFilters] = useState({ tab: 'orders', status: '', orderCode: '', userId: '', page: 1, pageSize: 10 });
  const [search, setSearch] = useState({ status: '', orderCode: '', userId: '' });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [c, r] = await Promise.all([api.compliance(id), api.changes(id)]);
      setRecord(c.data.data);
      setChanges(r.data.data);
      setType(t => t || c.data.data.requiredTypes[0] || '');
      onCompliance?.(canPublish(c.data.data));
    } catch (err) {
      setError(message(err));
    } finally {
      setLoading(false);
    }
  }, [id, onCompliance]);

  useEffect(() => { load(); }, [load]);

  const [autoOpened, setAutoOpened] = useState(false);
  useEffect(() => {
    if (autoOpened || changes.length === 0) return;
    const wanted = Number(new URLSearchParams(window.location.search).get('request'));
    const match = changes.find(c => c.id === wanted && c.status !== 'Rejected');
    setAutoOpened(true);
    if (match) {
      setRequest(match);
      setFilters({ tab: 'orders', status: '', orderCode: '', userId: '', page: 1, pageSize: 10 });
      setSearch({ status: '', orderCode: '', userId: '' });
    }
  }, [changes, autoOpened]);

  const run = async action => {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      await load();
    } catch (err) {
      setError(message(err));
    } finally {
      setBusy(false);
    }
  };

  const download = doc => run(async () => {
    const response = await api.download(id, doc.id);
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = doc.fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  });

  const editable = !admin && ['Draft', 'Rejected'].includes(event.status);
  const current = record?.documents.filter(d => d.version === record.version) || [];

  const changeable = !admin && ['Published', 'Postponed'].includes(event.status) && !changes.some(c => c.status === 'Pending');

  const buildInput = () => {
    const isPostpone = form.type === 'Postpone';
    const combinedReason = `[${category}] ${form.reason.trim()}`;
    const input = {
      type: form.type,
      reason: combinedReason,
      newStartsAt: isPostpone ? null : localToUtc(form.newStartsAt, event.timezone),
      newEndsAt: isPostpone ? null : localToUtc(form.newEndsAt, event.timezone)
    };
    const problem = validateChange(input);
    if (problem) throw new Error(problem);
    return input;
  };

  const updateForm = (key, value) => {
    setForm(f => ({ ...f, [key]: value }));
    setPreview(null);
  };

  const getNowLocal = () => {
    const d = new Date();
    d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
    return d.toISOString().slice(0, 16);
  };
  const nowMin = getNowLocal();

  const isStartInPast = form.type === 'Reschedule' && form.newStartsAt && form.newStartsAt <= nowMin;
  const isEndBeforeStart = form.type === 'Reschedule' && form.newStartsAt && form.newEndsAt && form.newEndsAt <= form.newStartsAt;

  const ticketTypes = event?.ticketTypes || [];
  const earliestSalesStart = ticketTypes.reduce((min, t) => {
    if (!t.salesStartsAt) return min;
    const dt = new Date(t.salesStartsAt).getTime();
    return !min || dt < min ? dt : min;
  }, null);

  const isStartBeforeSales = Boolean(
    form.type === 'Reschedule' &&
    form.newStartsAt &&
    earliestSalesStart &&
    new Date(form.newStartsAt).getTime() <= earliestSalesStart
  );

  const isRescheduleInvalid =
    form.type === 'Reschedule' &&
    (!form.newStartsAt || !form.newEndsAt || isStartInPast || isEndBeforeStart || isStartBeforeSales);

  useEffect(() => {
    if (!request) return;
    let active = true;
    setReading(true);
    setAffected(null);
    api.affected(id, request.id, filters)
      .then(r => { if (active) setAffected(r.data.data); })
      .catch(err => { if (active) setError(message(err)); })
      .finally(() => { if (active) setReading(false); });
    return () => { active = false; };
  }, [id, request, filters]);

  return (
    <section className="governance-panel ow-panel">
      {(event.salesFrozen || event.status === 'Postponed') && (
        <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #f87171', borderRadius: '8px', marginBottom: '16px' }}>
          <strong style={{ color: '#b91c1c', display: 'flex', alignItems: 'center', gap: '8px' }}>
            Ticket Sales Frozen
          </strong>
          <p style={{ margin: '4px 0 0 0', fontSize: '13px', color: '#7f1d1d' }}>
            Ticket sales are currently frozen for this concert to guarantee inventory consistency while schedule changes or refunds are being processed.
          </p>
        </div>
      )}

      <h2>Compliance Documents & Governance</h2>
      {error && <div className="ow-error" role="alert">{error}</div>}
      {notice && <p role="status" style={{ color: '#047857', fontWeight: 500 }}>{notice}</p>}
      <button className="tb-btn tb-btn-outline" disabled={busy || loading} onClick={load}>Refresh</button>

      {loading ? <p><span className="app-loading-spinner" role="status" aria-label="Loading" /></p> : record && (
        <>
          <p>Concert #{id} · {event.title} · <strong>Status: {record.status}</strong> · Version {record.version}</p>
          <p>Required: {record.requiredTypes.join(', ')}. PDF / PNG / JPEG, up to {Math.round(record.maxBytes / 1048576)} MB.</p>
          {current.length === 0 ? <p>No documents submitted.</p> : (
            <ul>
              {current.map(doc => (
                <li key={doc.id}>
                  {doc.documentType}: {doc.fileName} · {date(doc.submittedAt)}{' '}
                  <button disabled={busy} onClick={() => download(doc)}>Download</button>
                </li>
              ))}
            </ul>
          )}

          {editable && (
            <form onSubmit={e => {
              e.preventDefault();
              run(async () => {
                const problem = fileError(file, record.maxBytes);
                if (problem) throw new Error(problem);
                await api.upload(id, type, file);
                setFile(null);
                setFileKey(k => k + 1);
                setNotice('Document saved. Submit the concert when the required set is complete.');
              });
            }}>
              <label>Document type
                <select value={type} onChange={e => setType(e.target.value)}>
                  {record.requiredTypes.map(t => <option key={t}>{t}</option>)}
                </select>
              </label>
              <div className="gov-file-field">
                <span className="gov-file-label">File</span>
                <div className="gov-file-picker">
                  <label className="gov-file-btn" htmlFor="gov-file-input">Choose File</label>
                  <span className="gov-file-name">{file ? file.name : "No file chosen"}</span>
                  <input
                    id="gov-file-input"
                    key={fileKey}
                    type="file"
                    accept=".pdf,.png,.jpg,.jpeg"
                    style={{ display: "none" }}
                    onChange={e => setFile(e.target.files[0] || null)}
                  />
                </div>
              </div>
              <button className="tb-btn tb-btn-primary" disabled={busy || !file}>
                {busy ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : 'Upload document'}
              </button>
            </form>
          )}

          {!editable && !admin && <p>Documents are read-only while this concert is {event.status}.</p>}

          {admin && event.status === 'Pending' && record.status === 'PendingReview' && (
            <form className="gov-admin-review-form" onSubmit={e => {
              e.preventDefault();
              run(async () => {
                const problem = validateReview(decision, notes);
                if (problem) throw new Error(problem);
                await api.reviewCompliance(id, { version: record.version, decision, notes: notes.trim() });
                setNotes('');
                setNotice('Compliance decision saved. Concert approval is a separate action.');
                onUpdated?.();
              });
            }}>
              <label>Compliance decision
                <select value={decision} onChange={e => setDecision(e.target.value)}>
                  {['Approved', 'Rejected', 'RequestMoreInfo'].map(d => <option key={d}>{d}</option>)}
                </select>
              </label>
              <label style={{ flex: 2 }}>Review notes
                <input
                  type="text"
                  placeholder="Enter review notes or compliance feedback..."
                  maxLength={2000}
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                />
              </label>
              <button className="tb-btn tb-btn-primary" disabled={busy}>Submit review</button>
            </form>
          )}

          <details>
            <summary>Document and review history</summary>
            <ul>{record.documents.filter(d => d.version !== record.version).map(d => <li key={d.id}>Version {d.version} · {d.documentType} · {d.fileName} <button disabled={busy} onClick={() => download(d)}>Download</button></li>)}</ul>
            {record.reviews.map(r => <p key={r.id}>Version {r.version} · {r.decision} · Admin #{r.reviewedBy} · {date(r.reviewedAt)}: {r.notes}</p>)}
          </details>
        </>
      )}

      <h2 style={{ marginTop: '28px' }}>Event Change Governance</h2>
      <p style={{ margin: "4px 0 10px 0", color: "#6b7280", fontSize: "13px" }}>
        Formal workflow for event schedule modification after ticket sales have opened. Requires Force Majeure justification, impact preview, and Admin approval.
      </p>
      <p>Current schedule: {event.status === 'Postponed' ? 'To be announced (Postponed)' : `${date(event.startsAt)} – ${date(event.endsAt)}`} · Timezone: {event.timezone}</p>

      {changeable && (
        <form className="gov-change-schedule-form" onSubmit={e => {
          e.preventDefault();
          run(async () => {
            const input = buildInput();
            const r = await api.preview(id, input);
            setPreview({ ...r.data.data, input });
          });
        }}>
          <label>Change type
            <select value={form.type} onChange={e => updateForm('type', e.target.value)}>
              <option value="Reschedule">Reschedule (Change Dates)</option>
              {event.status !== 'Postponed' && <option value="Postpone">Postpone (Hold Indefinitely)</option>}
            </select>
          </label>

          <label>Reason Category (Force Majeure)
            <select value={category} onChange={e => { setCategory(e.target.value); setPreview(null); }}>
              {REASON_CATEGORIES.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
          </label>

          {form.type === 'Reschedule' && (
            <>
              <label>
                New start in concert timezone
                <input
                  type="datetime-local"
                  required
                  min={nowMin}
                  value={form.newStartsAt}
                  onChange={e => updateForm('newStartsAt', e.target.value)}
                  style={isStartInPast || isStartBeforeSales ? { borderColor: '#ef4444', background: '#fef2f2' } : {}}
                />
                {isStartInPast && (
                  <span style={{ color: '#ef4444', fontSize: '11px', marginTop: '2px', fontWeight: 500 }}>
                    Start time must be in the future (after now)
                  </span>
                )}
                {!isStartInPast && isStartBeforeSales && (
                  <span style={{ color: '#ea580c', fontSize: '11px', marginTop: '2px', fontWeight: 500 }}>
                    Start time is earlier than ticket sales opening ({new Date(earliestSalesStart).toLocaleDateString()})
                  </span>
                )}
              </label>

              <label>
                New end in concert timezone
                <input
                  type="datetime-local"
                  required
                  min={form.newStartsAt || nowMin}
                  value={form.newEndsAt}
                  onChange={e => updateForm('newEndsAt', e.target.value)}
                  style={isEndBeforeStart ? { borderColor: '#ef4444', background: '#fef2f2' } : {}}
                />
                {isEndBeforeStart && (
                  <span style={{ color: '#ef4444', fontSize: '11px', marginTop: '2px', fontWeight: 500 }}>
                    End time must be after start time
                  </span>
                )}
              </label>
            </>
          )}

          <label style={{ flex: 2 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span>Detailed justification / authority reference</span>
              <span style={{ fontSize: '11px', color: form.reason.trim().length < 10 ? '#ef4444' : '#6b7280' }}>
                {form.reason.trim().length < 10 
                  ? `Need at least ${10 - form.reason.trim().length} more characters` 
                  : `${form.reason.length} / 1000`}
              </span>
            </div>
            <input
              type="text"
              placeholder="Provide clear justification or government directive reference (min 10 characters)..."
              required
              minLength={10}
              maxLength={1000}
              value={form.reason}
              onChange={e => updateForm('reason', e.target.value)}
            />
          </label>

          <div style={{ width: '100%', marginTop: '6px', marginBottom: '8px' }}>
            <span style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#374151', marginBottom: '4px' }}>
              Supporting Evidence / Legal Directive (Optional: PDF, PNG, JPG - max 15MB)
            </span>
            {!changeEvidenceFile ? (
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', padding: '9px 12px', border: '1px dashed #cbd5e1', borderRadius: '6px', cursor: 'pointer', background: '#f8fafc' }}>
                <input
                  type="file"
                  accept=".pdf,image/png,image/jpeg,image/jpg"
                  style={{ display: 'none' }}
                  onChange={e => {
                    const f = e.target.files?.[0];
                    if (f) {
                      if (f.size > 15 * 1024 * 1024) {
                        setError('File exceeds 15MB limit.');
                        return;
                      }
                      setChangeEvidenceFile(f);
                    }
                  }}
                />
                <span style={{ fontSize: '13px', color: '#64748b' }}>Attach venue confirmation, ministerial permit, or authority notice</span>
              </label>
            ) : (
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '8px 12px', background: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '6px', width: '100%', boxSizing: 'border-box' }}>
                <span style={{ fontSize: '13px', color: '#1e40af', fontWeight: 600 }}>{changeEvidenceFile.name} ({(changeEvidenceFile.size / (1024 * 1024)).toFixed(2)} MB)</span>
                <button type="button" style={{ background: 'none', border: 'none', color: '#dc2626', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }} onClick={() => setChangeEvidenceFile(null)}>Remove</button>
              </div>
            )}
          </div>

          <button
            className="tb-btn tb-btn-outline"
            disabled={busy || isRescheduleInvalid || form.reason.trim().length < 10}
          >
            Review change impact
          </button>

          {preview && (
            <div style={{ width: '100%', marginTop: '12px', padding: '14px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '8px' }}>
              <h4 style={{ margin: '0 0 8px 0', color: '#0f172a' }}>Impact Preview</h4>
              <p style={{ margin: '4px 0' }}>Target Schedule: {preview.input.newStartsAt ? `${date(preview.input.newStartsAt)} – ${date(preview.input.newEndsAt)}` : 'Indefinite Postponement'}</p>
              <p style={{ margin: '4px 0', fontWeight: 600, color: '#1e40af' }}>
                Estimated Impact: {preview.affected.summary.orders} orders · {preview.affected.summary.tickets} tickets · Total value: {money(preview.affected.summary.totalPaid)}
              </p>
              <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 10px 0' }}>
                The current schedule stays live until Administrator approval. When approved, ticket sales will freeze immediately and affected customers will be notified.
              </p>
              {changeEvidenceFile && (
                <p style={{ fontSize: '13px', color: '#0284c7', margin: '4px 0 8px 0', fontWeight: 500 }}>
                  Attached Evidence: {changeEvidenceFile.name} (will be archived with the compliance dossier)
                </p>
              )}
              <button
                type="button"
                className="tb-btn tb-btn-primary"
                disabled={busy}
                onClick={() => run(async () => {
                  if (changeEvidenceFile) {
                    try {
                      await api.upload(id, 'ChangeSupportingEvidence', changeEvidenceFile);
                    } catch (uploadErr) {
                      console.warn('Could not upload evidence document:', uploadErr);
                    }
                  }
                  const finalInput = {
                    ...preview.input,
                    reason: changeEvidenceFile 
                      ? `${preview.input.reason} [Attached Evidence: ${changeEvidenceFile.name}]`
                      : preview.input.reason
                  };
                  await api.submitChange(id, finalInput);
                  setPreview(null);
                  setChangeEvidenceFile(null);
                  setForm({ type: 'Reschedule', reason: '', newStartsAt: '', newEndsAt: '' });
                  setNotice('Change request submitted successfully with supporting evidence. Waiting for Administrator review.');
                })}
              >
                Submit for approval
              </button>
            </div>
          )}
        </form>
      )}

      {changes.length === 0 ? <p>No schedule change requests recorded.</p> : changes.map(change => (
        <article key={change.id} className="governance-change">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <strong>CR-{change.id} · {change.type} · <span className={`gov-status-${change.status.toLowerCase()}`}>{change.status}</span></strong>
            <span style={{ fontSize: '12px', color: '#6b7280' }}>Submitted: {date(change.submittedAt)}</span>
          </div>
          <p style={{ margin: '6px 0', color: '#334155' }}>{change.reason}</p>
          <p style={{ fontSize: '13px', margin: '4px 0' }}>
            Schedule: {date(change.oldStartsAt)} → {change.newStartsAt ? date(change.newStartsAt) : 'Postponed'} · Synchronization: <strong>{change.processingStatus}</strong>
          </p>
          {change.reviewNotes && (
            <p style={{ fontSize: '13px', color: '#475569', background: '#f1f5f9', padding: '6px 10px', borderRadius: '4px' }}>
              Admin #{change.reviewedBy}: {change.reviewNotes}
            </p>
          )}
          {change.status !== 'Rejected' && (
            <button
              disabled={busy}
              className="tb-btn tb-btn-outline"
              style={{ marginTop: '6px' }}
              onClick={() => {
                setRequest(change);
                setFilters({ tab: 'orders', status: '', orderCode: '', userId: '', page: 1, pageSize: 10 });
                setSearch({ status: '', orderCode: '', userId: '' });
              }}
            >
              View affected orders / tickets
            </button>
          )}
          {admin && change.status === 'Pending' && (
            <div style={{ marginTop: '10px' }}>
              <button
                type="button"
                className="tb-btn tb-btn-primary"
                onClick={() => { window.location.href = `/admin/change-requests?request=${change.id}`; }}
              >
                Open Review Decision Page
              </button>
            </div>
          )}
        </article>
      ))}

      {request && (
        <section aria-label="Affected orders and tickets" style={{ marginTop: '24px', padding: '16px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '8px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0 }}>CR-{request.id} Audit · Concert #{id} · {event.title}</h3>
            <button className="tb-btn tb-btn-outline" onClick={() => setRequest(null)}>Close</button>
          </div>
          <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 10px 0' }}>
            {request.status === 'Pending' ? 'Estimated impact before approval' : 'Applied change records (Synchronized with Ticket API)'}
          </p>

          {affected && (
            <p style={{ fontWeight: 600, color: '#1e40af', margin: '8px 0' }}>
              Total: {affected.summary.orders} orders · {affected.summary.tickets} tickets · Paid: {money(affected.summary.totalPaid)} · Refunded: {money(affected.summary.refunded)}
            </p>
          )}

          <div style={{ display: 'flex', gap: '8px', margin: '12px 0' }}>
            {['orders', 'tickets'].map(tab => (
              <button
                key={tab}
                disabled={reading}
                className={`tb-btn ${filters.tab === tab ? 'tb-btn-primary' : 'tb-btn-outline'}`}
                onClick={() => setFilters(f => ({ ...f, tab, page: 1 }))}
              >
                {tab === 'orders' ? 'Impacted Orders' : 'Impacted Tickets'}
              </button>
            ))}
          </div>

          <form
            onSubmit={e => {
              e.preventDefault();
              if (search.userId && (!/^\d+$/.test(search.userId) || Number(search.userId) < 1)) {
                setError('User ID must be a positive integer.');
                return;
              }
              setFilters(f => ({ ...f, ...search, page: 1 }));
            }}
            style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: '14px' }}
          >
            <label style={{ fontSize: '12px' }}>Status
              <select value={search.status} onChange={e => setSearch(s => ({ ...s, status: e.target.value }))}>
                <option value="">All</option>
                {['Pending', 'Notified', 'RefundPending', 'Refunded', 'RefundFailed', 'Returned', 'Active'].map(s => <option key={s}>{s}</option>)}
              </select>
            </label>
            <label style={{ fontSize: '12px' }}>Order code
              <input value={search.orderCode} placeholder="ORD-..." onChange={e => setSearch(s => ({ ...s, orderCode: e.target.value }))} />
            </label>
            <label style={{ fontSize: '12px' }}>User ID
              <input inputMode="numeric" placeholder="123" value={search.userId} onChange={e => setSearch(s => ({ ...s, userId: e.target.value }))} />
            </label>
            <button className="tb-btn tb-btn-outline" disabled={reading}>Search</button>
            <button
              type="button"
              className="tb-btn tb-btn-outline"
              disabled={reading}
              onClick={() => {
                setSearch({ status: '', orderCode: '', userId: '' });
                setFilters(f => ({ ...f, status: '', orderCode: '', userId: '', page: 1 }));
              }}
            >
              Reset
            </button>
          </form>

          {reading ? <p><span className="app-loading-spinner" role="status" aria-label="Loading" /></p> : affected && (
            <>
              <div className="governance-table">
                <table>
                  <thead>
                    <tr>
                      <th>Order</th>
                      <th>Customer</th>
                      <th>{filters.tab === 'orders' ? 'Tickets' : 'Ticket Code'}</th>
                      <th>{filters.tab === 'orders' ? 'Paid Amount' : 'Ticket Status'}</th>
                      <th>Processing Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {affected.items.map((row, i) => (
                      <tr key={row.ticketId || row.orderId || i}>
                        <td>ORD-{row.orderId}</td>
                        <td>User #{row.customerId}</td>
                        <td>{filters.tab === 'orders' ? row.tickets : row.ticketCode}</td>
                        <td>{filters.tab === 'orders' ? money(row.amount) : row.ticketStatus}</td>
                        <td><span className={`gov-status-${(row.status || '').toLowerCase()}`}>{row.status}</span></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {affected.items.length === 0 && <p>No affected records found for the selected filter.</p>}
              <div style={{ marginTop: '10px', display: 'flex', gap: '8px', alignItems: 'center' }}>
                <button className="tb-btn tb-btn-outline" disabled={affected.page <= 1} onClick={() => setFilters(f => ({ ...f, page: affected.page - 1 }))}>Prev</button>
                <span style={{ fontSize: '13px' }}>Page {affected.page} / {affected.totalPages}</span>
                <button className="tb-btn tb-btn-outline" disabled={affected.page >= affected.totalPages} onClick={() => setFilters(f => ({ ...f, page: affected.page + 1 }))}>Next</button>
                <button className="tb-btn tb-btn-outline" onClick={() => setFilters(f => ({ ...f }))}>Refresh</button>
              </div>
            </>
          )}
        </section>
      )}
    </section>
  );
}
