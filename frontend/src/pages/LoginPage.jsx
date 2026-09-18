import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import { GoogleLogin } from "@react-oauth/google";
import authApi from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import "../styles/auth.css";

export default function LoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [form, setForm] = useState({ email: "", password: "" });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const res = await authApi.login(form);
      login(res.data);
      navigate("/");
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
      navigate("/");
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
        </div>
      </div>
    </div>
  );
}
