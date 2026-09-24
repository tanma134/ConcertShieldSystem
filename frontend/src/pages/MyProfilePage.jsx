import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import authApi from "../api/authApi";
import wishlistApi from "../api/wishlistApi";
import { useAuth } from "../context/AuthContext";
import "./ProfilePages.css";

const formatDate = (value) => value ? new Date(value).toLocaleDateString("en-GB") : "N/A";

export default function MyProfilePage() {
  const navigate = useNavigate();
  const { user, updateUser } = useAuth();
  const [profile, setProfile] = useState(user);
  const [wishlist, setWishlist] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [removeTarget, setRemoveTarget] = useState(null);
  const [removing, setRemoving] = useState(false);

  const load = async () => {
    try {
      setLoading(true); setError("");
      const [profileResponse, wishlistItems] = await Promise.all([authApi.getProfile(), wishlistApi.getMine()]);
      const nextProfile = profileResponse.data;
      setProfile(nextProfile); updateUser(nextProfile); setWishlist(wishlistItems || []);
    } catch (requestError) { setError(requestError.response?.data?.message || "Failed to load your profile."); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); }, []);

  const removeWishlist = async () => {
    if (!removeTarget) return;
    try { setRemoving(true); await wishlistApi.remove(removeTarget.eventId); setWishlist((items) => items.filter((item) => item.eventId !== removeTarget.eventId)); setSuccess("Event removed from your wishlist."); setRemoveTarget(null); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to remove event from your wishlist."); }
    finally { setRemoving(false); }
  };

  return <div className="profile-page"><div className="profile-container"><div className="profile-page-heading"><div><p className="profile-eyebrow">Account</p><h1>My Profile</h1></div><Link className="profile-button secondary" to="/">Back to Events</Link></div>{error && <div className="profile-alert error">{error}</div>}{success && <div className="profile-alert success">{success}</div>}{loading ? <div className="profile-loading">Loading profile...</div> : <><section className="profile-card profile-overview"><div className="profile-avatar">{profile?.avatarUrl ? <img src={profile.avatarUrl} alt={profile.fullName || "Profile avatar"} /> : <span>{(profile?.fullName || profile?.email || "U").charAt(0).toUpperCase()}</span>}</div><div className="profile-details"><h2>{profile?.fullName || "Unnamed user"}</h2><div className="profile-detail-grid"><div><span>Email</span><strong>{profile?.email || "N/A"}</strong></div><div><span>Phone Number</span><strong>{profile?.phoneNumber || "N/A"}</strong></div><div><span>Email Status</span><strong>{profile?.isVerified ? "Verified" : "Unverified"}</strong></div><div><span>eKYC Status</span><strong>{profile?.ekycStatus || "Not Submitted"}</strong></div><div><span>Role</span><strong>{profile?.roleName || "Customer"}</strong></div><div><span>Joined</span><strong>{formatDate(profile?.createdAt)}</strong></div></div><div className="profile-actions"><button type="button" className="profile-button primary" onClick={() => navigate("/profile/edit")}>Edit Profile</button><button type="button" className="profile-button secondary" disabled>Change Password</button></div></div></section><section className="profile-card wishlist-card"><div className="profile-section-heading"><div><p className="profile-eyebrow">Saved events</p><h2>My Wishlist</h2></div><span>{wishlist.length} {wishlist.length === 1 ? "event" : "events"}</span></div>{wishlist.length === 0 ? <div className="profile-empty">Your wishlist is empty.<Link to="/">Explore Events</Link></div> : <div className="wishlist-grid">{wishlist.map((item) => <article className="wishlist-item" key={item.wishlistId}><div className="wishlist-image">{item.eventImage ? <img src={item.eventImage} alt={item.eventName} /> : <span>No image</span>}</div><div className="wishlist-content"><h3>{item.eventName}</h3><p>{formatDate(item.eventDate)}</p><p>{item.location || "Location unavailable"}</p><div className="wishlist-actions"><Link className="profile-button primary small" to={`/events/${item.slug}`}>View Event</Link><button type="button" className="profile-button danger small" onClick={() => setRemoveTarget(item)}>Remove</button></div></div></article>)}</div>}</section></>}</div>{removeTarget && <div className="profile-modal-overlay" onClick={() => setRemoveTarget(null)}><div className="profile-modal" onClick={(event) => event.stopPropagation()}><h2>Remove from Wishlist</h2><p>Are you sure you want to remove this event from your wishlist?</p><div className="profile-modal-actions"><button type="button" className="profile-button secondary" onClick={() => setRemoveTarget(null)}>Cancel</button><button type="button" className="profile-button danger" onClick={removeWishlist} disabled={removing}>{removing ? "Removing..." : "Remove"}</button></div></div></div>}</div>;
}
