import React, { useEffect, useState, useCallback } from "react";
import policyApi from "../../api/policyApi";
import AdminShell from "./AdminShell";
import "./SystemPoliciesPage.css";

const CATEGORIES = [
  {
    id: "PRICE_TICKETING",
    name: "Pricing & Ticketing Policy",
    icon: "🏷️",
    desc: "Regulations on official ticket pricing, platform service fees, per-order limits, and anti-scalping mechanisms.",
  },
  {
    id: "REFUND_CANCELLATION",
    name: "Refund & Event Cancellation",
    icon: "🔄",
    desc: "Standards for 100% attendee refunds upon cancellation, refund processing timelines, and purchase protection.",
  },
  {
    id: "VIRTUAL_QUEUE",
    name: "Virtual Queue & Reservation",
    icon: "⏳",
    desc: "Fair randomized queue number allocation, waiting room throughput, and seat reservation hold timeouts.",
  },
  {
    id: "RISK_ANTI_FRAUD",
    name: "Anti-Scalping & Dynamic QR Protection",
    icon: "🛡️",
    desc: "Dynamic rotating QR security mandates, screenshot anti-fraud measures, and secondary market account blacklisting.",
  },
  {
    id: "TERMS_OF_SERVICE",
    name: "Platform Terms of Service (TOS)",
    icon: "📜",
    desc: "Binding user agreements, legal responsibilities, and dispute resolution for Organizers, Attendees, and Platform.",
  },
];

