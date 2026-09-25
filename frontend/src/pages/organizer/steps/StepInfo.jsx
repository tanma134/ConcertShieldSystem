import { useEffect, useMemo, useState } from "react";
import eventApi from "../../../api/eventApi";
import VN_PROVINCES from "../../../data/vnProvinces";

// Convert "2027-03-15T19:00:00Z" (from API) <-> "2027-03-15T19:00" (datetime-local input)
function toLocalInput(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(
    d.getHours()
  )}:${pad(d.getMinutes())}`;
}

const emptyForm = {
  title: "",
  slug: "",
  shortDescription: "",
  description: "",
  locationName: "",
  address: "",
  city: "",
  startsAt: "",
  endsAt: "",
  minTicketsPerAccount: "",
  maxTicketsPerAccount: "",
  metaTitle: "",
  metaDescription: "",
};

export default function StepInfo({ eventId, event, onCreated, onSaved, onNext }) {
  const [form, setForm] = useState(emptyForm);
  const [showSeo, setShowSeo] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [touched, setTouched] = useState({});
  const [slugState, setSlugState] = useState({ checking: false, available: null, suggestion: "" });

  const readOnly = event && !["Draft", "Rejected"].includes(event.status);

  useEffect(() => {
    if (!event) return;
    setForm({
      title: event.title || "",
      slug: event.slug || "",
      shortDescription: event.shortDescription || "",
      description: event.description || "",
      locationName: event.locationName || "",
      address: event.address || "",
      city: event.city || "",
      startsAt: toLocalInput(event.startsAt),
      endsAt: toLocalInput(event.endsAt),
      minTicketsPerAccount: event.minTicketsPerAccount ?? "",
      maxTicketsPerAccount: event.maxTicketsPerAccount ?? "",
      metaTitle: event.metaTitle || "",
      metaDescription: event.metaDescription || "",
    });
  }, [event]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
    setTouched((current) => ({ ...current, [name]: true }));
  };

  const normalizeSlug = (value) => value
    .normalize("NFD").replace(/[\u0300-\u036f]/g, "")
    .replace(/[đĐ]/g, "d").toLowerCase()
    .replace(/[^a-z0-9\s-]/g, "").replace(/[\s-]+/g, "-").replace(/^-|-$/g, "")
    .slice(0, 200);

  useEffect(() => {
    const slug = normalizeSlug(form.slug || form.title);
    if (slug.length < 3) {
      setSlugState({ checking: false, available: null, suggestion: "" });
      return undefined;
    }
    const timer = window.setTimeout(async () => {
      setSlugState((s) => ({ ...s, checking: true }));
      try {
        const res = await eventApi.checkSlug(slug, eventId);
        const data = res.data?.data;
        setSlugState({ checking: false, available: !!data?.available, suggestion: data?.suggestion || "" });
      } catch {
        setSlugState({ checking: false, available: null, suggestion: "" });
      }
    }, 350);
    return () => window.clearTimeout(timer);
  }, [form.slug, form.title, eventId]);

  // Dynamic validation: recompute the moment the start/end date changes,
  // instead of waiting until Save is clicked to surface an error.
  const dateRangeError = useMemo(() => {
    if (!form.startsAt || !form.endsAt) return "";
    if (new Date(form.endsAt) <= new Date(form.startsAt)) {
      return "End time must be after the start time.";
    }
    return "";
  }, [form.startsAt, form.endsAt]);

  const fieldErrors = useMemo(() => {
    const errors = {};
    if (touched.title && !form.title.trim()) errors.title = "Event name is required.";
    const normalizedSlug = normalizeSlug(form.slug || form.title);
    if ((touched.slug || touched.title) && normalizedSlug.length < 3) errors.slug = "Slug must contain at least 3 characters.";
    else if (slugState.available === false) errors.slug = `This slug is already used. Try '${slugState.suggestion}'.`;
    if (touched.startsAt && !form.startsAt) errors.startsAt = "Start time is required.";
    // Gated by `touched` so that simply reopening an existing Draft/Rejected
    // event whose start date has since passed (the organizer just came back
    // to it later) doesn't permanently disable Save/Next before they've
    // touched the field themselves.
    if (touched.startsAt && form.startsAt && new Date(form.startsAt) <= new Date()) {
      errors.startsAt = "Start time must be in the future.";
    }
    if (touched.endsAt && !form.endsAt) errors.endsAt = "End time is required.";
    if (dateRangeError) errors.endsAt = dateRangeError;
    const min = form.minTicketsPerAccount === "" ? null : Number(form.minTicketsPerAccount);
    const max = form.maxTicketsPerAccount === "" ? null : Number(form.maxTicketsPerAccount);
    if (min != null && min < 1) errors.minTicketsPerAccount = "Minimum must be at least 1.";
    if (max != null && max < 1) errors.maxTicketsPerAccount = "Maximum must be at least 1.";
    if (min != null && max != null && min > max) errors.maxTicketsPerAccount = "Maximum must be greater than or equal to minimum.";
    return errors;
  }, [form, touched, dateRangeError, slugState]);

  const hasLiveErrors = Object.keys(fieldErrors).length > 0;

  const buildDto = () => {
    const dto = {
      title: form.title.trim(),
      slug: normalizeSlug(form.slug || form.title),
      shortDescription: form.shortDescription.trim() || null,
      description: form.description.trim() || null,
      locationName: form.locationName.trim() || null,
      address: form.address.trim() || null,
      city: form.city.trim() || null,
      startsAt: form.startsAt ? new Date(form.startsAt).toISOString() : null,
      endsAt: form.endsAt ? new Date(form.endsAt).toISOString() : null,
      minTicketsPerAccount:
        form.minTicketsPerAccount === "" ? null : Number(form.minTicketsPerAccount),
      maxTicketsPerAccount:
        form.maxTicketsPerAccount === "" ? null : Number(form.maxTicketsPerAccount),
      metaTitle: form.metaTitle.trim() || null,
      metaDescription: form.metaDescription.trim() || null,
    };
    return dto;
  };

  const validateClientSide = () => {
    if (!form.title.trim()) return "Please enter an event name.";
    if (!eventId) {
      // Create requires StartsAt/EndsAt right away (CreateEventDTO marks them Required).
      if (!form.startsAt || !form.endsAt)
        return "Please choose a start time and an end time.";
    }
    if (dateRangeError) return dateRangeError;
    return "";
  };

  const save = async () => {
    const clientError = validateClientSide();
    if (clientError) {
      setError(clientError);
      return null;
    }

    setSaving(true);
    setError("");
    try {
      const dto = buildDto();

      if (!eventId) {
        const res = await eventApi.create(dto);
        const created = res.data?.data;
        onCreated(created);
        return created;
      }

      const res = await eventApi.update(eventId, dto);
      const updated = res.data?.data;
      onSaved(updated);
      return updated;
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.join(" ")) ||
          err.response?.data?.message ||
          "Could not save the information. Please try again."
      );
      return null;
    } finally {
      setSaving(false);
    }
  };

  const handleSaveDraft = () => save();

  const handleNext = async () => {
    if (readOnly) {
      onNext();
      return;
    }
    const result = await save();
    if (result) onNext();
  };

  return (
    <div className="ow-step-body">
      <h2>1. Event Info</h2>

      {readOnly && (
        <div className="ow-banner ow-banner-pending">
          This event is in <strong>{event.status}</strong> status, so its
          basic info can't be edited.
        </div>
      )}

      {error && <div className="ow-error">{error}</div>}

      <fieldset disabled={readOnly || saving} className="ow-fieldset">
        <div className="ow-grid">
          <label className="ow-field ow-span-2">
            <span>Event name *</span>
            <input
              name="title"
              value={form.title}
              onChange={handleChange}
              placeholder="e.g. Son Tung M-TP Live in Can Tho"
              maxLength={200}
              aria-invalid={!!fieldErrors.title}
            />
            {fieldErrors.title && <span className="ow-field-error">{fieldErrors.title}</span>}
          </label>

          <label className="ow-field ow-span-2">
            <span>Public URL slug *</span>
            <input
              name="slug"
              value={form.slug}
              onChange={handleChange}
              onBlur={() => setTouched((current) => ({ ...current, slug: true }))}
              placeholder={normalizeSlug(form.title) || "event-url-slug"}
              maxLength={200}
              aria-invalid={!!fieldErrors.slug}
            />
            {slugState.checking && <span className="ow-hint">Checking availability...</span>}
            {!slugState.checking && slugState.available === true && <span className="ow-field-ok">✓ Slug is available</span>}
            {fieldErrors.slug && <span className="ow-field-error">{fieldErrors.slug}</span>}
          </label>

          <label className="ow-field ow-span-2">
            <span>Short description</span>
            <input
              name="shortDescription"
              value={form.shortDescription}
              onChange={handleChange}
              placeholder="A short one-line intro, shown on the event card"
              maxLength={500}
            />
          </label>

          <label className="ow-field ow-span-2">
            <span>Full description</span>
            <textarea
              name="description"
              value={form.description}
              onChange={handleChange}
              rows={5}
              placeholder="Full details about the event, artists, program..."
            />
          </label>

          <label className="ow-field">
            <span>Starts *</span>
            <input
              type="datetime-local"
              name="startsAt"
              value={form.startsAt}
              onChange={handleChange}
              aria-invalid={!!fieldErrors.startsAt}
            />
            {fieldErrors.startsAt && <span className="ow-field-error">{fieldErrors.startsAt}</span>}
          </label>

          <label className="ow-field">
            <span>Ends *</span>
            <input
              type="datetime-local"
              name="endsAt"
              value={form.endsAt}
              onChange={handleChange}
              aria-invalid={!!fieldErrors.endsAt}
            />
            {fieldErrors.endsAt && (
              <span className="ow-field-error">{fieldErrors.endsAt}</span>
            )}
          </label>

          <label className="ow-field ow-span-2">
            <span>Venue name</span>
            <input
              name="locationName"
              value={form.locationName}
              onChange={handleChange}
              placeholder="e.g. Can Tho Convention Center"
              maxLength={200}
            />
          </label>

          <label className="ow-field ow-span-2">
            <span>Address</span>
            <input
              name="address"
              value={form.address}
              onChange={handleChange}
              placeholder="Street number, street name..."
              maxLength={300}
            />
          </label>


          <label className="ow-field">
            <span>City</span>
            <select name="city" value={form.city} onChange={handleChange}>
              <option value="">-- Select a province/city --</option>
              {VN_PROVINCES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
          </label>

          <label className="ow-field">
            <span>Min tickets / account</span>
            <input
              type="number"
              min={1}
              name="minTicketsPerAccount"
              value={form.minTicketsPerAccount}
              onChange={handleChange}
              aria-invalid={!!fieldErrors.minTicketsPerAccount}
            />
            {fieldErrors.minTicketsPerAccount && <span className="ow-field-error">{fieldErrors.minTicketsPerAccount}</span>}
          </label>

          <label className="ow-field">
            <span>Max tickets / account</span>
            <input
              type="number"
              min={1}
              name="maxTicketsPerAccount"
              value={form.maxTicketsPerAccount}
              onChange={handleChange}
              aria-invalid={!!fieldErrors.maxTicketsPerAccount}
            />
            {fieldErrors.maxTicketsPerAccount && <span className="ow-field-error">{fieldErrors.maxTicketsPerAccount}</span>}
          </label>
        </div>

        <button
          type="button"
          className="ow-toggle-seo"
          onClick={() => setShowSeo((v) => !v)}
        >
          {showSeo ? "▾" : "▸"} SEO options (optional)
        </button>

        {showSeo && (
          <div className="ow-grid">
            <label className="ow-field ow-span-2">
              <span>Meta title</span>
              <input
                name="metaTitle"
                value={form.metaTitle}
                onChange={handleChange}
                maxLength={200}
              />
            </label>
            <label className="ow-field ow-span-2">
              <span>Meta description</span>
              <input
                name="metaDescription"
                value={form.metaDescription}
                onChange={handleChange}
                maxLength={500}
              />
            </label>
          </div>
        )}
      </fieldset>

      <div className="ow-actions">
        <button
          type="button"
          className="tb-btn tb-btn-outline"
          onClick={handleSaveDraft}
          disabled={saving || slugState.checking || readOnly || hasLiveErrors}
        >
          {saving ? "Saving..." : "💾 Save draft"}
        </button>
        <button
          type="button"
          className="tb-btn tb-btn-primary"
          onClick={handleNext}
          disabled={saving || slugState.checking || (!readOnly && hasLiveErrors)}
        >
          {readOnly ? "Next →" : saving ? "Saving..." : "Save & Next →"}
        </button>
      </div>
    </div>
  );
}
