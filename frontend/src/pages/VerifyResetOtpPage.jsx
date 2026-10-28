import { useState } from "react";
import { useLocation, useNavigate, Link } from "react-router-dom";
import authApi from "../api/authApi";
import "../styles/auth.css";

export default function VerifyResetOtpPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: location.state?.email || "",
    otp: "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      // Giả định API trả về resetToken để dùng ở bước đặt lại mật khẩu
      const res = await authApi.verifyResetOtp(form);
      navigate("/reset-password", {
        state: { email: form.email, resetToken: res.data?.resetToken },
      });
    } catch (err) {
      setError(err.response?.data?.message || "Invalid OTP code.");
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
          <h2>Verify OTP</h2>
          <p className="auth-subtitle">Enter the OTP sent to your email</p>

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
              <label>OTP Code</label>
              <input
                type="text"
                name="otp"
                value={form.otp}
                onChange={handleChange}
                required
              />
            </div>

            <button className="auth-button" type="submit" disabled={loading}>
              {loading ? "Verifying..." : "Verify"}
            </button>
          </form>

          <div className="auth-links">
            <Link to="/login">Back to login</Link>
          </div>
        </div>
      </div>
    </div>
  );
}
