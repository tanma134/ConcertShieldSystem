import React, { useEffect, useState } from "react";
import policyApi from "../api/policyApi";

const DEFAULT_POLICIES = [
  {
    category: "TERMS_OF_SERVICE",
    title: "Platform Terms of Service (TOS)",
    version: "v1.2",
    effectiveDate: "2026-10-01",
    scope: "All Platform Attendees & Event Organizers",
    icon: "📜",
    summary:
      "Legally binding conditions governing the use of ConcertShield. Users agree to provide verified identity, abide by venue safety regulations, and secure their account credentials.",
  },
  {
    category: "PRICE_TICKETING",
    title: "Official Pricing & Anti-Scalping Policy",
    version: "v2.1",
    effectiveDate: "2026-09-15",
    scope: "Ticket Buyers & Secondary Market",
    icon: "🏷️",
    summary:
      "Strict prohibition against secondary ticket scalping and unauthorized price markups. Each verified account is subject to per-event purchase caps to guarantee fair access.",
  },
  {
    category: "REFUND_CANCELLATION",
    title: "Attendee Refund & Cancellation Protection",
    version: "v1.0",
    effectiveDate: "2026-08-20",
    scope: "All Valid Orders on ConcertShield",
    icon: "🔄",
    summary:
      "100% refund guarantee if an event is officially Cancelled or Postponed due to force majeure. Refunds are automatically reconciled and routed to original payment methods.",
  },
  {
    category: "RISK_ANTI_FRAUD",
    title: "Anti-Scalping & Rotating Dynamic QR Standards",
    version: "v1.3",
    effectiveDate: "2026-10-10",
    scope: "Check-in Gates & Digital Tickets",
    icon: "🛡️",
    summary:
      "Digital tickets utilize encrypted, continuously rotating dynamic QR codes with HMAC signatures. Static screenshots, unauthorized transfers, or counterfeit credentials will be rejected at gates.",
  },
  {
    category: "VIRTUAL_QUEUE",
    title: "Virtual Waiting Room & Fair Queue Reservation",
    version: "v1.1",
    effectiveDate: "2026-07-01",
    scope: "High-Demand Event Onsales",
    icon: "⏳",
    summary:
      "Fair randomized queue positioning during high-traffic onsales to prevent bot attacks and guarantee equal buying opportunities for real fans.",
  },
];

// Helper to merge fetched active policies with base default policies so all 5 categories remain visible
function mergeWithDefaults(activeList) {
  if (!activeList || activeList.length === 0) return DEFAULT_POLICIES;

  const merged = DEFAULT_POLICIES.map((base) => {
    const found = activeList.find(
      (m) =>
        m.category &&
        (m.category.toUpperCase() === base.category.toUpperCase() ||
          m.category.toLowerCase().replace(/_/g, "") === base.category.toLowerCase().replace(/_/g, ""))
    );
    if (found) {
      return {
        ...base,
        title: found.title || base.title,
        version: found.version || base.version,
        effectiveDate: found.effectiveDate || base.effectiveDate,
        scope: found.scope || base.scope,
        summary: found.summary || base.summary,
      };
    }
    return base;
  });

  // Also include any custom policy categories not in the 5 defaults
  activeList.forEach((m) => {
    const exists = DEFAULT_POLICIES.some(
      (base) =>
        m.category &&
        (m.category.toUpperCase() === base.category.toUpperCase() ||
          m.category.toLowerCase().replace(/_/g, "") === base.category.toLowerCase().replace(/_/g, ""))
    );
    if (!exists) {
      merged.push(m);
    }
  });

  return merged;
}

export default function PlatformPolicyModal({ isOpen, onClose, onAgree }) {
  const [policies, setPolicies] = useState(() => {
    try {
      const cached = localStorage.getItem("cs_cached_policies");
      if (cached) {
        const parsed = JSON.parse(cached);
        return mergeWithDefaults(parsed);
      }
      return DEFAULT_POLICIES;
    } catch {
      return DEFAULT_POLICIES;
    }
  });
  const [loading, setLoading] = useState(false);

  const fetchActive = () => {
    setLoading(true);
    policyApi
      .getActivePolicies()
      .then((res) => {
        const raw = res.data?.data || res.data;
        const list = Array.isArray(raw)
          ? raw
          : raw && typeof raw === "object"
          ? Object.values(raw)
          : [];

        if (list.length > 0) {
          const mapped = list.map((p) => ({
            category: p.category || p.Category,
            title: p.title || p.Title || p.category,
            version: p.versionCode || p.versionTag || `v${p.versionNumber || "1.0"}`,
            effectiveDate: (p.effectiveDate || p.createdAt || "").slice(0, 10),
            scope: p.scope || p.Scope || "All Platform Attendees & Organizers",
            icon:
              p.category?.includes("PRICE") ? "🏷️" :
              p.category?.includes("REFUND") ? "🔄" :
              p.category?.includes("QUEUE") ? "⏳" :
              p.category?.includes("RISK") ? "🛡️" : "📜",
            summary: p.changeSummary || p.content || p.summary || "Latest active platform governance policy version.",
          }));

          const merged = mergeWithDefaults(mapped);
          setPolicies(merged);
          try {
            localStorage.setItem("cs_cached_policies", JSON.stringify(mapped));
          } catch (e) {}
        }
      })
      .catch(() => {
        try {
          const cached = localStorage.getItem("cs_cached_policies");
          if (cached) setPolicies(mergeWithDefaults(JSON.parse(cached)));
          else setPolicies(DEFAULT_POLICIES);
        } catch {
          setPolicies(DEFAULT_POLICIES);
        }
      })
      .finally(() => setLoading(false));
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
      <div className="policy-modal-card" onClick={(e) => e.stopPropagation()}>
        <div className="policy-modal-header">
          <h3>
            <span>📜</span> Platform Policies & Terms of Service
          </h3>
          <button className="policy-modal-close" onClick={onClose} title="Close">
            ✕
          </button>
        </div>

        <div className="policy-modal-body">
          <p style={{ margin: "0 0 16px 0", color: "#94a3b8", fontSize: "0.88rem", lineHeight: 1.5 }}>
            Please review the active platform policies and terms governing ConcertShield. Continuing to register or use our services constitutes acceptance of these terms and conditions.
          </p>

          {loading ? (
            <div style={{ textAlign: "center", padding: "24px", color: "#94a3b8" }}>
              Loading active policy versions...
            </div>
          ) : (
            policies.map((p, idx) => (
              <div key={idx} className="policy-category-block">
                <div className="policy-category-header">
                  <span className="policy-cat-name">
                    {p.icon} {p.title}
                  </span>
                  <span className="policy-version-badge">{p.version} Active</span>
                </div>
                <div style={{ fontSize: "0.78rem", color: "#64748b", marginBottom: "6px" }}>
                  Effective: {p.effectiveDate} · Scope: {p.scope}
                </div>
                <p className="policy-cat-desc">{p.summary}</p>
              </div>
            ))
          )}
        </div>

        <div className="policy-modal-footer">
          <button
            type="button"
            className="tb-btn tb-btn-outline"
            onClick={onClose}
            style={{ padding: "8px 16px", fontSize: "0.85rem", color: "#94a3b8", borderColor: "#334155" }}
          >
            Close
          </button>
          {onAgree && (
            <button
              type="button"
              className="btn-policy-agree"
              onClick={() => {
                onAgree();
                onClose();
              }}
            >
              ✓ I have read and agree to platform policies
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
