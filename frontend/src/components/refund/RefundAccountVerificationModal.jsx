import { useState } from "react";
import EvidenceUploader from "../EvidenceUploader";
import ticketReturnApi from "../../api/ticketReturnApi";
import { maskAccountNumber } from "../../utils/refundPolicy";

export default function RefundAccountVerificationModal({ row, onClose, onSaved }) {
  const [form, setForm] = useState({ identityName: "", bankReportedName: "", identityEvidence: "", bankEvidence: "", customerConfirmationEvidence: "", notes: "", isSimulation: false });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const update = (key, value) => setForm(previous => ({ ...previous, [key]: value }));
  const submit = async event => {
    event.preventDefault(); setBusy(true); setError("");
    try {
      await ticketReturnApi.verifyRefundAccount(row.ticketReturnRequestId, form);
      await onSaved();
    } catch (failure) {
      setError(failure.response?.data?.message || "Could not record the account checks.");
    } finally { setBusy(false); }
  };
  return (
    <div className="rp-modal-backdrop">
      <form className="rp-modal" style={{ maxWidth: 720, maxHeight: "88vh", overflowY: "auto" }} onSubmit={submit}>
        <h3>Accounting: verify refund account — #{row.ticketReturnRequestId}</h3>
        <p>{row.bankName} · {maskAccountNumber(row.bankAccountNumber)} · {row.bankAccountName}</p>
        <p className="rp-muted">Check the bank's beneficiary lookup against the buyer's identity documents. Confirm the request through the customer's registered contact. Matching names alone does not establish ownership; these checks are manual.</p>
        <label className="ow-field"><span>Buyer name checked against identity / eKYC *</span>
          <input required maxLength={200} value={form.identityName} onChange={e => update("identityName", e.target.value)} />
        </label>
        <label className="ow-field"><span>Account holder returned by the bank *</span>
          <input required maxLength={200} value={form.bankReportedName} onChange={e => update("bankReportedName", e.target.value)} />
        </label>
        <EvidenceUploader label="Identity check evidence" optional={false} maxSizeMb={2} value={form.identityEvidence} onChange={value => update("identityEvidence", value)} hint="Attach the checked identity / eKYC evidence. Booking contact information is not verified identity." />
        <EvidenceUploader label="Bank beneficiary lookup evidence" optional={false} maxSizeMb={2} value={form.bankEvidence} onChange={value => update("bankEvidence", value)} hint="Use the bank's lookup result, not a name typed by the customer." />
        <EvidenceUploader label="Registered-contact confirmation evidence" optional={false} maxSizeMb={2} value={form.customerConfirmationEvidence} onChange={value => update("customerConfirmationEvidence", value)} hint="Record confirmation through the registered email / phone. This confirms the request, not bank ownership on its own." />
        <label className="ow-field"><span>Accounting verification notes *</span>
          <textarea required minLength={10} maxLength={1000} value={form.notes} onChange={e => update("notes", e.target.value)} />
        </label>
        {import.meta.env.DEV && <label className="ow-checkbox" style={{ margin: "12px 0" }}>
          <input type="checkbox" checked={form.isSimulation} onChange={e => update("isSimulation", e.target.checked)} />
          Simulation — sample evidence; no bank ownership check or real money transfer
        </label>}
        {error && <p role="alert" className="ow-error">{error}</p>}
        <div className="rp-modal-actions">
          <button type="button" className="tb-btn tb-btn-outline" disabled={busy} onClick={onClose}>Cancel</button>
          <button type="submit" className="tb-btn tb-btn-primary" disabled={busy || !form.identityEvidence || !form.bankEvidence || !form.customerConfirmationEvidence}>{busy ? "Saving..." : "Record account checks"}</button>
        </div>
      </form>
    </div>
  );
}
