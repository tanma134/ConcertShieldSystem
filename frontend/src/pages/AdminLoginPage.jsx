import { useState, useEffect } from "react";
import { useNavigate, Link } from "react-router-dom";
import authApi from "../api/authApi";
import policyApi from "../api/policyApi";
import { useAuth } from "../context/AuthContext";
import PlatformPolicyModal from "../components/PlatformPolicyModal";
import "../styles/auth.css";

export default function AdminLoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [form, setForm] = useState({ email: "", password: "" });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [activePolicy, setActivePolicy] = useState(null);
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [showPolicyModal, setShowPolicyModal] = useState(false);
  const [requireConsentModal, setRequireConsentModal] = useState(false);
  const [pendingLoginData, setPendingLoginData] = useState(null);

  const fetchActivePolicy = async () => {
    try {
      const res = await policyApi.getPricingTerms();
      if (res?.data?.data) {
        const policy = res.data.data;
        setActivePolicy(policy);
        const currentVersion = policy.versionCode || "POL-PRICE-V1.0";
        const lastAccepted = localStorage.getItem("cs_latest_accepted_policy");
        if (lastAccepted === currentVersion) {
          setAgreeTerms(true);
        } else {
          setAgreeTerms(false);
        }
      }
    } catch (err) {
      console.warn("Could not load latest active policy:", err);
    }
  };

  useEffect(() => {
    fetchActivePolicy();
    window.addEventListener("policy-updated", fetchActivePolicy);
    return () => window.removeEventListener("policy-updated", fetchActivePolicy);
  }, []);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handlePolicyAgree = (policy) => {
    const targetVersion = policy?.version || policy?.versionCode || activePolicy?.versionCode || "POL-PRICE-V1.0";
    setAgreeTerms(true);
    setError("");
    localStorage.setItem("cs_latest_accepted_policy", targetVersion);

    if (pendingLoginData) {
      const userKey = pendingLoginData?.user?.userId || pendingLoginData?.user?.email || "admin";
      localStorage.setItem(`cs_policy_consent_${userKey}`, targetVersion);
      login(pendingLoginData);
      navigate("/admin");
      setPendingLoginData(null);
    }

    setShowPolicyModal(false);
    setRequireConsentModal(false);
  };

  const handlePolicyDecline = () => {
    if (pendingLoginData) {
      setPendingLoginData(null);
    }
    setError("Login cancelled: You must review and accept the updated terms and conditions to proceed.");
    setShowPolicyModal(false);
    setRequireConsentModal(false);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (!agreeTerms) {
      setError("Platform terms have been updated. Please accept the terms before logging in.");
      setRequireConsentModal(true);
      setShowPolicyModal(true);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.login(form);

      const roles = res.data?.roles || res.data?.Roles || [];
      const isAdmin = roles.some((role) =>
        String(role?.roleName || role?.name || role).toLowerCase() === "admin"
      );

      if (!isAdmin) {
        setError("This account does not have admin privileges.");
        setLoading(false);
        return;
      }

      const currentVersion = activePolicy?.versionCode || "POL-PRICE-V1.0";
      const userKey = res.data?.user?.userId || res.data?.user?.email;

      // User already agreed via the checkbox before clicking login; record consent and log in directly without second popup
      if (userKey) {
        localStorage.setItem(`cs_policy_consent_${userKey}`, currentVersion);
      }
      localStorage.setItem("cs_latest_accepted_policy", currentVersion);

      login(res.data);
      navigate("/admin");
    } catch (err) {
      setError(err.response?.data?.message || "Login failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <h2>Admin Login</h2>
        <p className="auth-subtitle">Restricted area. Admin accounts only.</p>

        {error && <div className="auth-error">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="auth-field">
            <label>Email</label>
            <input
              type="email"
              name="email"
              value={form.email}
              onChange={handleChange}
              required
            />
          </div>

          <div className="auth-field">
            <label>Password</label>
            <input
              type="password"
              name="password"
              value={form.password}
              onChange={handleChange}
              required
            />
          </div>

          {/* Mandatory Platform Policy Agreement Checkbox */}
          <div style={{
            margin: "12px 0 16px 0",
            textAlign: "left",
            fontSize: "0.83rem",
            color: "#111827",
            background: "#f9fafb",
            border: "1px solid #e5e7eb",
            padding: "10px 12px",
            borderRadius: "8px"
          }}>
            <label style={{ display: "flex", alignItems: "flex-start", gap: "8px", cursor: "pointer", color: "#111827", fontWeight: 500 }}>
              <input
                type="checkbox"
                checked={agreeTerms}
                onChange={(e) => setAgreeTerms(e.target.checked)}
                style={{ marginTop: "2px", width: "16px", height: "16px", accentColor: "#6366f1", cursor: "pointer" }}
              />
              <span>
                I agree to the{" "}
                <button
                  type="button"
                  onClick={() => {
                    setRequireConsentModal(false);
                    setShowPolicyModal(true);
                  }}
                  style={{
                    background: "none",
                    border: "none",
                    color: "#4f46e5",
                    textDecoration: "underline",
                    padding: 0,
                    cursor: "pointer",
                    fontWeight: 600,
                    fontSize: "inherit"
                  }}
                >
                  Platform Policies & Terms ({activePolicy?.versionCode || "POL-PRICE-V1.0"})
                </button>
              </span>
            </label>
          </div>

          <button className="auth-button" type="submit" disabled={loading}>
            {loading ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Login as Admin"}
          </button>
        </form>

        <div className="auth-links">
          <Link to="/login">Back to normal login</Link>
        </div>
      </div>

      {/* Platform Policy Modal */}
      <PlatformPolicyModal
        isOpen={showPolicyModal}
        onClose={() => {
          if (requireConsentModal && pendingLoginData) {
            handlePolicyDecline();
          } else {
            setShowPolicyModal(false);
          }
        }}
        onAgree={handlePolicyAgree}
        onDecline={handlePolicyDecline}
        requireConsent={requireConsentModal}
      />
    </div>
  );
}