export default function SystemPoliciesPage() {
  const [selectedCategory, setSelectedCategory] = useState("PRICE_TICKETING");
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [successMsg, setSuccessMsg] = useState(null);

  // Modals state
  const [readingDoc, setReadingDoc] = useState(null);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Form State for creating a new version
  const [createForm, setCreateForm] = useState({
    title: "",
    scope: "All Platform Attendees & Event Organizers",
    changeSummary: "",
    content: "",
    effectiveDate: new Date().toISOString().slice(0, 16),
  });

  const loadHistory = useCallback(async (category) => {
    try {
      setLoading(true);
      setError(null);
      const res = await policyApi.getPolicyHistory(category);
      if (res?.data?.data) {
        setHistory(res.data.data);
      } else {
        setHistory([]);
      }
    } catch (err) {
      console.error("Failed to load policy versions:", err);
      setError("Could not connect to database to retrieve policy version records.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadHistory(selectedCategory);
  }, [selectedCategory, loadHistory]);

  const activePolicy = history.find((p) => p.status === "Active") || history[0];

  const handleOpenCreateModal = () => {
    const defaultTemplate = activePolicy
      ? `ARTICLE 1. OBJECTIVES AND APPLICABLE SCOPE\n1. This policy update establishes binding standards across the platform to ensure full transparency and consumer protection.\n\nARTICLE 2. OPERATIONAL REQUIREMENTS AND COMPLIANCE MANDATES\n1. All participants must adhere strictly to platform regulations regarding ticket issuance and dispute management.\n\nARTICLE 3. EFFECTIVE DATE AND ENFORCEMENT\n1. This policy version takes legal effect immediately upon publication on the system.`
      : "";

    setCreateForm({
      title: activePolicy ? `Update ${activePolicy.title}` : `Policy for ${selectedCategory}`,
      scope: activePolicy?.scope || "All Platform Attendees & Event Organizers",
      changeSummary: "",
      content: activePolicy?.content || defaultTemplate,
      effectiveDate: new Date().toISOString().slice(0, 16),
    });
    setShowCreateModal(true);
  };

  const handlePublishPolicy = async (e) => {
    e.preventDefault();
    if (!createForm.title.trim()) {
      setError("Please provide a policy document title.");
      return;
    }
    if (!createForm.changeSummary.trim()) {
      setError("Please provide an enactment justification / change summary.");
      return;
    }
    if (!createForm.content.trim()) {
      setError("Please provide the full policy text provisions.");
      return;
    }

    try {
      setSubmitting(true);
      const payload = {
        category: selectedCategory,
        title: createForm.title.trim(),
        scope: createForm.scope.trim(),
        changeSummary: createForm.changeSummary.trim(),
        content: createForm.content.trim(),
        effectiveDate: new Date(createForm.effectiveDate).toISOString(),
      };

      const res = await policyApi.createPolicyVersion(payload);
      if (res?.data?.success) {
        setShowCreateModal(false);
        setSuccessMsg(res.data.message || "New policy version published successfully!");
        setTimeout(() => setSuccessMsg(null), 5000);
        await loadHistory(selectedCategory);
        window.dispatchEvent(new Event("policy-updated"));
      }
    } catch (err) {
      console.error("Publish policy error:", err);
      setError("Failed to persist policy version to database: " + (err.response?.data?.message || err.message));
    } finally {
      setSubmitting(false);
    }
  };

  const currentCatObj = CATEGORIES.find((c) => c.id === selectedCategory) || CATEGORIES[0];

  return (
    <AdminShell title="Platform Governance & Policy Versioning">
      <div className="policy-gov-container">
        {/* Header Banner */}
        <header className="policy-gov-header">
          <div className="header-info">
            <span className="policy-top-badge">⚖️ GOVERNANCE & POLICY VERSIONING</span>
            <h1>Platform Policy Governance & Version Archive</h1>
            <p className="subtitle">
              Immutable platform archive and lifecycle management for ticketing policies, financial rules, and compliance standards.
              Every version is queried directly from the database with complete audit logging.
            </p>
          </div>
          <button className="btn-publish-new" onClick={handleOpenCreateModal}>
            📢 Enact New Policy Version
          </button>
        </header>

        {successMsg && (
          <div className="policy-toast success">
            <span>✅ {successMsg}</span>
            <button onClick={() => setSuccessMsg(null)}>✕</button>
          </div>
        )}

        {error && (
          <div className="policy-toast error">
            <span>⚠️ {error}</span>
            <button onClick={() => setError(null)}>✕</button>
          </div>
        )}

        {/* Category Tabs */}
        <div className="policy-category-tabs">
          {CATEGORIES.map((cat) => (
            <button
              key={cat.id}
              className={`cat-tab-btn ${selectedCategory === cat.id ? "active" : ""}`}
              onClick={() => setSelectedCategory(cat.id)}
            >
              <span className="cat-icon">{cat.icon}</span>
              <div className="cat-text">
                <strong>{cat.name}</strong>
              </div>
            </button>
          ))}
        </div>

        {/* Current Active Policy Snapshot Card */}
        {activePolicy && (
          <div className="active-policy-spotlight">
            <div className="spotlight-badge-row">
              <span className="badge-active-version">
                🟢 Active In-Force Version: {activePolicy.versionCode} (v{activePolicy.versionNumber})
              </span>
              <span className="badge-effective-date">
                📅 Effective from: {new Date(activePolicy.effectiveDate).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" })}
              </span>
            </div>

            <h2 className="spotlight-title">{activePolicy.title}</h2>
            <p className="spotlight-scope">
              <strong>Applicable Scope:</strong> {activePolicy.scope}
            </p>
            <div className="spotlight-summary">
              <strong>Enactment Summary / Revision Justification:</strong> {activePolicy.changeSummary}
            </div>

            <div className="spotlight-actions">
              <button
                className="btn-read-doc"
                onClick={() => setReadingDoc(activePolicy)}
              >
                📖 Read Full Policy Document
              </button>
            </div>
          </div>
        )}

        {/* Version History Table */}
        <section className="policy-history-section">
          <div className="history-section-header">
            <div>
              <h3>📋 Enacted Version History ({currentCatObj.name})</h3>
              <p>{currentCatObj.desc}</p>
            </div>
            <button
              className="btn-refresh-history"
              onClick={() => loadHistory(selectedCategory)}
              disabled={loading}
            >
              {loading ? "🔄 Querying..." : "🔄 Refresh Data"}
            </button>
          </div>

          <div className="policy-table-wrapper">
            <table className="policy-table">
              <thead>
                <tr>
                  <th>Version Code</th>
                  <th>Policy Document Title</th>
                  <th>Applicable Scope</th>
                  <th>Summary of Changes</th>
                  <th>Effective Date</th>
                  <th>Enacted By</th>
                  <th>Status</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan="8" className="table-loading">
                      Querying policy records directly from database...
                    </td>
                  </tr>
                ) : history.length === 0 ? (
                  <tr>
                    <td colSpan="8" className="table-empty">
                      No policy documents have been published for this category yet.
                    </td>
                  </tr>
                ) : (
                  history.map((item) => (
                    <tr key={item.policyId} className={item.status === "Active" ? "row-active" : ""}>
                      <td>
                        <span className="version-tag">{item.versionCode}</span>
                        <div className="sub-date">
                          {new Date(item.createdAt).toLocaleDateString("en-US")}
                        </div>
                      </td>
                      <td className="policy-title-cell">
                        <strong>{item.title}</strong>
                      </td>
                      <td>
                        <span className="scope-tag">{item.scope}</span>
                      </td>
                      <td className="summary-cell">
                        <p>{item.changeSummary}</p>
                      </td>
                      <td>
                        {new Date(item.effectiveDate).toLocaleDateString("en-US")}
                      </td>
                      <td>
                        <span className="admin-name">{item.createdByAdminName}</span>
                      </td>
                      <td>
                        <span className={`status-pill ${item.status.toLowerCase()}`}>
                          {item.status === "Active" ? "🟢 Active In-Force" : "⚪ Archived"}
                        </span>
                      </td>
                      <td>
                        <button
                          className="btn-action-read"
                          onClick={() => setReadingDoc(item)}
                        >
                          📖 Read Document
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>

        {/* MODAL 1: Reading Full Policy Document */}
        {readingDoc && (
          <div className="policy-modal-overlay" onClick={() => setReadingDoc(null)}>
            <div className="policy-doc-modal" onClick={(e) => e.stopPropagation()}>
              <header className="doc-modal-header">
                <div>
                  <div className="modal-top-tags">
                    <span className="doc-ver-code">{readingDoc.versionCode}</span>
                    <span className={`doc-status-tag ${readingDoc.status.toLowerCase()}`}>
                      {readingDoc.status === "Active" ? "Active In-Force" : "Archived Version"}
                    </span>
                  </div>
                  <h2>{readingDoc.title}</h2>
                  <div className="doc-meta-row">
                    <span>📅 Effective: <strong>{new Date(readingDoc.effectiveDate).toLocaleDateString("en-US")}</strong></span>
                    <span>👤 Enacted By: <strong>{readingDoc.createdByAdminName}</strong></span>
                    <span>🕒 Date Signed: <strong>{new Date(readingDoc.createdAt).toLocaleString("en-US")}</strong></span>
                  </div>
                </div>
                <button className="btn-close-doc" onClick={() => setReadingDoc(null)}>✕</button>
              </header>

              <div className="doc-modal-body">
                <div className="doc-summary-box">
                  <h4>📌 Scope & Purpose of Enactment:</h4>
                  <p><strong>Target Audience:</strong> {readingDoc.scope}</p>
                  <p><strong>Revision Justification:</strong> {readingDoc.changeSummary}</p>
                </div>

                <div className="doc-full-content">
                  <h3>📜 Full Policy Provisions & Articles:</h3>
                  <div className="content-rendered">
                    {readingDoc.content.split("\n\n").map((para, idx) => (
                      <p key={idx}>{para}</p>
                    ))}
                  </div>
                </div>
              </div>

              <footer className="doc-modal-footer">
                <span className="doc-hash-audit">
                  🛡️ Immutable audit record from database (Policy ID #{readingDoc.policyId})
                </span>
                <button className="btn-done-read" onClick={() => setReadingDoc(null)}>
                  Close Document
                </button>
              </footer>
            </div>
          </div>
        )}

        {/* MODAL 2: Create / Publish New Policy Version */}
        {showCreateModal && (
          <div className="policy-modal-overlay">
            <div className="policy-create-modal" onClick={(e) => e.stopPropagation()}>
              <header className="create-modal-header">
                <div>
                  <h2>📢 Enact & Publish New Policy Version</h2>
                  <p>Category: <strong>{currentCatObj.name}</strong></p>
                </div>
                <button className="btn-close-doc" onClick={() => setShowCreateModal(false)}>✕</button>
              </header>

              <form onSubmit={handlePublishPolicy}>
                <div className="create-modal-body">
                  <div className="form-group">
                    <label>Policy Document Title (*):</label>
                    <input
                      type="text"
                      className="form-input"
                      placeholder="e.g. Fair Pricing Mechanism & Platform Surcharge Regulation 2026..."
                      value={createForm.title}
                      onChange={(e) => setCreateForm({ ...createForm, title: e.target.value })}
                      required
                    />
                  </div>

                  <div className="form-row-2">
                    <div className="form-group">
                      <label>Applicable Scope (*):</label>
                      <input
                        type="text"
                        className="form-input"
                        placeholder="e.g. All Platform Attendees & Event Organizers..."
                        value={createForm.scope}
                        onChange={(e) => setCreateForm({ ...createForm, scope: e.target.value })}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label>Effective Start Date (*):</label>
                      <input
                        type="datetime-local"
                        className="form-input"
                        value={createForm.effectiveDate}
                        onChange={(e) => setCreateForm({ ...createForm, effectiveDate: e.target.value })}
                        required
                      />
                    </div>
                  </div>

                  <div className="form-group">
                    <label>Enactment Reason / Summary of Changes (*):</label>
                    <textarea
                      rows={3}
                      className="form-textarea"
                      placeholder="Describe the reasons for updating this policy version compared to previous versions..."
                      value={createForm.changeSummary}
                      onChange={(e) => setCreateForm({ ...createForm, changeSummary: e.target.value })}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Full Policy Provisions & Terms (Articles) (*):</label>
                    <textarea
                      rows={10}
                      className="form-textarea code-font"
                      placeholder="Enter full policy terms article by article (Article 1, Article 2...)"
                      value={createForm.content}
                      onChange={(e) => setCreateForm({ ...createForm, content: e.target.value })}
                      required
                    />
                  </div>
                </div>

                <footer className="create-modal-footer">
                  <span className="create-note">
                    ℹ️ When a new policy version is published, the currently active version is automatically transitioned to "Archived" status in the database.
                  </span>
                  <div className="create-actions">
                    <button
                      type="button"
                      className="btn-modal-cancel"
                      onClick={() => setShowCreateModal(false)}
                      disabled={submitting}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      className="btn-modal-submit"
                      disabled={submitting}
                    >
                      {submitting ? "Saving to Database..." : "Confirm & Publish Version"}
                    </button>
                  </div>
                </footer>
              </form>
            </div>
          </div>
        )}
      </div>
    </AdminShell>
  );
}
