import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { GoogleLogin } from "@react-oauth/google";
import authApi from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import PlatformPolicyModal from "../components/PlatformPolicyModal";
import "../styles/auth.css";

export default function LoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [form, setForm] = useState({ email: "", password: "" });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);
  const [showPolicyModal, setShowPolicyModal] = useState(false);

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

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const res = await authApi.login(form);
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

            <button className="auth-button" type="submit" disabled={loading}>
              {loading ? "Logging in..." : "Log In"}
            </button>
          </form>

          <div className="auth-divider">or</div>

          <div className="google-login-wrapper">
            {googleLoading ? (
              <p>Logging in with Google...</p>
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

          {/* Platform Policy Reference */}
          <div style={{ marginTop: "18px", textAlign: "center", fontSize: "0.78rem", color: "#71717a" }}>
            By signing in, you agree to the{" "}
            <button
              type="button"
              className="auth-policy-link"
              onClick={() => setShowPolicyModal(true)}
            >
              Platform Policies & Terms of Service
            </button>
          </div>
        </div>
      </div>

      {/* UC 11.1 & 11.2: Platform Policy Modal */}
      <PlatformPolicyModal
        isOpen={showPolicyModal}
        onClose={() => setShowPolicyModal(false)}
      />
    </div>
  );
}
