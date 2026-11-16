import { useState } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import authApi from "../api/authApi";
import "../styles/auth.css";

export default function ResetPasswordPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: location.state?.email || "",
    resetToken: location.state?.resetToken || "",
    newPassword: "",
    confirmPassword: "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (form.newPassword !== form.confirmPassword) {
      setError("Passwords do not match.");
      return;
    }

    setLoading(true);
    try {
      await authApi.resetPassword({
        email: form.email,
        resetToken: form.resetToken,
        newPassword: form.newPassword,
        confirmPassword: form.confirmPassword,
      });
      navigate("/login");
    } catch (err) {
      setError(err.response?.data?.message || "Failed to reset password.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-wrapper">
      <div className="auth-card-wrap">
        <Link to="/" className="auth-logo">
          Concert<span>Shield</span>
        </Link>

        <div className="auth-card">
          <h2>Reset Password</h2>
          <p className="auth-subtitle">Create a new password for your account</p>

          {error && <div className="auth-error">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="auth-field">
              <label>New Password</label>
              <input
                type="password"
                name="newPassword"
                value={form.newPassword}
                onChange={handleChange}
                required
              />
            </div>

            <div className="auth-field">
              <label>Confirm Password</label>
              <input
                type="password"
                name="confirmPassword"
                value={form.confirmPassword}
                onChange={handleChange}
                required
              />
            </div>

            <button className="auth-button" type="submit" disabled={loading}>
              {loading ? "Updating..." : "Reset Password"}
            </button>
          </form>

          <div className="auth-links">
            <Link to="/login">Log In</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
