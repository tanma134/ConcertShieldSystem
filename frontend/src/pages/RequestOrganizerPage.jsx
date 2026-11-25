import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import organizerRequestApi from "../api/organizerRequestApi";
import policyApi from "../api/policyApi";
import "../styles/organizerrequest.css";

const STATUS_LABEL = {
  Pending: "Pending",
  Approved: "Approved",
  Rejected: "Rejected",
};


const handleDownloadFile = async (fileUrl, fileName) => {
  if (!fileUrl) return;
  try {
    let downloadUrl = fileUrl;
    if (fileUrl.includes("res.cloudinary.com") && fileUrl.includes("/upload/")) {
      downloadUrl = fileUrl.replace("/upload/", "/upload/fl_attachment/");
    }
    const response = await fetch(downloadUrl);
    if (!response.ok) throw new Error("Fetch failed");
    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = blobUrl;
    link.download = fileName || "download";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(blobUrl);
  } catch (err) {
    let fallbackUrl = fileUrl;
    if (fileUrl.includes("res.cloudinary.com") && fileUrl.includes("/upload/")) {
      fallbackUrl = fileUrl.replace("/upload/", "/upload/fl_attachment/");
    }
    const link = document.createElement("a");
    link.href = fallbackUrl;
    link.download = fileName || "download";
    link.target = "_blank";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
};

const DOC_TYPE_LABEL = {
  BUSINESS_LICENSE: "Business License",
  REPRESENTATIVE_ID: "Representative ID",
  PORTFOLIO: "Portfolio",
};

const VIETNAM_BANKS = [
  "Vietcombank (VCB)",
  "Techcombank (TCB)",
  "MB Bank (MB)",
  "VietinBank (CTG)",
  "BIDV",
  "ACB",
  "VPBank",
  "TPBank",
  "Sacombank",
  "HDBank",
  "VIB",
  "OCB",
  "MSB",
  "SeABank",
  "Shinhan Bank",
  "Standard Chartered",
  "HSBC Vietnam",
  "Other Bank",
];

function StatusBadge({ status }) {
  const cls =
    status === "Approved"
      ? "org-badge--approved"
      : status === "Rejected"
      ? "org-badge--rejected"
      : "org-badge--pending";
  return <span className={`org-badge ${cls}`}>{STATUS_LABEL[status] || status}</span>;
}

function FileUploadField({ label, required = false, fileData, onSelect, onRemove, error }) {
  const inputRef = useRef(null);

  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > 10 * 1024 * 1024) {
      alert("File size exceeds 10MB limit.");
      return;
    }

    const ext = "." + file.name.split(".").pop().toLowerCase();
    const isPdf = ext === ".pdf";
    const previewUrl = isPdf ? null : URL.createObjectURL(file);
    onSelect({ file, name: file.name, size: file.size, isPdf, previewUrl });
  };

  return (
    <div className="org-field">
      <label>
        {label} {required ? "*" : <span className="optional">(optional)</span>}
      </label>
      <input
        ref={inputRef}
        type="file"
        accept=".jpg,.jpeg,.png,.webp,.pdf,image/jpeg,image/png,image/webp,application/pdf"
        style={{ display: "none" }}
        onChange={handleFileChange}
      />
      {!fileData ? (
        <button
          type="button"
          className="org-button org-button--ghost"
          style={{
            height: "40px",
            fontSize: "13px",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: "8px",
            ...(error ? { borderColor: "#ef4444" } : {}),
          }}
          onClick={() => inputRef.current?.click()}
        >
          <span>📁 Choose file (Image or PDF)</span>
        </button>
      ) : (
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "12px",
            padding: "8px 12px",
            background: "var(--org-field-bg)",
            border: "0.5px solid var(--org-line)",
            borderRadius: "8px",
          }}
        >
          {fileData.isPdf ? (
            <div
              style={{
                width: "36px",
                height: "36px",
                background: "#ef4444",
                color: "#fff",
                borderRadius: "6px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: "11px",
                fontWeight: 700,
                flexShrink: 0,
              }}
            >
              PDF
            </div>
          ) : (
            <img
              src={fileData.previewUrl}
              alt="preview"
              style={{
                width: "36px",
                height: "36px",
                objectFit: "cover",
                borderRadius: "6px",
                flexShrink: 0,
              }}
            />
          )}
          <div style={{ flex: 1, minWidth: 0, fontSize: "13px" }}>
            <div
              style={{
                color: "var(--org-text)",
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
              }}
            >
              {fileData.name}
            </div>
            <div style={{ color: "var(--org-text-faint)", fontSize: "11px" }}>
              {(fileData.size / 1024 / 1024).toFixed(2)} MB
            </div>
          </div>
          <button
            type="button"
            className="org-button org-button--ghost"
            style={{ height: "28px", padding: "0 10px", fontSize: "12px" }}
            onClick={() => handleDownloadFile(fileData.previewUrl, fileData.name)}
            title="Download file"
          >
            ⬇️ Download
          </button>
          <button
            type="button"
            className="org-button org-button--ghost"
            style={{ height: "28px", padding: "0 10px", fontSize: "12px" }}
            onClick={() => inputRef.current?.click()}
          >
            Change
          </button>
          <button
            type="button"
            className="org-button org-button--danger"
            style={{ height: "28px", padding: "0 10px", fontSize: "12px" }}
            onClick={onRemove}
          >
            Remove
          </button>
        </div>
      )}
      {error && (
        <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
          {error}
        </span>
      )}
    </div>
  );
}

