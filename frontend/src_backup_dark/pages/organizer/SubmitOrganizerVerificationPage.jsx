import React, { useEffect, useState } from "react";
import organizerVerificationApi from "../../api/organizerVerificationApi";
import OrganizerShell from "./OrganizerShell";
import { useAuth } from "../../context/AuthContext";
import "./SubmitOrganizerVerificationPage.css";

export default function SubmitOrganizerVerificationPage() {
  const { user } = useAuth();
  const [existingRequest, setExistingRequest] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [notice, setNotice] = useState(null); // { type: 'success' | 'error', message: '' }

  // Form Fields
  const [form, setForm] = useState({
    companyName: "",
    taxCode: "",
    representativeName: "",
    phoneNumber: "",
    website: "",
    experience: "",
    reason: "",
  });

  // Selected File States: { file, previewUrl, isImage }
  const [businessLicenseDoc, setBusinessLicenseDoc] = useState(null);
  const [representativeIdDoc, setRepresentativeIdDoc] = useState(null);
  const [portfolioDoc, setPortfolioDoc] = useState(null);

  useEffect(() => {
    fetchMyStatus();
  }, []);

  const fetchMyStatus = async () => {
    try {
      setLoading(true);
      const res = await organizerVerificationApi.getMyStatus();
      if (res.data?.data) {
        const req = res.data.data;
        setExistingRequest(req);
        setForm({
          companyName: req.companyName || "",
          taxCode: req.taxCode || "",
          representativeName: req.representativeName || "",
          phoneNumber: req.phoneNumber || "",
          website: req.website || "",
          experience: req.experience || "",
          reason: req.reason || "",
        });
      }
    } catch (err) {
      console.error("Failed to load verification status:", err);
    } finally {
      setLoading(false);
    }
  };

  const handleFileSelect = (file, setFileState) => {
    if (!file) {
      setFileState(null);
      return;
    }
    const isImage = file.type.startsWith("image/");
    const previewUrl = isImage ? URL.createObjectURL(file) : null;
    setFileState({ file, previewUrl, isImage });
  };

  const removeFile = (setFileState, docState) => {
    if (docState?.previewUrl) {
      URL.revokeObjectURL(docState.previewUrl);
    }
    setFileState(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.companyName.trim() || !form.taxCode.trim() || !form.representativeName.trim()) {
      setNotice({
        type: "error",
        message: "Mandatory Fields Required: Please provide Company Name, Enterprise Tax Code, and Legal Representative Name.",
      });
      return;
    }

    try {
      setSubmitting(true);
      const formData = new FormData();
      formData.append("companyName", form.companyName.trim());
      formData.append("taxCode", form.taxCode.trim());
      formData.append("representativeName", form.representativeName.trim());
      formData.append("phoneNumber", form.phoneNumber.trim());
      formData.append("website", form.website.trim());
      formData.append("experience", form.experience.trim());
      formData.append("reason", form.reason.trim());

      if (businessLicenseDoc?.file) formData.append("businessLicenseFile", businessLicenseDoc.file);
      if (representativeIdDoc?.file) formData.append("representativeIdFile", representativeIdDoc.file);
      if (portfolioDoc?.file) formData.append("portfolioFile", portfolioDoc.file);

      const res = await organizerVerificationApi.submitVerification(formData);
      if (res.data?.success) {
        setNotice({
          type: "success",
          message: res.data.message || "Organizer verification documents submitted successfully! Your application is now in Pending Review status.",
        });
        fetchMyStatus();
      }
    } catch (err) {
      setNotice({
        type: "error",
        message: "Submission failed: " + (err.response?.data?.message || err.message),
      });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <OrganizerShell title="Business Verification">
      <div className="submit-verification-container">
        <header className="verification-header">
          <div>
            <span className="badge-organizer">ORGANIZER VERIFICATION</span>
            <h1>Submit Organizer Verification & Legal Credentials</h1>
            <p className="subtitle">
              Upload legal enterprise documents (Business License, Representative ID / Passport, Experience Portfolio) securely stored on Cloudinary for platform compliance auditing and Organizer role assignment.
            </p>
          </div>
        </header>

      {notice && (
        <div className={`verification-toast-banner ${notice.type}`} style={{
          padding: "1rem 1.5rem",
          borderRadius: "10px",
          marginBottom: "1.5rem",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          background: notice.type === "success" ? "rgba(16, 185, 129, 0.15)" : "rgba(239, 68, 68, 0.15)",
          border: notice.type === "success" ? "1px solid #10b981" : "1px solid #ef4444",
          color: notice.type === "success" ? "#10b981" : "#ef4444",
          fontWeight: "600",
        }}>
          <span>{notice.message}</span>
          <button
            onClick={() => setNotice(null)}
            style={{ background: "none", border: "none", color: "inherit", cursor: "pointer", fontSize: "0.9rem", fontWeight: "600" }}
          >
            Dismiss
          </button>
        </div>
      )}

      {loading ? (
        <div className="loading-box">
          <div className="spinner"></div>
          <p>Loading organizer verification application status...</p>
        </div>
      ) : (
        <div className="verification-workspace">
          {/* Status Tracker Box if request exists */}
          {existingRequest && (
            <div className={`status-tracker-card ${existingRequest.status.toLowerCase()}`}>
              <div className="tracker-top">
                <div>
                  <h2>Application Status: {existingRequest.status.toUpperCase()}</h2>
                  <p className="tracker-sub">
                    Submitted Date: {new Date(existingRequest.createdAt).toLocaleString("en-US")}
                  </p>
                </div>
              </div>

              {existingRequest.status === "Approved" && (
                <div className="approved-notice">
                  <strong>Account Verified:</strong> Your enterprise credentials have been reviewed and approved by Platform Administrators. You have full access to Event Organizer features.
                </div>
              )}

              {existingRequest.status === "Pending" && (
                <div className="pending-notice">
                  <strong>Pending Review:</strong> Your uploaded documents are currently being audited by Platform Administrators. You will receive an automated email notification once the review is completed.
                </div>
              )}

              {existingRequest.status === "Rejected" && (
                <div className="rejected-notice">
                  <strong>Application Rejected:</strong>
                  <p className="reason-text">{existingRequest.rejectionReason || "Verification criteria not met."}</p>
                  <p className="re-submit-hint">Please review the auditor notes above, update company information or attach clearer document files below, and re-submit.</p>
                </div>
              )}
            </div>
          )}

          {/* Submission Form (UC_11.3) */}
          <div className="form-card">
            <h3>Enterprise Legal Information & Document Upload</h3>

            <form onSubmit={handleSubmit}>
              <div className="form-row-2">
                <div className="form-group">
                  <label>Company / Legal Business Name *</label>
                  <input
                    type="text"
                    required
                    className="custom-form-input"
                    value={form.companyName}
                    onChange={(e) => setForm({ ...form, companyName: e.target.value })}
                    placeholder="e.g. VietConcert Entertainment Ltd."
                  />
                </div>

                <div className="form-group">
                  <label>Enterprise Tax Identification Number (Tax Code) *</label>
                  <input
                    type="text"
                    required
                    className="custom-form-input"
                    value={form.taxCode}
                    onChange={(e) => setForm({ ...form, taxCode: e.target.value })}
                    placeholder="e.g. 0312948591"
                  />
                </div>
              </div>

              <div className="form-row-2">
                <div className="form-group">
                  <label>Legal Representative Full Name *</label>
                  <input
                    type="text"
                    required
                    className="custom-form-input"
                    value={form.representativeName}
                    onChange={(e) => setForm({ ...form, representativeName: e.target.value })}
                    placeholder="e.g. Truong Hoang Minh"
                  />
                </div>

                <div className="form-group">
                  <label>Business Contact Phone Number *</label>
                  <input
                    type="text"
                    required
                    className="custom-form-input"
                    value={form.phoneNumber}
                    onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
                    placeholder="e.g. 0393424187"
                  />
                </div>
              </div>

              <div className="form-row-2">
                <div className="form-group">
                  <label>Official Company Website / Social Link</label>
                  <input
                    type="url"
                    className="custom-form-input"
                    value={form.website}
                    onChange={(e) => setForm({ ...form, website: e.target.value })}
                    placeholder="https://vietconcert.com"
                  />
                </div>

                <div className="form-group">
                  <label>Event Management Experience (Years / Track Record)</label>
                  <input
                    type="text"
                    className="custom-form-input"
                    value={form.experience}
                    onChange={(e) => setForm({ ...form, experience: e.target.value })}
                    placeholder="e.g. 5+ years producing major live concerts >10,000 attendees"
                  />
                </div>
              </div>

              {/* Cloudinary Document Upload Section with Image Previews */}
              <div className="upload-documents-section">
                <h4>Upload Verification Documents (Cloudinary Cloud Storage)</h4>
                <p className="upload-hint">Upload high-resolution scanned images (JPG, PNG, WEBP) or PDF documents (max 10MB per file).</p>

                <div className="upload-grid">
                  {/* File 1: Business License */}
                  <div className="upload-box">
                    <span className="box-title">Business Registration License *</span>
                    {businessLicenseDoc ? (
                      <div className="file-preview-card">
                        {businessLicenseDoc.isImage ? (
                          <div className="thumb-preview-box">
                            <img src={businessLicenseDoc.previewUrl} alt="Business License Preview" className="doc-preview-img" />
                          </div>
                        ) : (
                          <div className="pdf-preview-badge">PDF File: {businessLicenseDoc.file.name}</div>
                        )}
                        <div className="file-meta">
                          <span className="file-name-text">{businessLicenseDoc.file.name}</span>
                          <button
                            type="button"
                            className="btn-remove-file"
                            onClick={() => removeFile(setBusinessLicenseDoc, businessLicenseDoc)}
                          >
                            Remove
                          </button>
                        </div>
                      </div>
                    ) : (
                      <input
                        type="file"
                        className="custom-file-input"
                        accept=".jpg,.jpeg,.png,.webp,.pdf"
                        onChange={(e) => handleFileSelect(e.target.files[0], setBusinessLicenseDoc)}
                      />
                    )}
                  </div>

                  {/* File 2: Representative ID */}
                  <div className="upload-box">
                    <span className="box-title">Legal Representative ID / Passport *</span>
                    {representativeIdDoc ? (
                      <div className="file-preview-card">
                        {representativeIdDoc.isImage ? (
                          <div className="thumb-preview-box">
                            <img src={representativeIdDoc.previewUrl} alt="ID / Passport Preview" className="doc-preview-img" />
                          </div>
                        ) : (
                          <div className="pdf-preview-badge">PDF File: {representativeIdDoc.file.name}</div>
                        )}
                        <div className="file-meta">
                          <span className="file-name-text">{representativeIdDoc.file.name}</span>
                          <button
                            type="button"
                            className="btn-remove-file"
                            onClick={() => removeFile(setRepresentativeIdDoc, representativeIdDoc)}
                          >
                            Remove
                          </button>
                        </div>
                      </div>
                    ) : (
                      <input
                        type="file"
                        className="custom-file-input"
                        accept=".jpg,.jpeg,.png,.webp,.pdf"
                        onChange={(e) => handleFileSelect(e.target.files[0], setRepresentativeIdDoc)}
                      />
                    )}
                  </div>

                  {/* File 3: Capability Portfolio */}
                  <div className="upload-box">
                    <span className="box-title">Track Record / Capability Portfolio</span>
                    {portfolioDoc ? (
                      <div className="file-preview-card">
                        {portfolioDoc.isImage ? (
                          <div className="thumb-preview-box">
                            <img src={portfolioDoc.previewUrl} alt="Portfolio Preview" className="doc-preview-img" />
                          </div>
                        ) : (
                          <div className="pdf-preview-badge">Document File: {portfolioDoc.file.name}</div>
                        )}
                        <div className="file-meta">
                          <span className="file-name-text">{portfolioDoc.file.name}</span>
                          <button
                            type="button"
                            className="btn-remove-file"
                            onClick={() => removeFile(setPortfolioDoc, portfolioDoc)}
                          >
                            Remove
                          </button>
                        </div>
                      </div>
                    ) : (
                      <input
                        type="file"
                        className="custom-file-input"
                        accept=".jpg,.jpeg,.png,.webp,.pdf,.doc,.docx"
                        onChange={(e) => handleFileSelect(e.target.files[0], setPortfolioDoc)}
                      />
                    )}
                  </div>
                </div>
              </div>

              <div className="form-actions">
                <button type="submit" className="btn-submit-verification" disabled={submitting}>
                  {submitting ? "Uploading documents to Cloudinary..." : existingRequest ? "Update & Re-submit Documents" : "Submit Verification Documents"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
      </div>
    </OrganizerShell>
  );
}
