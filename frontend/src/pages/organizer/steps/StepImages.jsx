import { useEffect, useRef, useState } from "react";
import eventApi from "../../../api/eventApi";
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
    setError(err.response?.data?.message || fallback);
  };

  const handlePosterChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setError("");
    setUploading((u) => ({ ...u, poster: true }));
    try {
      await eventApi.uploadPoster(eventId, file);
      await onRefresh();
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
    } catch (err) {
      handleError(err, "Failed to delete image.");
    }
  };

  const handleSetMain = async (imageId) => {
    setError("");
    try {
      await eventImageApi.setMain(imageId);
      await loadGallery();
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
      <h2>2. Images</h2>

      {readOnly && (
        <div className="ow-banner ow-banner-pending">
          This event is in <strong>{event.status}</strong> status, so images
          can't be changed.
        </div>
      )}

      {error && <div className="ow-error">{error}</div>}

      <div className="ow-image-grid">
        <div className="ow-image-card">
          <h3>Poster (portrait)</h3>
          <p className="ow-hint">Shown on the event card and homepage.</p>
          {event?.posterUrl ? (
            <img className="ow-image-preview ow-poster" src={event.posterUrl} alt="Poster" />
          ) : (
            <div className="ow-image-placeholder ow-poster">No poster yet</div>
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
                ? "Uploading..."
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
          <h3>Banner (landscape)</h3>
          <p className="ow-hint">Large cover image at the top of the detail page.</p>
          {event?.bannerUrl ? (
            <img className="ow-image-preview ow-banner" src={event.bannerUrl} alt="Banner" />
          ) : (
            <div className="ow-image-placeholder ow-banner">No banner yet</div>
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
                ? "Uploading..."
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

      <div className="ow-gallery-section">
        <h3>Photo gallery</h3>
        <p className="ow-hint">Up to 20 photos. The first photo automatically becomes the main image.</p>

        <div className="ow-gallery-grid">
          {gallery.map((img) => (
            <div key={img.imageId} className="ow-gallery-item">
              <img src={img.imageUrl} alt="" />
              {img.isMain && <span className="ow-gallery-badge">Main photo</span>}
              {!readOnly && (
                <div className="ow-gallery-item-actions">
                  {!img.isMain && (
                    <button type="button" onClick={() => handleSetMain(img.imageId)}>
                      Set as main
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
                {uploading.gallery ? "Uploading..." : "+ Add photos"}
              </label>
            </div>
          )}
        </div>
      </div>

      <div className="ow-actions">
        <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
          ← Back
        </button>
        <button type="button" className="tb-btn tb-btn-outline" onClick={handleSaveDraft}>
          💾 Save draft
        </button>
        <button type="button" className="tb-btn tb-btn-primary" onClick={onNext}>
          Next →
        </button>
      </div>
    </div>
  );
}
