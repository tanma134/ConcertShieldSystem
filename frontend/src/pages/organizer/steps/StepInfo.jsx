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
  shortDescription: "",
  description: "",
  locationName: "",
  address: "",
  city: "",
  latitude: "",
  longitude: "",
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

  const readOnly = event && !["Draft", "Rejected"].includes(event.status);

  useEffect(() => {
    if (!event) return;
    setForm({
      title: event.title || "",
      shortDescription: event.shortDescription || "",
      description: event.description || "",
      locationName: event.locationName || "",
      address: event.address || "",
      city: event.city || "",
      latitude: event.latitude ?? "",
      longitude: event.longitude ?? "",
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
  };

  // Dynamic validation: recompute the moment the start/end date changes,
  // instead of waiting until Save is clicked to surface an error.
  const dateRangeError = useMemo(() => {
    if (!form.startsAt || !form.endsAt) return "";
    if (new Date(form.endsAt) <= new Date(form.startsAt)) {
      return "End time must be after the start time.";
    }
    return "";
  }, [form.startsAt, form.endsAt]);

  const buildDto = () => {
    const dto = {
      title: form.title.trim(),
      shortDescription: form.shortDescription.trim() || null,
      description: form.description.trim() || null,
      locationName: form.locationName.trim() || null,
      address: form.address.trim() || null,
      city: form.city.trim() || null,
      latitude: form.latitude === "" ? null : Number(form.latitude),
      longitude: form.longitude === "" ? null : Number(form.longitude),
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
            />
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
            />
          </label>

          <label className="ow-field">
            <span>Ends *</span>
            <input
              type="datetime-local"
              name="endsAt"
              value={form.endsAt}
              onChange={handleChange}
              aria-invalid={!!dateRangeError}
            />
            {dateRangeError && (
              <span className="ow-field-error">{dateRangeError}</span>
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
            />
          </label>

          <label className="ow-field">
            <span>Max tickets / account</span>
            <input
              type="number"
              min={1}
              name="maxTicketsPerAccount"
              value={form.maxTicketsPerAccount}
              onChange={handleChange}
            />
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
          disabled={saving || readOnly || !!dateRangeError}
        >
          {saving ? "Saving..." : "💾 Save draft"}
        </button>
        <button
          type="button"
          className="tb-btn tb-btn-primary"
          onClick={handleNext}
          disabled={saving || (!readOnly && !!dateRangeError)}
        >
          {readOnly ? "Next →" : saving ? "Saving..." : "Save & Next →"}
        </button>
      </div>
    </div>
  );
}
