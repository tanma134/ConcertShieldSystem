import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import authApi from "../api/authApi";
import Header from "../components/Header";
import Footer from "../components/Footer";
import "../styles/theme-dark.css";
import "./ProfilePages.css";

const MIN_LENGTH = 8;

export default function ChangePasswordPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ currentPassword: "", newPassword: "", confirmPassword: "" });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const change = (event) =>
    setForm((current) => ({ ...current, [event.target.name]: event.target.value }));

  const submit = async (event) => {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (form.newPassword.length < MIN_LENGTH) {
      return setError(`New password must be at least ${MIN_LENGTH} characters.`);
    }
    if (form.newPassword !== form.confirmPassword) {
      return setError("New password and confirmation do not match.");
    }
    if (form.newPassword === form.currentPassword) {
      return setError("New password must be different from the current password.");
    }

    try {
      setSaving(true);
      await authApi.changePassword({
        currentPassword: form.currentPassword,
        newPassword: form.newPassword,
        confirmPassword: form.confirmPassword,
      });
      setSuccess("Password changed successfully.");
      setForm({ currentPassword: "", newPassword: "", confirmPassword: "" });
      setTimeout(() => navigate("/profile"), 800);
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Failed to change password.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="tb-app">
      <Header />

      <main className="profile-page">
        <div className="profile-container profile-form-container">
          <div className="profile-page-heading">
            <div>
              <p className="profile-eyebrow">Account</p>
              <h1>Change Password</h1>
            </div>
            <Link className="profile-button secondary" to="/profile">
              Cancel
            </Link>
          </div>

          {error && <div className="profile-alert error">{error}</div>}
          {success && <div className="profile-alert success">{success}</div>}

          <form className="profile-card edit-profile-form" onSubmit={submit}>
            <label>
              <span>Current Password</span>
              <input
                name="currentPassword"
                type="password"
                value={form.currentPassword}
                onChange={change}
                autoComplete="current-password"
                required
              />
            </label>
            <label>
              <span>New Password</span>
              <input
                name="newPassword"
                type="password"
                value={form.newPassword}
                onChange={change}
                autoComplete="new-password"
                minLength={MIN_LENGTH}
                required
              />
            </label>
            <label>
              <span>Confirm New Password</span>
              <input
                name="confirmPassword"
                type="password"
                value={form.confirmPassword}
                onChange={change}
                autoComplete="new-password"
                minLength={MIN_LENGTH}
                required
              />
            </label>

            <div className="edit-profile-actions">
              <Link className="profile-button secondary" to="/profile">
                Cancel
              </Link>
              <button className="profile-button primary" type="submit" disabled={saving}>
                {saving ? "Saving..." : "Change Password"}
              </button>
            </div>
          </form>
        </div>
      </main>

      <Footer />
    </div>
  );
}
