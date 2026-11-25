import { useToast } from "../../../components/ToastProvider";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import eventApi from "../../../api/eventApi";
import { governanceApi } from "../../../api/governanceApi";
import { fileError } from "../../../utils/governanceRules";
import { formatDateRange, formatPrice, cleanReason } from "../../../utils/format";

const DOC_TYPE_LABELS = {
  EventPermit: "Event Permit (Department of Culture)",
  SafetyPlan: "Safety & Fire Prevention Plan",
  VenueContract: "Venue Rental Contract",
  CopyrightLicense: "Music Copyright License",
};

function formatDocType(type) {
  return DOC_TYPE_LABELS[type] || type;
}

export default function StepReview({ eventId, event, onRefresh, onSaved, onBack, onSubmitted }) {
  const [validation, setValidation] = useState(null);
  const [checking, setChecking] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const toast = useToast();
  const [submitted, setSubmitted] = useState(false);

  // Compliance state
  const [compliance, setCompliance] = useState(null);
  const [loadingCompliance, setLoadingCompliance] = useState(true);
  const [selectedDocType, setSelectedDocType] = useState("");
  const [docFile, setDocFile] = useState(null);
  const [fileInputKey, setFileInputKey] = useState(0);
  const [uploadingDoc, setUploadingDoc] = useState(false);
  const [docError, setDocError] = useState("");

  const canSubmit = event && ["Draft", "Rejected"].includes(event.status);

  const loadCompliance = async () => {
    if (!eventId) return;
    setLoadingCompliance(true);
    try {
      const res = await governanceApi.compliance(eventId);
      const data = res.data?.data;
      setCompliance(data);
      if (data?.requiredTypes?.length > 0 && !selectedDocType) {
        setSelectedDocType(data.requiredTypes[0]);
      }
    } catch {
      // ignore
    } finally {
      setLoadingCompliance(false);
    }
  };

  useEffect(() => {
    loadCompliance();
  }, [eventId]);

  const handleUploadDoc = async (e) => {
    e.preventDefault();
    if (!docFile || !selectedDocType) return;
    const maxBytes = compliance?.maxBytes || 20971520;
    const problem = fileError(docFile, maxBytes);
    if (problem) {
      setDocError(problem);
      toast.error(problem);
      return;
    }
    setUploadingDoc(true);
    setDocError("");
    try {
      await governanceApi.upload(eventId, selectedDocType, docFile);
      toast.success("Document uploaded successfully.");
      setDocFile(null);
      setFileInputKey((k) => k + 1);
      await loadCompliance();
      if (canSubmit) {
        await runValidation();
      }
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Failed to upload document.";
      setDocError(msg);
      toast.error(msg);
    } finally {
      setUploadingDoc(false);
    }
  };

  const handleDownloadDoc = async (doc) => {
    try {
      const res = await governanceApi.download(eventId, doc.id);
      const url = URL.createObjectURL(res.data);
      const a = document.createElement("a");
      a.href = url;
      a.download = doc.fileName;
      a.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (err) {
      toast.error(err.response?.data?.message || "Could not download document.");
    }
  };

  const currentDocs = compliance?.documents?.filter((d) => d.version === compliance.version) || [];
  const requiredTypes = compliance?.requiredTypes || [];
  const allRequiredUploaded = requiredTypes.length > 0 && requiredTypes.every((type) => currentDocs.some((d) => d.documentType === type));

  const runValidation = async () => {
    setChecking(true);
    setError("");
    try {
      const res = await eventApi.validateForSubmission(eventId);
      setValidation(res.data?.data || null);
    } catch (err) {
      setError(err.response?.data?.message || "Could not check submission eligibility.");
    } finally {
      setChecking(false);
    }
  };

  useEffect(() => {
    if (canSubmit) runValidation();
    else setChecking(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId, event?.status]);

  const handleSaveDraft = async () => {
    await onRefresh();
    onSaved();
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    setError("");
    try {
      await eventApi.submit(eventId);
      setSubmitted(true);
      toast.success("Event submitted for Platform Admin review.");
      await onSubmitted();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      const message =
        (apiErrors && apiErrors.length > 0 && apiErrors.join(" • ")) ||
        err.response?.data?.message ||
        "Failed to submit event for review.";
      setError(message);
      toast.error(
        apiErrors && apiErrors.length > 1
          ? `Cannot submit yet: ${apiErrors.length} requirements incomplete.`
          : message
      );
      // The 400 body also carries a fresh validation result - use it if present.
      const data = err.response?.data?.data;
      if (data && "errors" in data) setValidation(data);
    } finally {
      setSubmitting(false);
    }
  };

  if (submitted || event?.status === "Pending") {
    return (
      <div className="ow-step-body">
        <h2>4. Review & Submit</h2>
        <div className="ow-banner ow-banner-pending">
          <strong>Submission pending.</strong> Your event is under Platform Admin review. You will receive a notification as soon as a decision is made.
        </div>
        <Link to="/organizer/events" className="tb-btn tb-btn-primary">
          Back to Events
        </Link>
      </div>
    );
  }

  if (event?.status === "Published") {
    return (
      <div className="ow-step-body">
        <h2>4. Review & Submit</h2>
        <div className="ow-banner ow-banner-published">
          <strong>This event has been approved and is currently on sale.</strong>
        </div>
        <Link to="/organizer/events" className="tb-btn tb-btn-primary">
          Back to Events
        </Link>
      </div>
    );
  }

  const isCancelled = event?.status === "Cancelled";

  return (
    <div className="ow-step-body">
      <h2>{isCancelled ? "4. Event Overview (Cancelled)" : "4. Review & Submit"}</h2>
      {isCancelled ? (
        <div className="ow-banner ow-banner-rejected" style={{ background: "#fee2e2", borderColor: "#fca5a5", color: "#991b1b", marginBottom: "16px" }}>
          <strong>This event has been officially cancelled.</strong>
          <p style={{ margin: "4px 0 0 0" }}>
            {event.rejectedReason
              ? `Reason: ${cleanReason(event.rejectedReason)}`
              : "Ticket sales have ended and purchased tickets are in refund reconciliation."}
          </p>
        </div>
      ) : (
        <p className="ow-hint">
          Review all event details and ensure all required compliance documents are uploaded before submitting for approval.
        </p>
      )}

      {error && <div className="ow-error">{error}</div>}

      {checking && <div className="tb-loading"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>}

      {!isCancelled && !checking && validation && (
        <div
          className={
            "ow-validation " + (validation.isValid && allRequiredUploaded ? "ow-validation-ok" : "ow-validation-bad")
          }
        >
          {validation.isValid && allRequiredUploaded ? (
            <div className="ow-validation-title">Eligible for submission</div>
          ) : (
            <>
              <div className="ow-validation-title">
                {validation.errors.length + (!allRequiredUploaded ? 1 : 0)} items require completion
              </div>
              <ul>
                {validation.errors.map((e, i) => (
                  <li key={i}>{e}</li>
                ))}
                {!allRequiredUploaded && (
                  <li>All required compliance documents in the table below must be uploaded.</li>
                )}
              </ul>
            </>
          )}

          {validation.warnings?.length > 0 && (
            <div className="ow-validation-warnings">
              <div className="ow-validation-title">Notes</div>
              <ul>
                {validation.warnings.map((w, i) => (
                  <li key={i}>{w}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}

      {/* Compliance Documents Section - Redesigned clean, modern UI */}
      <div
        className="ow-compliance-section"
        style={{
          background: "#ffffff",
          border: "1px solid #e2e8f0",
          borderRadius: "14px",
          padding: "24px",
          marginBottom: "24px",
          boxShadow: "0 1px 3px rgba(0, 0, 0, 0.04)",
        }}
      >
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            marginBottom: "16px",
            flexWrap: "wrap",
            gap: "12px",
          }}
        >
          <div>
            <h3 style={{ margin: "0 0 6px", fontSize: "16px", fontWeight: 700, color: "#1e293b" }}>
              Compliance Documents
            </h3>
            <p className="ow-hint" style={{ margin: 0, fontSize: "13px", color: "#64748b" }}>
              Official event permits and fire safety documents required for platform admin approval. PDF, PNG, JPG (max {Math.round((compliance?.maxBytes || 20971520) / 1048576)} MB).
            </p>
          </div>
          {compliance && (
            <span
              style={{
                fontSize: "12px",
                fontWeight: 600,
                padding: "6px 12px",
                borderRadius: "20px",
                border: allRequiredUploaded ? "1px solid #bbf7d0" : "1px solid #fed7aa",
                background: allRequiredUploaded ? "#f0fdf4" : "#fffbeb",
                color: allRequiredUploaded ? "#15803d" : "#b45309",
              }}
            >
              {allRequiredUploaded ? "Documents Complete" : "Documents Incomplete"}
            </span>
          )}
        </div>

        {docError && <div className="ow-error" style={{ marginBottom: "16px" }}>{docError}</div>}

        {loadingCompliance ? (
          <p className="ow-hint"><span className="app-loading-spinner" role="status" aria-label="Loading" /></p>
        ) : (
          <>
            <div style={{ overflowX: "auto", border: "1px solid #f1f5f9", borderRadius: "10px", marginBottom: "18px" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "13px", textAlign: "left" }}>
                <thead>
                  <tr style={{ background: "#f8fafc", borderBottom: "1px solid #e2e8f0" }}>
                    <th style={{ padding: "12px 16px", fontWeight: 700, color: "#475569", fontSize: "12px", textTransform: "uppercase", letterSpacing: "0.04em", width: "40%" }}>
                      Required Document
                    </th>
                    <th style={{ padding: "12px 16px", fontWeight: 700, color: "#475569", fontSize: "12px", textTransform: "uppercase", letterSpacing: "0.04em", width: "45%" }}>
                      Submitted File
                    </th>
                    <th style={{ padding: "12px 16px", fontWeight: 700, color: "#475569", fontSize: "12px", textTransform: "uppercase", letterSpacing: "0.04em", width: "15%", textAlign: "right" }}>
                      Action
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {requiredTypes.map((type, idx) => {
                    const doc = currentDocs.find((d) => d.documentType === type);
                    return (
                      <tr
                        key={type}
                        style={{
                          borderBottom: idx === requiredTypes.length - 1 ? "none" : "1px solid #f1f5f9",
                          background: "#ffffff",
                        }}
                      >
                        <td style={{ padding: "14px 16px", verticalAlign: "middle" }}>
                          <span style={{ fontWeight: 600, color: "#1e293b", display: "block" }}>
                            {formatDocType(type)}
                          </span>
                        </td>
                        <td style={{ padding: "14px 16px", verticalAlign: "middle" }}>
                          {doc ? (
                            <div>
                              <span style={{ color: "#166534", fontWeight: 600, display: "block" }}>
                                {doc.fileName}
                              </span>
                              <span style={{ fontSize: "11.5px", color: "#64748b" }}>
                                Uploaded on {new Date(doc.submittedAt).toLocaleDateString("en-US")}
                              </span>
                            </div>
                          ) : (
                            <span
                              style={{
                                display: "inline-block",
                                padding: "3px 8px",
                                borderRadius: "4px",
                                background: "#fef2f2",
                                color: "#b91c1c",
                                fontSize: "12px",
                                fontWeight: 600,
                              }}
                            >
                              Missing (Required)
                            </span>
                          )}
                        </td>
                        <td style={{ padding: "14px 16px", verticalAlign: "middle", textAlign: "right" }}>
                          {doc && (
                            <button
                              type="button"
                              className="tb-btn tb-btn-outline ow-btn-sm"
                              style={{ padding: "5px 12px", fontSize: "12px", fontWeight: 600 }}
                              onClick={() => handleDownloadDoc(doc)}
                            >
                              Download
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {canSubmit && (
              <form
                onSubmit={handleUploadDoc}
                style={{
                  background: "#f8fafc",
                  border: "1px dashed #cbd5e1",
                  borderRadius: "10px",
                  padding: "16px 20px",
                  display: "flex",
                  flexWrap: "wrap",
                  gap: "16px",
                  alignItems: "flex-end",
                }}
              >
                <label className="ow-field" style={{ flex: "1 1 240px", margin: 0 }}>
                  <span style={{ fontSize: "12.5px", fontWeight: 600, color: "#334155" }}>Document type</span>
                  <select
                    value={selectedDocType}
                    onChange={(e) => setSelectedDocType(e.target.value)}
                    style={{ fontSize: "13px", height: "40px", background: "#ffffff", borderColor: "#cbd5e1" }}
                  >
                    {requiredTypes.map((t) => (
                      <option key={t} value={t}>
                        {formatDocType(t)}
                      </option>
                    ))}
                  </select>
                </label>

                <div style={{ flex: "2 1 280px", margin: 0 }}>
                  <span style={{ display: "block", fontSize: "12.5px", fontWeight: 600, color: "#334155", marginBottom: "6px" }}>
                    Choose file (.pdf, .png, .jpg)
                  </span>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "10px",
                      background: "#ffffff",
                      border: "1px solid #cbd5e1",
                      borderRadius: "8px",
                      padding: "4px 10px",
                      minHeight: "40px",
                    }}
                  >
                    <label
                      htmlFor="step-review-doc-file"
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        justifyContent: "center",
                        padding: "6px 14px",
                        borderRadius: "6px",
                        background: "#f1f5f9",
                        border: "1px solid #cbd5e1",
                        color: "#1e293b",
                        fontSize: "12.5px",
                        fontWeight: 600,
                        cursor: "pointer",
                        whiteSpace: "nowrap",
                        userSelect: "none",
                      }}
                    >
                      Choose file
                    </label>
                    <span
                      style={{
                        fontSize: "13px",
                        color: docFile ? "#0f172a" : "#64748b",
                        fontWeight: docFile ? 600 : 400,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        flex: 1,
                      }}
                    >
                      {docFile ? docFile.name : "No file chosen"}
                    </span>
                    <input
                      id="step-review-doc-file"
                      key={fileInputKey}
                      type="file"
                      accept=".pdf,.png,.jpg,.jpeg"
                      onChange={(e) => setDocFile(e.target.files?.[0] || null)}
                      style={{ display: "none" }}
                    />
                  </div>
                </div>

                <button
                  type="submit"
                  className="tb-btn tb-btn-primary"
                  disabled={uploadingDoc || !docFile}
                  style={{ height: "40px", whiteSpace: "nowrap", padding: "0 20px", fontWeight: 600 }}
                >
                  {uploadingDoc ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Upload document"}
                </button>
              </form>
            )}
          </>
        )}
      </div>

      {event && (
        <div className="ow-review-summary">
          <div className="ow-review-row">
            {event.posterUrl && (
              <img className="ow-review-poster" src={event.posterUrl} alt="" />
            )}
            <div>
              <h3>{event.title}</h3>
              {event.shortDescription && <p>{event.shortDescription}</p>}
              <p className="ow-hint">
                {formatDateRange(event.startsAt, event.endsAt)}
              </p>
              <p className="ow-hint">
                {[event.locationName, event.address, event.city]
                  .filter(Boolean)
                  .join(", ") || "Venue not set"}
              </p>
            </div>
          </div>

          {event.ticketTypes?.length > 0 && (
            <table className="ow-table">
              <thead>
                <tr>
                  <th>Ticket Type</th>
                  <th>Price</th>
                  <th>Quantity</th>
                </tr>
              </thead>
              <tbody>
                {event.ticketTypes.map((t) => (
                  <tr key={t.ticketTypeId}>
                    <td>{t.typeName}</td>
                    <td>{formatPrice(t.price)}</td>
                    <td>{t.quantity}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {event.seatingChart && (
            <p className="ow-hint">
              Seating layout: {event.seatingChart.zones?.length || 0} zones.
            </p>
          )}

          {event.refundPolicies?.length > 0 && (
            <p className="ow-hint">
              Refund policies: {event.refundPolicies.length} rules configured.
            </p>
          )}
        </div>
      )}

      {isCancelled ? (
        <div className="ow-actions">
          <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
            Back
          </button>
          <Link to="/organizer/events" className="tb-btn tb-btn-primary">
            Back to Events
          </Link>
        </div>
      ) : (
        <div className="ow-actions">
          <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
            Back
          </button>
          <button type="button" className="tb-btn tb-btn-outline" onClick={handleSaveDraft}>
            Save Draft
          </button>
          <button
            type="button"
            className="tb-btn tb-btn-primary"
            onClick={handleSubmit}
            disabled={submitting || checking || (validation && !validation.isValid) || !allRequiredUploaded}
          >
            {submitting ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Submit for Review"}
          </button>
        </div>
      )}
    </div>
  );
}
