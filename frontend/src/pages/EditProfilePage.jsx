import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import authApi from "../api/authApi";
import { useAuth } from "../context/AuthContext";
import "./ProfilePages.css";

export default function EditProfilePage() {
  const navigate = useNavigate();
  const { updateUser } = useAuth();
  const [form, setForm] = useState({ email: "", fullName: "", phoneNumber: "" });
  const [avatar, setAvatar] = useState(null);
  const [preview, setPreview] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  useEffect(() => { authApi.getProfile().then((response) => { setForm({ email: response.data.email || "", fullName: response.data.fullName || "", phoneNumber: response.data.phoneNumber || "" }); setPreview(response.data.avatarUrl || ""); }).catch((requestError) => setError(requestError.response?.data?.message || "Failed to load profile.")).finally(() => setLoading(false)); }, []);
  const change = (event) => setForm((current) => ({ ...current, [event.target.name]: event.target.value }));
  const chooseAvatar = (event) => { const file = event.target.files?.[0]; if (!file) return; const allowed = ["image/jpeg", "image/png", "image/webp"]; if (!allowed.includes(file.type) || file.size > 5 * 1024 * 1024) return setError("Choose a JPG, PNG, or WEBP image up to 5 MB."); setError(""); setAvatar(file); setPreview(URL.createObjectURL(file)); };
  const save = async (event) => { event.preventDefault(); try { setSaving(true); setError(""); const profileResponse = await authApi.updateProfile(form); let nextProfile = profileResponse.data; if (avatar) { try { setUploading(true); nextProfile = (await authApi.uploadAvatar(avatar)).data; } catch (uploadError) { updateUser(nextProfile); setError(uploadError.response?.data?.message || "Profile information was saved, but avatar upload failed."); return; } } updateUser(nextProfile); setSuccess("Profile updated successfully."); setAvatar(null); setTimeout(() => navigate("/profile"), 500); } catch (requestError) { setError(requestError.response?.data?.message || "Failed to update profile."); } finally { setSaving(false); setUploading(false); } };

  return <div className="profile-page"><div className="profile-container profile-form-container"><div className="profile-page-heading"><div><p className="profile-eyebrow">Account</p><h1>Edit Profile</h1></div><Link className="profile-button secondary" to="/profile">Cancel</Link></div>{error && <div className="profile-alert error">{error}</div>}{success && <div className="profile-alert success">{success}</div>}{loading ? <div className="profile-loading">Loading profile...</div> : <form className="profile-card edit-profile-form" onSubmit={save}><div className="avatar-upload"><div className="profile-avatar large">{preview ? <img src={preview} alt="Avatar preview" /> : <span>U</span>}</div><label className="profile-button secondary" htmlFor="avatar-file">Choose Avatar<input id="avatar-file" type="file" accept="image/jpeg,image/png,image/webp" onChange={chooseAvatar} hidden /></label><small>JPG, PNG, or WEBP. Maximum 5 MB.</small></div><label><span>Full Name</span><input name="fullName" value={form.fullName} onChange={change} required maxLength={200} /></label><label><span>Email</span><input name="email" type="email" value={form.email} onChange={change} required /></label><label><span>Phone Number</span><input name="phoneNumber" value={form.phoneNumber} onChange={change} maxLength={30} /></label><div className="edit-profile-actions"><Link className="profile-button secondary" to="/profile">Cancel</Link><button className="profile-button primary" type="submit" disabled={saving}>{uploading ? "Uploading..." : saving ? "Saving..." : "Save Changes"}</button></div></form>}</div></div>;
}
