import { useEffect, useMemo, useState } from "react";
import { useAuth } from "../context/AuthContext";
import reviewApi from "../api/reviewApi";
import "./ReviewSection.css";

const stars = (rating) => "★".repeat(rating) + "☆".repeat(5 - rating);

function ReviewForm({ initialReview, onSubmit, onCancel, submitting }) {
  const [rating, setRating] = useState(initialReview?.rating || 0);
  const [comment, setComment] = useState(initialReview?.comment || "");

  const handleSubmit = (event) => {
    event.preventDefault();
    if (!rating) return;
    onSubmit({ rating, comment: comment.trim() || null });
  };

  return <form className="review-form" onSubmit={handleSubmit}>
    <div className="review-form-field"><span className="review-label">Rating</span><div className="review-rating-picker" aria-label="Choose a rating">{[1, 2, 3, 4, 5].map((value) => <button type="button" key={value} className={value <= rating ? "selected" : ""} onClick={() => setRating(value)} aria-label={`${value} stars`}>★</button>)}</div></div>
    <label className="review-form-field"><span className="review-label">Comment</span><textarea value={comment} maxLength={1000} onChange={(event) => setComment(event.target.value)} placeholder="Share your experience..." rows="4" /><span className="review-counter">{comment.length}/1000</span></label>
    <div className="review-form-actions"><button type="button" className="review-button secondary" onClick={onCancel}>Cancel</button><button type="submit" className="review-button primary" disabled={!rating || submitting}>{submitting ? "Submitting..." : initialReview ? "Save Changes" : "Submit Review"}</button></div>
  </form>;
}

export default function ReviewSection({ eventId }) {
  const { user, isAuthenticated, roles } = useAuth();
  const [reviews, setReviews] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [editing, setEditing] = useState(null);
  const [showForm, setShowForm] = useState(true);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [submitting, setSubmitting] = useState(false);

  const loadReviews = async () => {
    try {
      setLoading(true);
      setError("");
      const response = await reviewApi.getByEvent(eventId);
      setReviews((response.data || []).filter((item) => !item.isDeleted));
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Failed to load reviews.");
    } finally {
      setLoading(false);
    }
  };

  // Reviews are loaded when the event changes.
  // eslint-disable-next-line react-hooks/set-state-in-effect
  useEffect(() => { loadReviews(); }, [eventId]);

  const ownReview = useMemo(() => reviews.find((item) => Number(item.userId) === Number(user?.userId)), [reviews, user]);
  const average = reviews.length ? (reviews.reduce((sum, item) => sum + item.rating, 0) / reviews.length).toFixed(1) : "0.0";
  const canWrite = isAuthenticated && roles.some((role) => String(role).toLowerCase() === "customer");

  const submitReview = async (payload) => {
    try {
      setSubmitting(true); setError("");
      if (editing) { await reviewApi.update(editing.reviewId, payload); setSuccess("Review updated successfully."); }
      else { await reviewApi.create({ eventId, ...payload }); setSuccess("Review submitted successfully."); }
      setEditing(null); setShowForm(false); await loadReviews();
    } catch (requestError) { setError(requestError.response?.data?.message || "Review operation failed."); }
    finally { setSubmitting(false); }
  };

  const deleteReview = async () => {
    if (!deleteTarget) return;
    try { setSubmitting(true); await reviewApi.remove(deleteTarget.reviewId); setSuccess("Review deleted successfully."); setDeleteTarget(null); await loadReviews(); }
    catch (requestError) { setError(requestError.response?.data?.message || "Failed to delete review."); }
    finally { setSubmitting(false); }
  };

  return <section className="review-section" aria-labelledby="reviews-heading">
    <div className="review-section-header"><div><p className="review-eyebrow">Community feedback</p><h2 id="reviews-heading">Reviews</h2></div><div className="review-summary"><strong>{average} / 5</strong><span className="review-stars">{stars(Math.round(Number(average)))}</span><span>{reviews.length} {reviews.length === 1 ? "review" : "reviews"}</span></div></div>
    {error && <div className="review-alert error">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
    {success && <div className="review-alert success">{success}<button type="button" onClick={() => setSuccess("")}>Dismiss</button></div>}
    {!isAuthenticated && <div className="review-login-prompt"><a href="/login">Log in to write a review.</a></div>}
    {canWrite && !ownReview && !showForm && <button type="button" className="review-button primary write-review-button" onClick={() => setShowForm(true)}>Write a Review</button>}
    {canWrite && showForm && !ownReview && <ReviewForm onSubmit={submitReview} onCancel={() => setShowForm(false)} submitting={submitting} />}
    {loading ? <div className="review-empty">Loading reviews...</div> : reviews.length === 0 ? <div className="review-empty">No reviews yet.</div> : <div className="review-list">{reviews.map((review) => <article className="review-item" key={review.reviewId}><div className="review-item-top"><div><strong>{Number(review.userId) === Number(user?.userId) ? "Your Review" : review.userName || `User #${review.userId}`}</strong><span className="review-date">{new Date(review.createdAt).toLocaleDateString()}</span></div><span className="review-stars">{stars(review.rating)}</span></div>{review.comment && <p className="review-comment">{review.comment}</p>}{review.replies?.length > 0 && <div className="review-replies">{review.replies.map((reply) => <div className="review-reply" key={reply.replyId}><strong>{reply.role}:</strong><span>{reply.comment}</span></div>)}</div>}{Number(review.userId) === Number(user?.userId) && <div className="review-item-actions"><button type="button" onClick={() => { setEditing(review); setShowForm(false); }}>Edit</button><button type="button" onClick={() => setDeleteTarget(review)}>Delete</button></div>}</article>)}</div>}
    {editing && <div className="review-modal-overlay" onClick={() => setEditing(null)}><div className="review-modal" onClick={(event) => event.stopPropagation()}><h3>Edit Review</h3><ReviewForm initialReview={editing} onSubmit={submitReview} onCancel={() => setEditing(null)} submitting={submitting} /></div></div>}
    {deleteTarget && <div className="review-modal-overlay" onClick={() => setDeleteTarget(null)}><div className="review-modal compact" onClick={(event) => event.stopPropagation()}><h3>Delete Review</h3><p>Are you sure you want to delete this review?</p><div className="review-form-actions"><button type="button" className="review-button secondary" onClick={() => setDeleteTarget(null)}>Cancel</button><button type="button" className="review-button danger" onClick={deleteReview} disabled={submitting}>{submitting ? "Deleting..." : "Delete"}</button></div></div></div>}
  </section>;
}
