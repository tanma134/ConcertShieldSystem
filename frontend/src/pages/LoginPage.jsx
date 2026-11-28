import { useState, useEffect } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { GoogleLogin } from "@react-oauth/google";
import authApi from "../api/authApi";
import policyApi from "../api/policyApi";
import { useAuth } from "../context/AuthContext";
import PlatformPolicyModal from "../components/PlatformPolicyModal";
import "../styles/auth.css";

export default function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { login } = useAuth();
  const [form, setForm] = useState({ email: "", password: "" });
  // BR-112: a session ended by a risk block lands here with the reason in the URL,
  // so the message explains the block instead of a generic "session expired".
  const [error, setError] = useState(() =>
    searchParams.get("reason") === "account-blocked"
      ? searchParams.get("msg") ||
        "Your account is temporarily restricted due to unusual activity."
      : ""
  );
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [showPolicyModal, setShowPolicyModal] = useState(false);
  const [requireConsentModal, setRequireConsentModal] = useState(false);
  const [activePolicy, setActivePolicy] = useState(null);
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [pendingLoginData, setPendingLoginData] = useState(null);

  const fetchActivePolicy = async () => {
    try {
      const res = await policyApi.getPricingTerms();
      if (res?.data?.data) {
        const policy = res.data.data;
        setActivePolicy(policy);
        const currentVersion = policy.versionCode || "POL-PRICE-V1.0";
        const lastAccepted = localStorage.getItem("cs_latest_accepted_policy");
        // Pre-check agreement only if already consented to this current version previously
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

  const redirectByRole = (loginData) => {
    const roles = loginData?.roles || loginData?.Roles || [];
    const roleNames = roles.map((role) =>
      String(role?.roleName || role?.name || role).toLowerCase()
    );

    const isAdmin = roleNames.includes("admin");
    const isStaff = roleNames.some((r) =>
      r.includes("staff") || r.includes("validator") || r.includes("checkin")
    );
    const isOrganizer = roleNames.includes("organizer");

    if (isAdmin) {
      navigate("/admin");
    } else if (isStaff) {
      navigate("/staff/gate-assignment");
    } else if (isOrganizer) {
      navigate("/organizer/dashboard");
    } else {
      navigate("/");
    }
  };

  const handlePolicyAgree = (policy) => {
    const targetVersion = policy?.version || policy?.versionCode || activePolicy?.versionCode || "POL-PRICE-V1.0";
    setAgreeTerms(true);
    setError("");
    localStorage.setItem("cs_latest_accepted_policy", targetVersion);

    if (pendingLoginData) {
      const userKey = pendingLoginData?.user?.userId || pendingLoginData?.user?.email || "anonymous";
      localStorage.setItem(`cs_policy_consent_${userKey}`, targetVersion);
      login(pendingLoginData);
      redirectByRole(pendingLoginData);
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

    // Check if user has checked terms agreement (required for first login or updated policy)
    if (!agreeTerms) {
      setError("Platform terms have been updated. Please accept the terms before logging in.");
      setRequireConsentModal(true);
      setShowPolicyModal(true);
      return;
    }

    setLoading(true);
    try {
      const res = await authApi.login(form);
      const currentVersion = activePolicy?.versionCode || "POL-PRICE-V1.0";
      const userKey = res.data?.user?.userId || res.data?.user?.email;

      // User already agreed via the checkbox before clicking login; record consent and log in directly without second popup
      if (userKey) {
        localStorage.setItem(`cs_policy_consent_${userKey}`, currentVersion);
      }
      localStorage.setItem("cs_latest_accepted_policy", currentVersion);

      login(res.data);
      redirectByRole(res.data);
    } catch (err) {
      setError(err.response?.data?.message || "Login failed. Please try again.");
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleSuccess = async (credentialResponse) => {
    setError("");
    setGoogleLoading(true);
    try {
      const res = await authApi.loginWithGoogle({
        idToken: credentialResponse.credential,
      });
      const currentVersion = activePolicy?.versionCode || "POL-PRICE-V1.0";
      const userKey = res.data?.user?.userId || res.data?.user?.email;
      const consentedVersion = localStorage.getItem(`cs_policy_consent_${userKey}`) || localStorage.getItem("cs_latest_accepted_policy");

      if (!agreeTerms && consentedVersion !== currentVersion) {
        setPendingLoginData(res.data);
        setRequireConsentModal(true);
        setShowPolicyModal(true);
        setError("New policy terms have been enacted. Please review and accept the updated terms before logging in.");
        return;
      }

      if (userKey) {
        localStorage.setItem(`cs_policy_consent_${userKey}`, currentVersion);
      }
      localStorage.setItem("cs_latest_accepted_policy", currentVersion);

      login(res.data);
      redirectByRole(res.data);
    } catch (err) {
      setError(err.response?.data?.message || "Google login failed. Please try again.");
    } finally {
      setGoogleLoading(false);
    }
  };

  return (
    <div className="auth-wrapper">
      <div className="auth-card-wrap">
        <Link to="/" className="auth-logo">
          Concert<span>Shield</span>
        </Link>

        <div className="auth-card">
          <h2>Log In</h2>
          <p className="auth-subtitle">Welcome back to ConcertShield</p>

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
                    Platform Policies & Terms of Service ({activePolicy?.versionCode || "POL-PRICE-V1.0"})
                  </button>
                </span>
              </label>
            </div>

            <button className="auth-button" type="submit" disabled={loading}>
              {loading ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Log In"}
            </button>
          </form>

          <div className="auth-divider">or</div>

          <div className="google-login-wrapper">
            {googleLoading ? (
              <span className="app-loading-spinner" role="status" aria-label="Loading" />
            ) : (
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => setError("Google login failed. Please try again.")}
              />
            )}
          </div>

          <div className="auth-links">
            <Link to="/forgot-password">Forgot password?</Link>
            <br />
            Don't have an account? <Link to="/register">Sign Up</Link>
          </div>
        </div>
      </div>

      {/* Platform Policy Modal (supports both voluntary viewing and mandatory login consent) */}
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
