import { useEffect, useState } from "react";
import kycAdminApi from "../../api/kycAdminApi";
import AdminShell from "./AdminShell";
import "./kycAdmin.css";

const errMsg = (e, fallback) => e.response?.data?.message || fallback;
const fmt = (v) => (v ? new Date(v).toLocaleString("en-GB") : "—");

export default function KycSettingsAdminPage() {
  const [form, setForm] = useState({ retentionDays: 0, keepDocumentHashAfterDeletion: false });
  const [meta, setMeta] = useState({ updatedAt: null, updatedBy: null });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const forever = Number(form.retentionDays) === 0;

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      const res = await kycAdminApi.getSettings();
      const d = res.data || {};
      setForm({
        retentionDays: d.retentionDays ?? 0,
        keepDocumentHashAfterDeletion: !!d.keepDocumentHashAfterDeletion,
      });
      setMeta({ updatedAt: d.updatedAt, updatedBy: d.updatedBy });
    } catch (e) {
      setError(errMsg(e, "Failed to load the retention policy."));
    } finally {
      setLoading(false);
    }
  };
  useEffect(() => { load(); }, []);

  const submit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    if (form.retentionDays < 0) return setError("Retention days can't be negative.");
    setSaving(true);
    try {
      const res = await kycAdminApi.updateSettings({
        retentionDays: Number(form.retentionDays),
        keepDocumentHashAfterDeletion: form.keepDocumentHashAfterDeletion,
      });
      const d = res.data || {};
      setMeta({ updatedAt: d.updatedAt, updatedBy: d.updatedBy });
      setSuccess("Retention policy updated.");
    } catch (e) {
      setError(errMsg(e, "Failed to update the retention policy."));
    } finally {
      setSaving(false);
    }
  };

  return (
    <AdminShell title="eKYC Data Retention">
      {error && <div className="auth-message error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
      {success && <div className="auth-message success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}

      <section className="admin-panel">
        <div className="panel-header">
          <h2>Retention policy</h2>
        </div>

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : (
          <form onSubmit={submit} style={{ padding: "4px 22px 22px" }}>
            <div className="kyc-settings-card">
              <div className="kyc-setting-row">
                <div className="kyc-setting-row__text">
                  <h3>Retention period</h3>
                  <p>
                    How long original eKYC documents (ID photos, selfie) are kept before they're
                    automatically deleted. Set to 0 to keep them indefinitely.
                  </p>
                </div>
                <div className="kyc-setting-row__control">
                  <div className="kyc-days-input">
                    <div className="kyc-input-unit">
                      <input
                        type="number"
                        min={0}
                        disabled={forever}
                        value={forever ? "" : form.retentionDays}
                        placeholder={forever ? "—" : "0"}
                        onChange={(e) => setForm((f) => ({ ...f, retentionDays: e.target.value }))}
                      />
                      <span className="kyc-unit-label">days</span>
                    </div>
                  </div>
                  <label className={`kyc-forever-toggle${forever ? " kyc-forever-toggle--active" : ""}`}>
                    <input
                      type="checkbox"
                      checked={forever}
                      onChange={(e) =>
                        setForm((f) => ({ ...f, retentionDays: e.target.checked ? 0 : f.retentionDays || 30 }))
                      }
                    />
                    Keep forever
                  </label>
                </div>
              </div>

              <div className="kyc-setting-row">
                <div className="kyc-setting-row__text">
                  <h3>Keep document hash after deletion</h3>
                  <p>
                    Retain a one-way hash of each deleted document for audit and fraud checks. No
                    personal image data is kept — the hash can't be turned back into a photo.
                  </p>
                </div>
                <div className="kyc-setting-row__control">
                  <span className="kyc-switch">
                    <input
                      type="checkbox"
                      checked={form.keepDocumentHashAfterDeletion}
                      onChange={(e) => setForm((f) => ({ ...f, keepDocumentHashAfterDeletion: e.target.checked }))}
                    />
                    <span className="kyc-switch__track" />
                  </span>
                </div>
              </div>
            </div>

            <div className="kyc-settings-footer">
              <p className="kyc-admin-note">
                Last updated: {fmt(meta.updatedAt)}
                {meta.updatedBy != null ? ` by admin #${meta.updatedBy}` : ""}
              </p>
              <button type="submit" className="admin-button" disabled={saving}>
                {saving ? "Saving..." : "Save changes"}
              </button>
            </div>
          </form>
        )}
      </section>
    </AdminShell>
  );
}
