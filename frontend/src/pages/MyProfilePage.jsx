import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import authApi from "../api/authApi";
import wishlistApi from "../api/wishlistApi";
import kycDeletionApi from "../api/kycDeletionApi";
import appealApi from "../api/appealApi";
import { useAuth } from "../context/AuthContext";
import Header from "../components/Header";
import Footer from "../components/Footer";
import "../styles/theme-dark.css";
import "./ProfilePages.css";

const formatDate = (value) =>
  value ? new Date(value).toLocaleDateString("en-GB") : "N/A";

const normalize = (value) => String(value || "").trim().toLowerCase();

const pickLatestRequest = (list) => {
  if (!list || list.length === 0) return null;
  return [...list].sort(
    (a, b) => new Date(b.requestedAt || 0) - new Date(a.requestedAt || 0)
  )[0];
};

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

  const [restrictions, setRestrictions] = useState([]);

  const [latestDeletion, setLatestDeletion] = useState(null);
  const [showDeletionModal, setShowDeletionModal] = useState(false);
  const [deletionStep, setDeletionStep] = useState("confirm"); // "confirm" | "status"
  const [requestingDeletion, setRequestingDeletion] = useState(false);
  const [deletionError, setDeletionError] = useState("");

  const isEkycPassed = normalize(profile?.ekycStatus) === "passed";
  const deletionStatus = normalize(latestDeletion?.status);
  const isDeletionPending = deletionStatus === "pending";
  const isDeletionRejected = deletionStatus === "rejected";

  const load = async () => {
    try {
      setLoading(true);
      setError("");
      const [profileResponse, wishlistItems, deletionResponse, decisionResponse] = await Promise.all([
        authApi.getProfile(),
        wishlistApi.getMine(),
        kycDeletionApi.getMine().catch(() => null),
        appealApi.getMyDecisions().catch(() => null),
      ]);
      const nextProfile = profileResponse.data;
      setProfile(nextProfile);
      updateUser(nextProfile);
      setWishlist(wishlistItems || []);

      const deletionData = deletionResponse?.data;
      const deletionList = Array.isArray(deletionData)
        ? deletionData
        : deletionData?.items || [];
      setLatestDeletion(pickLatestRequest(deletionList));

      const decisionData = decisionResponse?.data;
      setRestrictions(Array.isArray(decisionData) ? decisionData : decisionData?.items || []);
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Failed to load your profile.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const removeWishlist = async () => {
    if (!removeTarget) return;
    try {
      setRemoving(true);
      await wishlistApi.remove(removeTarget.eventId);
      setWishlist((items) => items.filter((item) => item.eventId !== removeTarget.eventId));
      setSuccess("Event removed from your wishlist.");
      setRemoveTarget(null);
    } catch (requestError) {
      setError(
        requestError.response?.data?.message || "Failed to remove event from your wishlist."
      );
    } finally {
      setRemoving(false);
    }
  };

  const openDeletionModal = () => {
    setDeletionError("");
    setDeletionStep(latestDeletion ? "status" : "confirm");
    setShowDeletionModal(true);
  };

  const closeDeletionModal = () => {
    if (requestingDeletion) return;
    setShowDeletionModal(false);
  };

  const requestDeletion = async () => {
    try {
      setRequestingDeletion(true);
      setDeletionError("");
      const response = await kycDeletionApi.request();
      const created = response?.data;
      setLatestDeletion(
        created && typeof created === "object" && created.status
          ? created
          : { status: "Pending", requestedAt: new Date().toISOString() }
      );
      setDeletionStep("status");
    } catch (requestError) {
      setDeletionError(
        requestError.response?.data?.message || "Failed to submit the deletion request."
      );
    } finally {
      setRequestingDeletion(false);
    }
  };

  return (
    <div className="tb-app">
      <Header />

      <main className="profile-page">
        <div className="profile-container">
          <div className="profile-page-heading">
            <div>
              <p className="profile-eyebrow">Account</p>
              <h1>My Profile</h1>
            </div>
            <Link className="profile-button secondary" to="/">
              Back to Events
            </Link>
          </div>

          {error && <div className="profile-alert error">{error}</div>}
          {success && <div className="profile-alert success">{success}</div>}

          {loading ? (
            <div className="profile-loading">Loading profile...</div>
          ) : (
            <>
              <section className="profile-card profile-overview">
                <div className="profile-avatar">
                  {profile?.avatarUrl ? (
                    <img src={profile.avatarUrl} alt={profile.fullName || "Profile avatar"} />
                  ) : (
                    <span>
                      {(profile?.fullName || profile?.email || "U").charAt(0).toUpperCase()}
                    </span>
                  )}
                </div>

                <div className="profile-details">
                  <h2>{profile?.fullName || "Unnamed user"}</h2>
                  <div className="profile-detail-grid">
                    <div>
                      <span>Email</span>
                      <strong>{profile?.email || "N/A"}</strong>
                    </div>
                    <div>
                      <span>Phone Number</span>
                      <strong>{profile?.phoneNumber || "N/A"}</strong>
                    </div>
                    <div>
                      <span>Email Status</span>
                      <strong>{profile?.isVerified ? "Verified" : "Unverified"}</strong>
                    </div>
                    <div>
                      <span>eKYC Status</span>
                      <strong>{profile?.ekycStatus || "Not Submitted"}</strong>
                    </div>
                    <div>
                      <span>Role</span>
                      <strong>{profile?.roleName || "Customer"}</strong>
                    </div>
                    <div>
                      <span>Joined</span>
                      <strong>{formatDate(profile?.createdAt)}</strong>
                    </div>
                  </div>

                  <div className="profile-actions">
                    <button
                      type="button"
                      className="profile-button primary"
                      onClick={() => navigate("/profile/edit")}
                    >
                      Edit Profile
                    </button>
                    <button
                      type="button"
                      className="profile-button secondary"
                      onClick={() => navigate("/profile/change-password")}
                    >
                      Change Password
                    </button>
                    <button
                      type="button"
                      className="profile-button primary"
                      onClick={() => navigate("/kyc")}
                      disabled={isEkycPassed}
                      title={isEkycPassed ? "Your eKYC has already been verified" : undefined}
                    >
                      Verify eKYC
                    </button>
                    {isEkycPassed && (
                      <button
                        type="button"
                        className="profile-button danger"
                        onClick={openDeletionModal}
                      >
                        {latestDeletion ? "View Deletion Status" : "Request eKYC Deletion"}
                      </button>
                    )}
                  </div>
                </div>
              </section>

              {restrictions.length > 0 && (
                <section className="profile-card restriction-card">
                  <div className="profile-section-heading">
                    <div>
                      <p className="profile-eyebrow">Security</p>
                      <h2>Account Restrictions</h2>
                    </div>
                    <span>{restrictions.length}</span>
                  </div>

                  <div className="restriction-list">
                    {restrictions.map((d) => {
                      const id = d.riskDecisionId ?? d.id;
                      const hasAppeal = d.existingAppealId != null;
                      return (
                        <article className="restriction-item" key={id}>
                          <div className="restriction-info">
                            <h3>
                              {d.action === "BLOCK" ? "Activity blocked" : "Under review"}
                              <span className="restriction-status">{d.status}</span>
                            </h3>
                            <p>{d.reasonPublic || "No details provided."}</p>
                            <p>
                              {d.decisionCode ? `${d.decisionCode} · ` : ""}
                              Since {formatDate(d.createdAt)}
                              {d.expiresAt ? ` · Until ${formatDate(d.expiresAt)}` : ""}
                            </p>
                            {hasAppeal && (
                              <p>Appeal #{d.existingAppealId}: <strong>{d.existingAppealStatus}</strong></p>
                            )}
                          </div>
                          {(d.canAppeal || hasAppeal) && (
                            <Link
                              className={`profile-button small ${hasAppeal ? "secondary" : "primary"}`}
                              to={`/risk/decisions/${id}/appeal`}
                            >
                              {hasAppeal ? "View Appeal" : "Submit Appeal"}
                            </Link>
                          )}
                        </article>
                      );
                    })}
                  </div>
                </section>
              )}

              <section className="profile-card wishlist-card">
                <div className="profile-section-heading">
                  <div>
                    <p className="profile-eyebrow">Saved events</p>
                    <h2>My Wishlist</h2>
                  </div>
                  <span>
                    {wishlist.length} {wishlist.length === 1 ? "event" : "events"}
                  </span>
                </div>

                {wishlist.length === 0 ? (
                  <div className="profile-empty">
                    Your wishlist is empty.
                    <Link to="/">Explore Events</Link>
                  </div>
                ) : (
                  <div className="wishlist-grid">
                    {wishlist.map((item) => (
                      <article className="wishlist-item" key={item.wishlistId}>
                        <div className="wishlist-image">
                          {item.eventImage ? (
                            <img src={item.eventImage} alt={item.eventName} />
                          ) : (
                            <span>No image</span>
                          )}
                        </div>
                        <div className="wishlist-content">
                          <h3>{item.eventName}</h3>
                          <p>{formatDate(item.eventDate)}</p>
                          <p>{item.location || "Location unavailable"}</p>
                          <div className="wishlist-actions">
                            <Link
                              className="profile-button primary small"
                              to={`/events/${item.slug}`}
                            >
                              View Event
                            </Link>
                            <button
                              type="button"
                              className="profile-button danger small"
                              onClick={() => setRemoveTarget(item)}
                            >
                              Remove
                            </button>
                          </div>
                        </div>
                      </article>
                    ))}
                  </div>
                )}
              </section>
            </>
          )}
        </div>

        {removeTarget && (
          <div className="profile-modal-overlay" onClick={() => setRemoveTarget(null)}>
            <div className="profile-modal" onClick={(event) => event.stopPropagation()}>
              <h2>Remove from Wishlist</h2>
              <p>Are you sure you want to remove this event from your wishlist?</p>
              <div className="profile-modal-actions">
                <button
                  type="button"
                  className="profile-button secondary"
                  onClick={() => setRemoveTarget(null)}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="profile-button danger"
                  onClick={removeWishlist}
                  disabled={removing}
                >
                  {removing ? "Removing..." : "Remove"}
                </button>
              </div>
            </div>
          </div>
        )}

        {showDeletionModal && (
          <div className="profile-modal-overlay" onClick={closeDeletionModal}>
            <div className="profile-modal" onClick={(event) => event.stopPropagation()}>
              {deletionStep === "confirm" ? (
                <>
                  <h2>Request eKYC Data Deletion</h2>
                  <p>
                    Are you sure you want to request deletion of your eKYC data? Your
                    request will be reviewed by an administrator.
                  </p>
                  {deletionError && (
                    <div className="profile-alert error">{deletionError}</div>
                  )}
                  <div className="profile-modal-actions">
                    <button
                      type="button"
                      className="profile-button secondary"
                      onClick={closeDeletionModal}
                      disabled={requestingDeletion}
                    >
                      No
                    </button>
                    <button
                      type="button"
                      className="profile-button danger"
                      onClick={requestDeletion}
                      disabled={requestingDeletion}
                    >
                      {requestingDeletion ? "Submitting..." : "Yes, Submit"}
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <h2>eKYC Deletion Request</h2>
                  <div className="deletion-status-box">
                    <div className="deletion-status-row">
                      <span>Status</span>
                      <span className={`deletion-status-badge ${deletionStatus || "pending"}`}>
                        {latestDeletion?.status || "Pending"}
                      </span>
                    </div>
                    <div className="deletion-status-row">
                      <span>Requested on</span>
                      <strong>{formatDate(latestDeletion?.requestedAt)}</strong>
                    </div>
                    {latestDeletion?.processedAt && (
                      <div className="deletion-status-row">
                        <span>Processed on</span>
                        <strong>{formatDate(latestDeletion.processedAt)}</strong>
                      </div>
                    )}
                    {isDeletionRejected && latestDeletion?.note && (
                      <div className="deletion-status-row column">
                        <span>Reason from admin</span>
                        <strong>{latestDeletion.note}</strong>
                      </div>
                    )}
                  </div>
                  <p>
                    {isDeletionPending
                      ? "Your request has been submitted and is waiting for an administrator to review it."
                      : isDeletionRejected
                      ? "Your previous request was rejected. You can submit a new request if you still want your eKYC data deleted."
                      : "Your request has been processed."}
                  </p>
                  {deletionError && (
                    <div className="profile-alert error">{deletionError}</div>
                  )}
                  <div className="profile-modal-actions">
                    <button
                      type="button"
                      className="profile-button secondary"
                      onClick={closeDeletionModal}
                    >
                      Close
                    </button>
                    {isDeletionRejected && (
                      <button
                        type="button"
                        className="profile-button danger"
                        onClick={() => {
                          setDeletionError("");
                          setDeletionStep("confirm");
                        }}
                      >
                        Request Again
                      </button>
                    )}
                  </div>
                </>
              )}
            </div>
          </div>
        )}
      </main>

      <Footer />
    </div>
  );
}