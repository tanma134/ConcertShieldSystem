import React, { useEffect, useState } from "react";
import policyApi from "../api/policyApi";

const DEFAULT_PLATFORM_POLICY = {
  title: "ConcertShield Platform Policy & Terms of Service",
  version: "POL-PRICE-V1.0",
  effectiveDate: new Date().toISOString(),
  scope: "All Platform Attendees & Event Organizers",
  platformFeePercent: 5.0,
  payoutDays: 7,
  summary:
    "Official ticketing governance, anti-scalping standards, and financial settlement terms. ConcertShield guarantees 100% refund protection for cancelled events, transparent platform fee deductions, and scheduled payouts for verified organizers.",
  content: `ARTICLE 1. OBJECTIVES AND APPLICABLE SCOPE\n1. This platform policy establishes binding operational, ticketing, and financial standards across ConcertShield.\n\nARTICLE 2. OPERATIONAL REQUIREMENTS AND COMPLIANCE MANDATES\n1. All participants must adhere strictly to platform regulations regarding ticket issuance, platform fee deductions (5.0%), and payout schedules (7 business days).\n\nARTICLE 3. EFFECTIVE DATE AND ENFORCEMENT\n1. This policy version takes legal effect immediately upon publication on the system.`,
};

export default function PlatformPolicyModal({ isOpen, onClose, onAgree, onDecline, requireConsent = false }) {
  const [activePolicy, setActivePolicy] = useState(DEFAULT_PLATFORM_POLICY);
  const [loading, setLoading] = useState(false);
  const [agreedCheckbox, setAgreedCheckbox] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setAgreedCheckbox(false);
    }
  }, [isOpen]);

  const formatEffectiveDate = (dateVal) => {
    if (!dateVal) return "—";
    const d = new Date(dateVal);
    if (isNaN(d.getTime())) return String(dateVal);
    return d.toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const fetchActive = async () => {
    setLoading(true);
    try {
      const res = await policyApi.getPricingTerms();
      if (res?.data?.data) {
        const p = res.data.data;
        setActivePolicy({
          title: p.title || "ConcertShield Platform Policy & Terms of Service",
          version: p.versionCode || "POL-PRICE-V1.0",
          effectiveDate: p.effectiveDate || p.createdAt || new Date().toISOString(),
          scope: p.scope || "All Platform Attendees & Event Organizers",
          platformFeePercent: p.platformFeePercent ?? 5.0,
          payoutDays: p.payoutDays ?? 7,
          summary: p.changeSummary || "",
          content: p.content || "",
        });
      }
    } catch {
      setActivePolicy(DEFAULT_PLATFORM_POLICY);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchActive();
    }
  }, [isOpen]);

  useEffect(() => {
    const handleUpdated = () => fetchActive();
    window.addEventListener("policy-updated", handleUpdated);
    return () => window.removeEventListener("policy-updated", handleUpdated);
  }, []);

  if (!isOpen) return null;

  return (
    <div className="policy-modal-overlay" onClick={onClose}>
      <div className="policy-modal-card" onClick={(e) => e.stopPropagation()} style={{ maxWidth: "720px", width: "95%" }}>
        <div className="policy-modal-header">
          <h3>
            Platform Policies & Terms of Service
          </h3>
          <button className="policy-modal-close" onClick={onClose} title="Close">
            ✕
          </button>
        </div>

        <div className="policy-modal-body">
          {requireConsent && (
            <div style={{
              background: "#fff1f2",
              border: "1.5px solid #fecdd3",
              borderRadius: "10px",
              padding: "14px 18px",
              marginBottom: "16px",
              display: "flex",
              alignItems: "flex-start",
              gap: "12px",
              boxShadow: "0 2px 8px rgba(225, 29, 72, 0.08)"
            }}>
              <span style={{ fontSize: "1.35rem", lineHeight: 1 }}>⚠️</span>
              <div>
                <strong style={{ color: "#9f1239", fontSize: "0.95rem", display: "block", marginBottom: "3px" }}>
                  Updated Terms of Service & Platform Policies
                </strong>
                <p style={{ margin: 0, color: "#881337", fontSize: "0.85rem", lineHeight: 1.45 }}>
                  Platform policies have been updated to <strong>{activePolicy.version}</strong>. Please review and accept the latest terms and conditions before continuing to log in.
                </p>
              </div>
            </div>
          )}

          <p style={{ margin: "0 0 16px 0", color: "#111827", fontSize: "0.88rem", lineHeight: 1.5 }}>
            Please review the active platform governance policies, financial settlement rules, and terms of service governing ConcertShield.
          </p>

          {loading ? (
            <div style={{ textAlign: "center", padding: "24px", color: "#111827" }}>
              <span className="app-loading-spinner" role="status" aria-label="Loading" />
            </div>
          ) : (
            <div className="policy-category-block" style={{ padding: "18px", borderRadius: "10px", background: "#faf5ff", border: "1px solid #e9d5ff" }}>
              {/* Header Row */}
              <div className="policy-category-header" style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "10px", marginBottom: "12px" }}>
                <div>
                  <span className="policy-cat-name" style={{ fontSize: "1.15rem", fontWeight: 700, color: "#111827", display: "block" }}>
                    {activePolicy.title}
                  </span>
                  <span style={{ fontSize: "0.82rem", color: "#111827", marginTop: "2px", display: "block" }}>
                    Applicable Scope: <strong style={{ color: "#111827" }}>{activePolicy.scope}</strong>
                  </span>
                </div>
                <span className="policy-version-badge" style={{ background: "#ec4899", color: "#fff", padding: "3px 12px", borderRadius: "999px", fontSize: "0.78rem", fontWeight: 700, whiteSpace: "nowrap" }}>
                  {activePolicy.version} · Active In-Force
                </span>
              </div>

              {/* Key Metrics Row */}
              <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: "10px", marginBottom: "14px" }}>
                <div style={{ background: "#eff6ff", border: "1px solid #bfdbfe", padding: "8px 12px", borderRadius: "8px" }}>
                  <span style={{ fontSize: "0.75rem", color: "#111827", textTransform: "uppercase", fontWeight: 700, display: "block" }}>Effective Start Date</span>
                  <strong style={{ color: "#111827", fontSize: "0.9rem" }}>{formatEffectiveDate(activePolicy.effectiveDate)}</strong>
                </div>

                <div style={{ background: "#fdf2f8", border: "1px solid #fbcfe8", padding: "8px 12px", borderRadius: "8px" }}>
                  <span style={{ fontSize: "0.75rem", color: "#111827", textTransform: "uppercase", fontWeight: 700, display: "block" }}>Platform Service Fee</span>
                  <strong style={{ color: "#111827", fontSize: "0.9rem" }}>{activePolicy.platformFeePercent}%</strong>
                </div>

                <div style={{ background: "#f5f3ff", border: "1px solid #ddd6fe", padding: "8px 12px", borderRadius: "8px" }}>
                  <span style={{ fontSize: "0.75rem", color: "#111827", textTransform: "uppercase", fontWeight: 700, display: "block" }}>Payout Settlement</span>
                  <strong style={{ color: "#111827", fontSize: "0.9rem" }}>{activePolicy.payoutDays} Business Days</strong>
                </div>
              </div>

              {/* Enactment Reason / Change Summary */}
              {activePolicy.summary && (
                <div style={{ background: "#ffffff", border: "1px solid #e9d5ff", borderRadius: "8px", padding: "12px 14px", marginBottom: "14px" }}>
                  <strong style={{ color: "#111827", fontSize: "0.85rem", display: "block", marginBottom: "4px" }}>
                    Enactment Reason & Summary of Changes:
                  </strong>
                  <p style={{ margin: 0, color: "#111827", fontSize: "0.88rem", lineHeight: 1.5 }}>
                    {activePolicy.summary}
                  </p>
                </div>
              )}

              {/* Full Articles & Provisions */}
              {activePolicy.content && (
                <div style={{ marginTop: "14px", borderTop: "1px solid #e9d5ff", paddingTop: "14px" }}>
                  <strong style={{ color: "#111827", fontSize: "0.92rem", display: "block", marginBottom: "8px" }}>
                    Full Policy Provisions & Articles:
                  </strong>
                  <div style={{
                    maxHeight: "260px",
                    overflowY: "auto",
                    background: "#ffffff",
                    padding: "14px",
                    borderRadius: "8px",
                    border: "1px solid #d8b4fe",
                    fontSize: "0.86rem",
                    color: "#111827",
                    lineHeight: 1.7,
                    whiteSpace: "pre-wrap",
                    fontFamily: "inherit"
                  }}>
                    {activePolicy.content}
                  </div>
                </div>
              )}

              {/* Platform Commitments */}
              <div style={{ borderTop: "1px solid #e9d5ff", paddingTop: "12px", marginTop: "14px", fontSize: "0.82rem", color: "#111827" }}>
                <strong style={{ color: "#111827", display: "block", marginBottom: "6px" }}>Platform Commitments:</strong>
                <ul style={{ margin: 0, paddingLeft: "18px", lineHeight: 1.5 }}>
                  <li>Guaranteed 100% attendee refunds if an event is officially cancelled or rescheduled.</li>
                  <li>Real-time dynamic rotating QR codes to eliminate secondary ticket scalping.</li>
                  <li>Transparent escrow-protected organizer payouts following post-event reconciliation.</li>
                </ul>
              </div>
            </div>
          )}
        </div>

        <div className="policy-modal-footer" style={{ display: "flex", flexDirection: requireConsent ? "column" : "row", gap: "12px", alignItems: requireConsent ? "stretch" : "center", justifyContent: "space-between" }}>
          {requireConsent && (
            <label style={{ display: "flex", alignItems: "center", gap: "10px", cursor: "pointer", fontSize: "0.88rem", color: "#111827", fontWeight: 600, background: "#f8fafc", padding: "10px 14px", borderRadius: "8px", border: "1px solid #e2e8f0" }}>
              <input
                type="checkbox"
                checked={agreedCheckbox}
                onChange={(e) => setAgreedCheckbox(e.target.checked)}
                style={{ width: "16px", height: "16px", accentColor: "#6366f1", cursor: "pointer" }}
              />
              <span>I have read, understood, and agree to the updated Platform Policies & Terms of Service ({activePolicy.version}).</span>
            </label>
          )}

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px", width: "100%" }}>
            <button
              type="button"
              className="tb-btn tb-btn-outline"
              onClick={() => {
                if (requireConsent && onDecline) {
                  onDecline();
                } else {
                  onClose();
                }
              }}
              style={{ padding: "8px 18px", fontSize: "0.85rem", color: "#111827", borderColor: "#9ca3af", background: "#fff" }}
            >
              {requireConsent ? "Decline & Cancel" : "Close"}
            </button>
            <button
              className="policy-btn-agree"
              disabled={requireConsent && !agreedCheckbox}
              onClick={() => {
                if (onAgree) onAgree(activePolicy);
                onClose();
              }}
              style={{
                opacity: requireConsent && !agreedCheckbox ? 0.6 : 1,
                cursor: requireConsent && !agreedCheckbox ? "not-allowed" : "pointer"
              }}
            >
              {requireConsent ? "Accept & Continue Login" : "I Understand & Accept Platform Policies"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
