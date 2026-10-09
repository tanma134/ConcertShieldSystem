import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import organizerRequestApi from "../api/organizerRequestApi";
import "../styles/organizerrequest.css";

const STATUS_LABEL = {
  Pending: "Pending",
  Approved: "Approved",
  Rejected: "Rejected",
};

function StatusBadge({ status }) {
  const cls =
    status === "Approved"
      ? "org-badge--approved"
      : status === "Rejected"
      ? "org-badge--rejected"
      : "org-badge--pending";
  return <span className={`org-badge ${cls}`}>{STATUS_LABEL[status] || status}</span>;
}

export default function RequestOrganizerPage() {
  const { isOrganizer, refreshRoles } = useAuth();
  const [form, setForm] = useState({
    reason: "",
    companyName: "",
    website: "",
    phoneNumber: "",
    experience: "",
  });
  const [history, setHistory] = useState([]);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");


  const hasPending = history.some((r) => r.status === "Pending");

  const fetchHistory = async () => {
    try {
      const res = await organizerRequestApi.getMyRequests();
      setHistory(res.data);
      if (!isOrganizer && res.data.some((request) => request.status === "Approved")) {
        await refreshRoles();
      }
    } catch {
      // im lặng bỏ qua nếu không load được lịch sử, không chặn form
    } finally {
      setLoadingHistory(false);
    }
  };

  useEffect(() => {
    fetchHistory();
  }, []);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setSubmitting(true);
    try {
      await organizerRequestApi.create({
        reason: form.reason,
        companyName: form.companyName || null,
        website: form.website || null,
        phoneNumber: form.phoneNumber || null,
        experience: form.experience || null,
      });
      setSuccess("Your organizer request has been submitted. We'll review it soon.");
      setForm({ reason: "", companyName: "", website: "", phoneNumber: "", experience: "" });
      fetchHistory();
    } catch (err) {
      setError(err.response?.data?.message || "Something went wrong. Please try again.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="org-page">
      <div className="org-container">
        <div className="org-card">
          <h1 className="org-title">Become an Organizer</h1>
          <p className="org-subtitle">
            Tell us about yourself and why you'd like to organize events on our platform.
          </p>

          {isOrganizer && (
            <div className="org-alert org-alert--success">
              🎉 You already have the <strong>Organizer</strong> role!{" "}
              <Link to="/organizer/events/new">Create your first event →</Link>
            </div>
          )}

          {!isOrganizer && error && <div className="org-alert org-alert--error">{error}</div>}
          {!isOrganizer && success && <div className="org-alert org-alert--success">{success}</div>}

          {!isOrganizer && hasPending && !success && (
            <div className="org-alert org-alert--success">
              You already have a pending request. Please wait for our review.
            </div>
          )}

          {!isOrganizer && !hasPending && !success && (
            <form onSubmit={handleSubmit}>
              <div className="org-field">
                <label>Reason *</label>
                <textarea
                  name="reason"
                  rows={4}
                  placeholder="Why do you want to become an organizer?"
                  value={form.reason}
                  onChange={handleChange}
                  required
                  maxLength={1000}
                />
              </div>

              <div className="org-field">
                <label>
                  Company name <span className="optional">(optional)</span>
                </label>
                <input
                  name="companyName"
                  type="text"
                  placeholder="Your company or brand"
                  value={form.companyName}
                  onChange={handleChange}
                  maxLength={200}
                />
              </div>

              <div className="org-field">
                <label>
                  Website <span className="optional">(optional)</span>
                </label>
                <input
                  name="website"
                  type="url"
                  placeholder="https://example.com"
                  value={form.website}
                  onChange={handleChange}
                  maxLength={255}
                />
              </div>

              <div className="org-field">
                <label>
                  Phone number <span className="optional">(optional)</span>
                </label>
                <input
                  name="phoneNumber"
                  type="tel"
                  placeholder="0912345678"
                  value={form.phoneNumber}
                  onChange={handleChange}
                  maxLength={20}
                />
              </div>

              <div className="org-field">
                <label>
                  Experience <span className="optional">(optional)</span>
                </label>
                <textarea
                  name="experience"
                  rows={3}
                  placeholder="Any relevant experience organizing events?"
                  value={form.experience}
                  onChange={handleChange}
                  maxLength={2000}
                />
              </div>

              <button className="org-button" type="submit" disabled={submitting}>
                {submitting ? "Submitting..." : "Submit request"}
              </button>
            </form>
          )}

        </div>

        <div className="org-card">
          <h2 className="org-title" style={{ fontSize: "16px" }}>
            Your request history
          </h2>

          {loadingHistory ? (
            <p className="org-empty">Loading...</p>
          ) : history.length === 0 ? (
            <p className="org-empty">You haven't submitted any request yet.</p>
          ) : (
            <div>
              {history.map((r) => (
                <div className="org-history-item" key={r.requestId}>
                  <div className="org-history-item__top">
                    <StatusBadge status={r.status} />
                    <span className="org-history-item__date">
                      {new Date(r.createdAt).toLocaleDateString()}
                    </span>
                  </div>
                  <p className="org-history-item__reason">{r.reason}</p>
                  {r.status === "Rejected" && r.rejectionReason && (
                    <p className="org-history-item__note">Reason: {r.rejectionReason}</p>
                  )}
                  {r.reviewNote && (
                    <p className="org-history-item__note">Note: {r.reviewNote}</p>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
