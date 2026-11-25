import { useEffect, useRef, useState } from "react";
import eventApi from "../../../api/eventApi";
import { useToast } from "../../../components/ToastProvider";
import eventImageApi from "../../../api/eventImageApi";

export default function StepImages({
  eventId,
  event,
  onRefresh,
  onSaved,
  onBack,
  onNext,
}) {
  const [gallery, setGallery] = useState([]);
  const [uploading, setUploading] = useState({ poster: false, banner: false, gallery: false });
  const [error, setError] = useState("");
  const toast = useToast();
  const posterInput = useRef(null);
  const bannerInput = useRef(null);
  const galleryInput = useRef(null);

  const readOnly = event && !["Draft", "Rejected"].includes(event.status);

  const loadGallery = async () => {
    try {
      const res = await eventImageApi.getByEvent(eventId);
      setGallery(res.data?.data || []);
    } catch {
      // gallery is optional - a load failure shouldn't block the step
    }
  };

  useEffect(() => {
    loadGallery();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId]);

  const handleError = (err, fallback) => {
    const message = err.response?.data?.message || fallback;
    setError(message);
    toast.error(message);
  };

  const handlePosterChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setError("");
    setUploading((u) => ({ ...u, poster: true }));
    try {
      await eventApi.uploadPoster(eventId, file);
      await onRefresh();
      toast.success("Poster uploaded.");
    } catch (err) {
      handleError(err, "Failed to upload poster.");
    } finally {
      setUploading((u) => ({ ...u, poster: false }));
      if (posterInput.current) posterInput.current.value = "";
    }
  };

  const handleDeletePoster = async () => {
    setError("");
    try {
      await eventApi.deletePoster(eventId);
      await onRefresh();
      toast.success("Poster removed.");
    } catch (err) {
      handleError(err, "Failed to delete poster.");
    }
  };

  const handleBannerChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setError("");
    setUploading((u) => ({ ...u, banner: true }));
    try {
      await eventApi.uploadBanner(eventId, file);
      await onRefresh();
      toast.success("Banner uploaded.");
    } catch (err) {
      handleError(err, "Failed to upload banner.");
    } finally {
      setUploading((u) => ({ ...u, banner: false }));
      if (bannerInput.current) bannerInput.current.value = "";
    }
  };

  const handleDeleteBanner = async () => {
    setError("");
    try {
      await eventApi.deleteBanner(eventId);
      await onRefresh();
      toast.success("Banner removed.");
    } catch (err) {
      handleError(err, "Failed to delete banner.");
    }
  };

  const handleGalleryChange = async (e) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0) return;
    setError("");
    setUploading((u) => ({ ...u, gallery: true }));
    try {
      await eventImageApi.uploadMultiple(eventId, files);
      await loadGallery();
      toast.success("Gallery images uploaded.");
    } catch (err) {
      handleError(err, "Failed to upload gallery images.");
    } finally {
      setUploading((u) => ({ ...u, gallery: false }));
      if (galleryInput.current) galleryInput.current.value = "";
    }
  };

  const handleDeleteGalleryImage = async (imageId) => {
    setError("");
    try {
      await eventImageApi.remove(imageId);
      await loadGallery();
      toast.success("Image removed.");
    } catch (err) {
      handleError(err, "Failed to delete image.");
    }
  };

  const handleSetMain = async (imageId) => {
    setError("");
    try {
      await eventImageApi.setMain(imageId);
      await loadGallery();
      toast.success("Main image updated.");
    } catch (err) {
      handleError(err, "Failed to set main image.");
    }
  };

  // Uploads already persist immediately - "Save draft" here just confirms
  // and refreshes the parent's copy of the concert, kept for a consistent
  // flow with the other steps.
  const handleSaveDraft = async () => {
    await onRefresh();
    onSaved();
  };

  return (
    <div className="ow-step-body">
      <h2>2. Event Images</h2>

      {readOnly && (
        <div className="ow-banner ow-banner-pending">
          Event is currently <strong>{event.status}</strong>; image updates are locked.
        </div>
      )}

      {error && <div className="ow-error">{error}</div>}

      <div className="ow-image-grid" style={{ alignItems: "start" }}>
        <div className="ow-image-card">
          <h3>Event Poster (Portrait)</h3>
          <p className="ow-hint">Displayed on event cards, tickets, and search discovery.</p>
          {event?.posterUrl ? (
            <img
              className="ow-image-preview ow-poster"
              src={event.posterUrl}
              alt="Poster"
              style={{ maxHeight: "360px", width: "auto", maxWidth: "100%", margin: "0 auto 10px", display: "block" }}
            />
          ) : (
            <div className="ow-image-placeholder ow-poster" style={{ maxHeight: "360px" }}>No poster uploaded</div>
          )}
          <div className="ow-image-actions">
            <input
              ref={posterInput}
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              onChange={handlePosterChange}
              disabled={readOnly || uploading.poster}
              id="poster-input"
              className="ow-file-input"
            />
            <label htmlFor="poster-input" className="tb-btn tb-btn-outline">
              {uploading.poster
                ? <span className="app-loading-spinner" role="status" aria-label="Loading" />
                : event?.posterUrl
                ? "Replace poster"
                : "Upload poster"}
            </label>
            {event?.posterUrl && !readOnly && (
              <button
                type="button"
                className="ow-link-danger"
                onClick={handleDeletePoster}
              >
                Delete
              </button>
            )}
          </div>
        </div>

        <div className="ow-image-card">
          <h3>Cover Banner (Landscape)</h3>
          <p className="ow-hint">Hero banner across the top of the concert details page.</p>
          {event?.bannerUrl ? (
            <img className="ow-image-preview ow-banner" src={event.bannerUrl} alt="Banner" />
          ) : (
            <div className="ow-image-placeholder ow-banner">No banner uploaded</div>
          )}
          <div className="ow-image-actions">
            <input
              ref={bannerInput}
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              onChange={handleBannerChange}
              disabled={readOnly || uploading.banner}
              id="banner-input"
              className="ow-file-input"
            />
            <label htmlFor="banner-input" className="tb-btn tb-btn-outline">
              {uploading.banner
                ? <span className="app-loading-spinner" role="status" aria-label="Loading" />
                : event?.bannerUrl
                ? "Replace banner"
                : "Upload banner"}
            </label>
            {event?.bannerUrl && !readOnly && (
              <button
                type="button"
                className="ow-link-danger"
                onClick={handleDeleteBanner}
              >
                Delete
              </button>
            )}
          </div>
        </div>
      </div>

      <div className="ow-gallery-section" style={{ marginTop: "16px" }}>
        <h3>Event Photo Gallery</h3>
        <p className="ow-hint">Up to 20 images of the venue, artists, and past performances. First photo is automatically set as main.</p>

        {gallery.length === 0 && (
          <div
            style={{
              padding: "24px 16px",
              textAlign: "center",
              background: "var(--tb-surface-2)",
              borderRadius: "10px",
              color: "var(--tb-text-muted)",
              fontSize: "13px",
              border: "1px dashed var(--tb-border)",
              margin: "12px 0 16px 0",
            }}
          >
            No photos uploaded to gallery yet.
          </div>
        )}

        <div className="ow-gallery-grid">
          {gallery.map((img) => (
            <div key={img.imageId} className="ow-gallery-item">
              <img src={img.imageUrl} alt="" />
              {img.isMain && <span className="ow-gallery-badge">Cover</span>}
              {!readOnly && (
                <div className="ow-gallery-item-actions">
                  {!img.isMain && (
                    <button type="button" onClick={() => handleSetMain(img.imageId)}>
                      Set as cover
                    </button>
                  )}
                  <button
                    type="button"
                    className="ow-link-danger"
                    onClick={() => handleDeleteGalleryImage(img.imageId)}
                  >
                    Delete
                  </button>
                </div>
              )}
            </div>
          ))}

          {!readOnly && (
            <div className="ow-gallery-add">
              <input
                ref={galleryInput}
                type="file"
                multiple
                accept="image/jpeg,image/png,image/webp,image/gif"
                onChange={handleGalleryChange}
                disabled={uploading.gallery}
                id="gallery-input"
                className="ow-file-input"
              />
              <label htmlFor="gallery-input">
                {uploading.gallery ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Add photos"}
              </label>
            </div>
          )}
        </div>
      </div>

      <div className="ow-actions">
        <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
          Back
        </button>
        <button type="button" className="tb-btn tb-btn-outline" onClick={handleSaveDraft}>
          Save Draft
        </button>
        <button type="button" className="tb-btn tb-btn-primary" onClick={onNext}>
          Continue
        </button>
      </div>
    </div>
  );
}
