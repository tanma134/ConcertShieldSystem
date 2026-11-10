import { useEffect, useState } from "react";
import kycAdminApi from "../../api/kycAdminApi";
import AdminShell from "./AdminShell";
import "./kycAdmin.css";

const errMsg = (e, fallback) => e.response?.data?.message || fallback;
const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "N/A");
const getId = (v) => v.id;

const EMPTY_ITEM = { heading: "", content: "" };

function ConsentPreview({ data }) {
  return (
    <div className="kyc-preview">
      {data.title && <h4 style={{ margin: "0 0 4px" }}>{data.title}</h4>}
      <dl>
        {(data.items || []).map((it, i) => (
          <div key={i}>
            <dt>{it.heading}</dt>
            <dd>{it.content}</dd>
          </div>
        ))}
      </dl>
      <div className="kyc-preview__check">☑ {data.checkboxText}</div>
    </div>
  );
}

function CreateModal({ onClose, onDone, setError }) {
  const [version, setVersion] = useState("");
  const [title, setTitle] = useState("");
  const [activate, setActivate] = useState(false);
  const [checkboxText, setCheckboxText] = useState("");
  const [items, setItems] = useState([{ ...EMPTY_ITEM }]);
  const [saving, setSaving] = useState(false);
  const [localError, setLocalError] = useState("");

  const setItem = (i, patch) => setItems((list) => list.map((it, idx) => (idx === i ? { ...it, ...patch } : it)));

  const submit = async (e) => {
    e.preventDefault();
    setLocalError("");
    const cleaned = items.map((it) => ({ heading: it.heading.trim(), content: it.content.trim() }));
    if (!version.trim() || !title.trim() || !checkboxText.trim())
      return setLocalError("Version, title and checkbox text are required.");
    if (cleaned.length === 0 || cleaned.some((it) => !it.heading || !it.content))
      return setLocalError("Every section needs a heading and content.");
    setSaving(true);
    try {
      await kycAdminApi.createConsentVersion({ version: version.trim(), title: title.trim(), activate, checkboxText: checkboxText.trim(), items: cleaned });
      onDone();
    } catch (err) {
      setLocalError(errMsg(err, "Failed to create the consent version."));
      setError("");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>New consent version</h3>
          <button type="button" className="icon-button" onClick={onClose}>x</button>
        </div>
        <form onSubmit={submit}>
          <div className="modal-body kyc-admin-fields" style={{ maxHeight: "65vh", overflowY: "auto" }}>
            <p className="kyc-admin-note">
              A version can't be edited once created. To change the wording, create a new version.
            </p>
            {localError && <div className="auth-message error">{localError}</div>}
            <label>
              <span>Version *</span>
              <input value={version} onChange={(e) => setVersion(e.target.value)} placeholder="e.g. 2026-09-v1" />
            </label>

            <label>
              <span>Title *</span>
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="e.g. Notice & consent to identity verification" />
            </label>

            <div className="kyc-admin-fields">
              <span style={{ fontSize: 13 }}>Sections *</span>
              {items.map((it, i) => (
                <div className="kyc-item-editor" key={i}>
                  <div className="kyc-item-editor__head">
                    <span>Section {i + 1}</span>
                    {items.length > 1 && (
                      <button type="button" className="text-button" onClick={() => setItems((l) => l.filter((_, idx) => idx !== i))}>
                        Remove
                      </button>
                    )}
                  </div>
                  <input value={it.heading} onChange={(e) => setItem(i, { heading: e.target.value })} placeholder="Heading (e.g. Purpose of processing)" />
                  <textarea rows={3} value={it.content} onChange={(e) => setItem(i, { content: e.target.value })} placeholder="Content shown to the user" />
                </div>
              ))}
              <button type="button" className="admin-button-secondary" onClick={() => setItems((l) => [...l, { ...EMPTY_ITEM }])}>
                + Add section
              </button>
            </div>

            <label>
              <span>Checkbox text *</span>
              <textarea rows={2} value={checkboxText} onChange={(e) => setCheckboxText(e.target.value)} placeholder="I have read and agree to..." />
            </label>
            <label style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <input type="checkbox" checked={activate} onChange={(e) => setActivate(e.target.checked)} />
              <span>Activate immediately (all users must consent again)</span>
            </label>
          </div>
          <div className="modal-footer">
            <button type="button" className="admin-button-secondary" onClick={onClose} disabled={saving}>Cancel</button>
            <button type="submit" className="admin-button" disabled={saving}>{saving ? "Saving..." : "Create"}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function KycConsentAdminPage() {
  const [versions, setVersions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [modal, setModal] = useState(null); // {type: "create"|"view"|"activate"|"delete", data?}
  const [busy, setBusy] = useState(false);

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await kycAdminApi.listConsentVersions();
      setVersions(Array.isArray(res.data) ? res.data : res.data?.items || []);
    } catch (e) {
      setError(errMsg(e, "Failed to load consent versions."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); }, []);

  const openView = async (v) => {
    setModal({ type: "view", data: null });
    try {
      const res = await kycAdminApi.getConsentVersion(getId(v));
      setModal({ type: "view", data: res.data });
    } catch (e) {
      setModal(null);
      setError(errMsg(e, "Failed to load the consent version."));
    }
  };

  const confirm = async () => {
    const { type, data } = modal;
    setBusy(true);
    try {
      const res = type === "activate"
        ? await kycAdminApi.activateConsentVersion(getId(data))
        : await kycAdminApi.deleteConsentVersion(getId(data));
      setSuccess(res.data?.message || "Done.");
      setModal(null);
      await load();
    } catch (e) {
      setModal(null);
      setError(errMsg(e, `Failed to ${type} the consent version.`));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AdminShell title="eKYC Consent">
      {error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
      {success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}

      <section className="admin-panel">
        <div className="panel-header">
          <h2>Consent versions</h2>
          <div style={{ display: "flex", gap: 8 }}>
            <button type="button" className="admin-button-secondary" onClick={load}>Refresh</button>
            <button type="button" className="admin-button" onClick={() => setModal({ type: "create" })}>+ New version</button>
          </div>
        </div>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : versions.length === 0 ? (
          <div className="empty-state">No consent versions yet.</div>
        ) : (
          <div className="table-wrap">
            <table className="user-table">
              <thead>
                <tr><th>Version</th><th>Title</th><th>Status</th><th>Consents</th><th>Created</th><th>Created by</th><th>Actions</th></tr>
              </thead>
              <tbody>
                {versions.map((v) => (
                  <tr key={getId(v)}>
                    <td><strong>{v.version}</strong></td>
                    <td>{v.title}</td>
                    <td>
                      <span className={`kyc-pill ${v.isActive ? "kyc-pill--active" : "kyc-pill--muted"}`}>
                        {v.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>{v.consentCount}</td>
                    <td>{fmt(v.createdAt)}</td>
                    <td>{v.createdBy != null ? `#${v.createdBy}` : "—"}</td>
                    <td>
                      <div className="table-actions">
                        <button type="button" className="text-button" onClick={() => openView(v)}>View</button>
                        <button type="button" className="text-button" disabled={v.isActive} onClick={() => setModal({ type: "activate", data: v })}>Activate</button>
                        <button type="button" className="danger-button" disabled={v.isActive} onClick={() => setModal({ type: "delete", data: v })}>Delete</button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {modal?.type === "create" && (
        <CreateModal
          setError={setError}
          onClose={() => setModal(null)}
          onDone={() => { setModal(null); setSuccess("Consent version created."); load(); }}
        />
      )}

      {modal?.type === "view" && (
        <div className="modal-overlay" onClick={() => setModal(null)}>
          <div className="modal-card" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Version {modal.data?.version ?? ""}{modal.data ? ` · ${modal.data.consentCount} consents` : ""}</h3>
              <button type="button" className="icon-button" onClick={() => setModal(null)}>x</button>
            </div>
            <div className="modal-body" style={{ maxHeight: "65vh", overflowY: "auto" }}>
              {modal.data ? <ConsentPreview data={modal.data} /> : <div className="empty-state">Loading...</div>}
            </div>
            <div className="modal-footer">
              <button type="button" className="admin-button-secondary" onClick={() => setModal(null)}>Close</button>
            </div>
          </div>
        </div>
      )}

      {(modal?.type === "activate" || modal?.type === "delete") && (
        <div className="modal-overlay" onClick={() => !busy && setModal(null)}>
          <div className="modal-card small" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>{modal.type === "activate" ? "Activate version" : "Delete version"}</h3>
            </div>
            <div className="modal-body">
              {modal.type === "activate" ? (
                <p>Activate <strong>{modal.data.version}</strong>? Every user will have to consent again on their next eKYC submission.</p>
              ) : (
                <p>Delete <strong>{modal.data.version}</strong>? This can't be undone.</p>
              )}
            </div>
            <div className="modal-footer">
              <button type="button" className="admin-button-secondary" onClick={() => setModal(null)} disabled={busy}>Cancel</button>
              <button type="button" className={`admin-button ${modal.type === "delete" ? "danger" : ""}`} onClick={confirm} disabled={busy}>
                {busy ? "Working..." : "Confirm"}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminShell>
  );
}