export default function RequestOrganizerPage() {
  const { isOrganizer, refreshRoles } = useAuth();
  const [form, setForm] = useState({
    reason: "",
    companyName: "",
    taxCode: "",
    representativeName: "",
    bankAccountHolder: "",
    bankAccountNumber: "",
    bankName: "",
    bankBranch: "",
    website: "",
    phoneNumber: "",
    experience: "",
  });

  const [fieldErrors, setFieldErrors] = useState({});
  const [businessLicense, setBusinessLicense] = useState(null);
  const [representativeId, setRepresentativeId] = useState(null);
  const [portfolio, setPortfolio] = useState(null);
  const [viewingDoc, setViewingDoc] = useState(null);

  const [history, setHistory] = useState([]);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [pricingPolicy, setPricingPolicy] = useState({ platformFeePercent: 5.0, payoutDays: 7, versionCode: "" });

  const hasPending = history.some((r) => r.status === "Pending");

  const fetchPricingPolicy = async () => {
    try {
      const res = await policyApi.getPricingTerms();
      if (res?.data?.data) {
        setPricingPolicy({
          platformFeePercent: res.data.data.platformFeePercent ?? 5.0,
          payoutDays: res.data.data.payoutDays ?? 7,
          versionCode: res.data.data.versionCode ?? ""
        });
      }
    } catch {
      // ignore
    }
  };

  const fetchHistory = async () => {
    try {
      const res = await organizerRequestApi.getMyRequests();
      setHistory(res.data);
      if (!isOrganizer && res.data.some((request) => request.status === "Approved")) {
        await refreshRoles();
      }
    } catch {
      // ignore
    } finally {
      setLoadingHistory(false);
    }
  };

  useEffect(() => {
    fetchHistory();
    fetchPricingPolicy();
  }, []);

  useEffect(() => {
    const handlePolicyUpdated = () => fetchPricingPolicy();
    window.addEventListener("policy-updated", handlePolicyUpdated);
    return () => window.removeEventListener("policy-updated", handlePolicyUpdated);
  }, []);

  const validateSingleField = (name, value) => {
    let err = "";
    const v = (value ?? "").trim();
    if (name === "reason") {
      if (!v) err = "Please enter your reason for becoming an organizer.";
      else if (v.length < 10) err = "Reason must be at least 10 characters long.";
    } else if (name === "taxCode") {
      if (!v) err = "Tax code is required (Personal MST or Enterprise MST).";
      else if (!/^[0-9A-Za-z-]{10,14}$/.test(v)) err = "Tax code must be 10 to 14 characters.";
    } else if (name === "representativeName") {
      if (!v) err = "Full legal representative name is required.";
    } else if (name === "bankAccountHolder") {
      if (!v) err = "Bank account holder name is required.";
    } else if (name === "bankAccountNumber") {
      if (!v) err = "Bank account number is required.";
      else if (v.length < 6) err = "Bank account number must be at least 6 digits.";
    } else if (name === "bankName") {
      if (!v) err = "Please select or enter your bank name.";
    } else if (name === "website") {
      if (v && !/^(https?:\/\/)?([\da-z.-]+)\.([a-z.]{2,6})([/\w .-]*)*\/?$/i.test(v)) {
        err = "Please enter a valid website URL.";
      }
    } else if (name === "phoneNumber") {
      if (v && !/^(0|\+84)[0-9]{9,10}$/.test(v)) {
        err = "Phone number must be 10 digits starting with 0 or +84.";
      }
    }
    return err;
  };

  const validateForm = () => {
    const errors = {};
    const fieldsToValidate = [
      "reason",
      "taxCode",
      "representativeName",
      "bankAccountHolder",
      "bankAccountNumber",
      "bankName",
      "website",
      "phoneNumber",
    ];

    fieldsToValidate.forEach((f) => {
      const err = validateSingleField(f, form[f]);
      if (err) errors[f] = err;
    });

    if (!representativeId) {
      errors.representativeId = "Representative ID / Passport file is required.";
    }

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    const err = validateSingleField(name, value);
    setFieldErrors((prev) => {
      const next = { ...prev };
      if (err) {
        next[name] = err;
      } else {
        delete next[name];
      }
      return next;
    });
  };

  const handleBlur = (e) => {
    const { name, value } = e.target;
    const err = validateSingleField(name, value);
    setFieldErrors((prev) => {
      const next = { ...prev };
      if (err) {
        next[name] = err;
      } else {
        delete next[name];
      }
      return next;
    });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");

    if (!validateForm()) {
      setError("Please resolve the validation errors indicated below.");
      return;
    }

    setSubmitting(true);
    try {
      const formData = new FormData();
      formData.append("reason", form.reason.trim());
      if (form.companyName?.trim()) formData.append("companyName", form.companyName.trim());
      formData.append("taxCode", form.taxCode.trim());
      formData.append("representativeName", form.representativeName.trim());
      formData.append("bankAccountHolder", form.bankAccountHolder.trim());
      formData.append("bankAccountNumber", form.bankAccountNumber.trim());
      formData.append("bankName", form.bankName.trim());
      if (form.bankBranch?.trim()) formData.append("bankBranch", form.bankBranch.trim());
      if (form.website?.trim()) formData.append("website", form.website.trim());
      if (form.phoneNumber?.trim()) formData.append("phoneNumber", form.phoneNumber.trim());
      if (form.experience?.trim()) formData.append("experience", form.experience.trim());

      if (businessLicense?.file) {
        formData.append("businessLicenseFile", businessLicense.file);
      }
      formData.append("representativeIdFile", representativeId.file);
      if (portfolio?.file) {
        formData.append("portfolioFile", portfolio.file);
      }

      await organizerRequestApi.create(formData);
      setSuccess("Your organizer request has been submitted. We'll review it soon.");
      setForm({
        reason: "",
        companyName: "",
        taxCode: "",
        representativeName: "",
        bankAccountHolder: "",
        bankAccountNumber: "",
        bankName: "",
        bankBranch: "",
        website: "",
        phoneNumber: "",
        experience: "",
      });
      setBusinessLicense(null);
      setRepresentativeId(null);
      setPortfolio(null);
      fetchHistory();
    } catch (err) {
      const data = err.response?.data;
      const errorMsg =
        data?.message ||
        (data?.errors ? Object.values(data.errors).flat().join(" ") : null) ||
        data?.title ||
        (typeof data === "string" ? data : null) ||
        err.message ||
        "Something went wrong. Please try again.";
      setError(errorMsg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="org-page">
      <div className="org-container">
        <div className="org-card">
          <h1 className="org-title">Become an Organizer</h1>
          <p className="org-subtitle">
            Tell us about yourself and why you'd like to organize events on our platform.
          </p>

          {isOrganizer && (
            <div className="org-alert org-alert--success">
              You already have the <strong>Organizer</strong> role!{" "}
              <Link to="/organizer/events/new">Create your first event</Link>
            </div>
          )}

          {!isOrganizer && error && <div className="org-alert org-alert--error">{error}</div>}
          {!isOrganizer && success && <div className="org-alert org-alert--success">{success}</div>}

          {!isOrganizer && hasPending && !success && (
            <div className="org-alert org-alert--success">
              You already have a pending request. Please wait for our review.
            </div>
          )}

          {!isOrganizer && !hasPending && (
            <form onSubmit={handleSubmit}>
              <div className="org-field">
                <label>Reason *</label>
                <textarea
                  name="reason"
                  rows={4}
                  placeholder="Enter your reasons for becoming an organizer"
                  value={form.reason}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={1000}
                  style={fieldErrors.reason ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.reason && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.reason}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>
                  Company name <span className="optional">(optional for individuals)</span>
                </label>
                <input
                  name="companyName"
                  type="text"
                  placeholder="Enter company or organization name (leave blank if individual)"
                  value={form.companyName}
                  onChange={handleChange}
                  maxLength={200}
                />
              </div>

              <div className="org-field">
                <label>Tax code *</label>
                <input
                  name="taxCode"
                  type="text"
                  placeholder="Enter personal or enterprise tax code"
                  value={form.taxCode}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={50}
                  style={fieldErrors.taxCode ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.taxCode && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.taxCode}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>Legal representative full name *</label>
                <input
                  name="representativeName"
                  type="text"
                  placeholder="Enter legal representative or individual full name"
                  value={form.representativeName}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={200}
                  style={fieldErrors.representativeName ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.representativeName && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.representativeName}
                  </span>
                )}
              </div>

              {/* PAYOUT & BANK ACCOUNT INFORMATION */}
              <div
                style={{
                  background: "rgba(139, 92, 246, 0.08)",
                  border: "1px solid rgba(139, 92, 246, 0.25)",
                  borderRadius: "10px",
                  padding: "16px",
                  margin: "24px 0 16px 0",
                }}
              >
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "8px", marginBottom: "6px" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: "8px", fontWeight: "700", color: "#c4b5fd" }}>
                    Payment & Bank Account Information (Ticket Payout)
                  </div>
                  {pricingPolicy?.versionCode && (
                    <span style={{ fontSize: "11px", color: "#a78bfa", background: "rgba(139, 92, 246, 0.2)", padding: "2px 8px", borderRadius: "999px", fontWeight: "600" }}>
                      Policy {pricingPolicy.versionCode}
                    </span>
                  )}
                </div>
                <p style={{ margin: 0, fontSize: "13px", color: "#d1d5db", lineHeight: "1.5" }}>
                  ConcertShield will transfer ticket sales revenue directly to your registered bank account. Net sales (after deducting the platform service fee of <strong>{pricingPolicy?.platformFeePercent ?? 5}%</strong>) will be deposited within <strong>{pricingPolicy?.payoutDays ?? 7} business days</strong> after the post-event sales report is finalized. If you need expedited disbursement, please contact our support team at <a href="mailto:support@concertshield.com" style={{ color: "#ec4899", textDecoration: "underline" }}>support@concertshield.com</a>.
                </p>
              </div>

              <div className="org-field">
                <label>Account Holder Name *</label>
                <input
                  name="bankAccountHolder"
                  type="text"
                  placeholder="Enter full name of account holder (e.g. NGUYEN VAN A)"
                  value={form.bankAccountHolder}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={200}
                  style={fieldErrors.bankAccountHolder ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.bankAccountHolder && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.bankAccountHolder}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>Bank Account Number *</label>
                <input
                  name="bankAccountNumber"
                  type="text"
                  placeholder="Enter bank account number"
                  value={form.bankAccountNumber}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={100}
                  style={fieldErrors.bankAccountNumber ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.bankAccountNumber && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.bankAccountNumber}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>Bank Name *</label>
                <input
                  name="bankName"
                  list="vietnam-banks"
                  type="text"
                  placeholder="Select or enter bank name (e.g. Vietcombank)"
                  value={form.bankName}
                  onChange={handleChange}
                  onBlur={handleBlur}
                  required
                  maxLength={150}
                  style={fieldErrors.bankName ? { borderColor: "#ef4444" } : {}}
                />
                <datalist id="vietnam-banks">
                  {VIETNAM_BANKS.map((b) => (
                    <option key={b} value={b} />
                  ))}
                </datalist>
                {fieldErrors.bankName && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.bankName}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>
                  Bank Branch <span className="optional">(optional)</span>
                </label>
                <input
                  name="bankBranch"
                  type="text"
                  placeholder="Enter bank branch (e.g. Ho Chi Minh City Branch)"
                  value={form.bankBranch}
                  onChange={handleChange}
                  maxLength={150}
                />
              </div>

              <div className="org-field">
                <label>
                  Website <span className="optional">(optional)</span>
                </label>
                <input
                  name="website"
                  type="text"
                  placeholder="Enter website URL"
                  value={form.website}
                  onChange={handleChange}
                  maxLength={255}
                  style={fieldErrors.website ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.website && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.website}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>
                  Phone number <span className="optional">(optional)</span>
                </label>
                <input
                  name="phoneNumber"
                  type="tel"
                  placeholder="Enter contact phone number"
                  value={form.phoneNumber}
                  onChange={handleChange}
                  maxLength={20}
                  style={fieldErrors.phoneNumber ? { borderColor: "#ef4444" } : {}}
                />
                {fieldErrors.phoneNumber && (
                  <span className="org-field-error" style={{ color: "#ef4444", fontSize: "12px", marginTop: "4px", display: "block" }}>
                    {fieldErrors.phoneNumber}
                  </span>
                )}
              </div>

              <div className="org-field">
                <label>
                  Experience <span className="optional">(optional)</span>
                </label>
                <textarea
                  name="experience"
                  rows={3}
                  placeholder="Describe relevant event organization experience"
                  value={form.experience}
                  onChange={handleChange}
                  maxLength={2000}
                />
              </div>

              {/* 3 Upload Sections */}
              <FileUploadField
                label="Business registration license (optional for individuals)"
                required={false}
                fileData={businessLicense}
                onSelect={(val) => {
                  setBusinessLicense(val);
                  setFieldErrors((prev) => {
                    const n = { ...prev };
                    delete n.businessLicense;
                    return n;
                  });
                }}
                onRemove={() => setBusinessLicense(null)}
                error={fieldErrors.businessLicense}
              />

              <FileUploadField
                label="Representative ID / Passport"
                required
                fileData={representativeId}
                onSelect={(val) => {
                  setRepresentativeId(val);
                  setFieldErrors((prev) => {
                    const n = { ...prev };
                    delete n.representativeId;
                    return n;
                  });
                }}
                onRemove={() => setRepresentativeId(null)}
                error={fieldErrors.representativeId}
              />

              <FileUploadField
                label="Portfolio"
                required={false}
                fileData={portfolio}
                onSelect={setPortfolio}
                onRemove={() => setPortfolio(null)}
              />

              <button className="org-button" type="submit" disabled={submitting}>
                {submitting ? "Submitting..." : "Submit request"}
              </button>
            </form>
          )}
        </div>

        <div className="org-card">
          <h2 className="org-title" style={{ fontSize: "16px" }}>
            Your request history
          </h2>

          {loadingHistory ? (
            <p className="org-empty">Loading...</p>
          ) : history.length === 0 ? (
            <p className="org-empty">You haven't submitted any request yet.</p>
          ) : (
            <div>
              {history.map((r) => (
                <div className="org-history-item" key={r.requestId}>
                  <div className="org-history-item__top">
                    <StatusBadge status={r.status} />
                    <span className="org-history-item__date">
                      {new Date(r.createdAt).toLocaleDateString()}
                    </span>
                  </div>
                  <p className="org-history-item__reason">{r.reason}</p>
                  {r.status === "Rejected" && r.rejectionReason && (
                    <p className="org-history-item__note">Reason: {r.rejectionReason}</p>
                  )}
                  {r.reviewNote && (
                    <p className="org-history-item__note">Note: {r.reviewNote}</p>
                  )}

                  {/* Attached Documents List */}
                  {r.documents && r.documents.length > 0 && (
                    <div style={{ marginTop: "8px", display: "flex", flexWrap: "wrap", gap: "8px" }}>
                      {r.documents.map((doc) => (
                        <div
                          key={doc.documentId}
                          style={{
                            display: "inline-flex",
                            alignItems: "center",
                            background: "var(--org-field-bg)",
                            border: "0.5px solid var(--org-line)",
                            borderRadius: "6px",
                            overflow: "hidden",
                          }}
                        >
                          <button
                            type="button"
                            onClick={() => setViewingDoc(doc)}
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: "6px",
                              padding: "4px 8px",
                              background: "transparent",
                              border: "none",
                              fontSize: "12px",
                              color: "var(--org-primary)",
                              cursor: "pointer",
                            }}
                            title="Click to view file in this tab"
                          >
                            <span>{doc.isImage ? "🖼️" : "📄"}</span>
                            <span>{DOC_TYPE_LABEL[doc.documentType] || doc.fileName}</span>
                          </button>
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation();
                              handleDownloadFile(doc.fileUrl, doc.fileName);
                            }}
                            style={{
                              padding: "4px 8px",
                              background: "rgba(255,255,255,0.05)",
                              border: "none",
                              borderLeft: "0.5px solid var(--org-line)",
                              fontSize: "11px",
                              color: "var(--org-text-faint)",
                              cursor: "pointer",
                            }}
                            title={`Download ${doc.fileName}`}
                          >
                            ⬇️
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* In-Tab Document Preview Lightbox Modal */}
      {viewingDoc && (
        <div
          className="org-modal-backdrop"
          style={{ zIndex: 70 }}
          onClick={() => setViewingDoc(null)}
        >
          <div
            style={{
              position: "relative",
              maxWidth: "92vw",
              maxHeight: "92vh",
              width: viewingDoc.isImage ? "auto" : "900px",
              background: "var(--org-card)",
              border: "1px solid var(--org-line)",
              borderRadius: "12px",
              padding: "20px",
              display: "flex",
              flexDirection: "column",
              gap: "14px",
              boxShadow: "0 20px 40px rgba(0,0,0,0.6)",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                borderBottom: "0.5px solid var(--org-line)",
                paddingBottom: "12px",
              }}
            >
              <div>
                <h3 style={{ margin: 0, fontSize: "16px", color: "var(--org-text)" }}>
                  {DOC_TYPE_LABEL[viewingDoc.documentType] || viewingDoc.fileName}
                </h3>
                <span style={{ fontSize: "12px", color: "var(--org-text-faint)" }}>
                  {viewingDoc.fileName}
                </span>
              </div>
              <div style={{ display: "flex", gap: "8px" }}>
                <button
                  type="button"
                  className="org-button org-button--ghost"
                  style={{ height: "30px", padding: "0 10px", fontSize: "12px", display: "inline-flex", alignItems: "center", gap: "4px" }}
                  onClick={() => handleDownloadFile(viewingDoc.fileUrl, viewingDoc.fileName)}
                >
                  ⬇️ Download
                </button>
                <button
                  type="button"
                  className="org-button org-button--ghost"
                  style={{ height: "30px", padding: "0 10px", fontSize: "12px" }}
                  onClick={() => setViewingDoc(null)}
                >
                  ✕ Close
                </button>
              </div>
            </div>

            <div
              style={{
                flex: 1,
                display: "flex",
                justifyContent: "center",
                alignItems: "center",
                overflow: "hidden",
              }}
            >
              {viewingDoc.isImage || (viewingDoc.previewUrl && viewingDoc.previewUrl.includes(".jpg")) ? (
                <div style={{ textAlign: "center", width: "100%", maxHeight: "75vh", overflow: "auto" }}>
                  <img
                    src={viewingDoc.previewUrl || viewingDoc.fileUrl}
                    alt={viewingDoc.fileName}
                    style={{
                      maxWidth: "100%",
                      maxHeight: "75vh",
                      objectFit: "contain",
                      borderRadius: "8px",
                    }}
                  />
                </div>
              ) : (
                <div style={{ width: "100%", display: "flex", flexDirection: "column", gap: "8px" }}>
                  <iframe
                    src={viewingDoc.fileUrl}
                    title={viewingDoc.fileName}
                    style={{
                      width: "100%",
                      height: "70vh",
                      border: "none",
                      borderRadius: "8px",
                      background: "#fff",
                    }}
                  />
                  <div
                    style={{
                      padding: "8px 12px",
                      background: "rgba(239, 68, 68, 0.1)",
                      border: "0.5px solid rgba(239, 68, 68, 0.3)",
                      borderRadius: "6px",
                      fontSize: "12px",
                      color: "#fca5a5",
                      textAlign: "left",
                      lineHeight: "1.5",
                    }}
                  >
                    ⚠️ <strong>Note:</strong> If the browser fails to render the PDF file from Cloudinary (error <em>deny or ACL failure 401</em>), please navigate to <strong>Cloudinary Console &gt; Settings &gt; Security &gt; Enable "PDF and ZIP files delivery"</strong> and click Save.
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
