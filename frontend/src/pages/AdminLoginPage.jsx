import { useState } from "react";
import { useNavigate, Link } from "react-router-dom";
import authApi from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import "../styles/auth.css";

export default function AdminLoginPage() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [form, setForm] = useState({ email: "", password: "" });
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
      const res = await authApi.login(form);

      // LoginResponseDTO co field "roles" (camelCase neu backend dung
      // JsonNamingPolicy.CamelCase, nguoc lai doi thanh res.data.Roles)
      const roles = res.data?.roles || res.data?.Roles || [];
      const isAdmin = roles.some((role) =>
        String(role?.roleName || role?.name || role).toLowerCase() === "admin"
      );

      if (!isAdmin) {
        setError("This account does not have admin privileges.");
        setLoading(false);
        return;
      }

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

          <button className="auth-button" type="submit" disabled={loading}>
            {loading ? "Logging in..." : "Login as Admin"}
          </button>
        </form>

        <div className="auth-links">
          <Link to="/login">Back to normal login</Link>
        </div>
      </div>
    </div>
  );
}
