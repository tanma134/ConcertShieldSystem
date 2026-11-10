import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import authApi from "../api/authApi";
import PlatformPolicyModal from "../components/PlatformPolicyModal";
import "../styles/auth.css";

export default function RegisterPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({
    email: "",
    password: "",
    fullName: "",
    phoneNumber: "",
  });
  const [agreedPolicies, setAgreedPolicies] = useState(false);
  const [showPolicyModal, setShowPolicyModal] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    // UC 11.1 & 11.2: Check policy agreement
    if (!agreedPolicies) {
      setError("Vui lòng đọc và tích chọn đồng ý với Điều khoản Dịch vụ & Chính sách nền tảng để tiếp tục.");
      return;
    }

    setLoading(true);
    try {
      await authApi.register(form);
      // Chuyển sang trang nhập OTP, kèm email để dùng ở bước sau
      navigate("/verify-email", { state: { email: form.email } });
    } catch (err) {
      setError(err.response?.data?.message || "Registration failed. Please try again.");
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
          <h2>Create Account</h2>
          <p className="auth-subtitle">Join ConcertShield for faster ticket booking</p>

          {error && <div className="auth-error">{error}</div>}

          <form onSubmit={handleSubmit}>
            <div className="auth-field">
              <label>Full Name</label>
              <input
                type="text"
                name="fullName"
                value={form.fullName}
                onChange={handleChange}
                required
              />
            </div>

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
              <label>Phone Number</label>
              <input
                type="tel"
                name="phoneNumber"
                value={form.phoneNumber}
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

            {/* UC 11.1 & UC 11.2: Policy Agreement Checkbox */}
            <div className="auth-policy-group">
              <input
                type="checkbox"
                id="policyAgreement"
                className="auth-policy-checkbox"
                checked={agreedPolicies}
                onChange={(e) => setAgreedPolicies(e.target.checked)}
              />
              <label htmlFor="policyAgreement" className="auth-policy-label">
                I agree to the{" "}
                <button
                  type="button"
                  className="auth-policy-link"
                  onClick={(e) => {
                    e.preventDefault();
                    setShowPolicyModal(true);
                  }}
                >
                  Platform Policies & Terms of Service
                </button>
              </label>
            </div>

            <button
              className="auth-button"
              type="submit"
              disabled={loading || !agreedPolicies}
              style={{
                opacity: agreedPolicies ? 1 : 0.6,
                cursor: agreedPolicies ? "pointer" : "not-allowed",
              }}
            >
              {loading ? "Processing..." : "Sign Up"}
            </button>
          </form>

          <div className="auth-links">
            Already have an account? <Link to="/login">Log In</Link>
          </div>
        </div>
      </div>

      {/* UC 11.1 & 11.2: Platform Policy Modal */}
      <PlatformPolicyModal
        isOpen={showPolicyModal}
        onClose={() => setShowPolicyModal(false)}
        onAgree={() => setAgreedPolicies(true)}
      />
    </div>
  );
}
