import { startTimeError, nextLocalMinute } from '../../../utils/eventTimeRules';
import { useEffect, useMemo, useRef, useState } from "react";
import eventApi from "../../../api/eventApi";
import VN_PROVINCES from "../../../data/vnProvinces";
import { useToast } from "../../../components/ToastProvider";

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
  const [clock, setClock] = useState(Date.now());
  useEffect(() => { const timer = window.setInterval(() => setClock(Date.now()), 1000); return () => window.clearInterval(timer); }, []);
  const [showSeo, setShowSeo] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [touched, setTouched] = useState({});
  // Sau lần bấm Save đầu tiên, mọi trường lỗi đều hiện đỏ (kể cả trường chưa chạm vào).
  const [attempted, setAttempted] = useState(false);
  const toast = useToast();
  const formRef = useRef(null);
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

  // Các trường backend bắt buộc ngay khi lưu nháp (CreateEventDTO).
  const DRAFT_REQUIRED = ["title", "slug", "startsAt", "endsAt"];

  // Tất cả lỗi hiện có, theo từng trường. Trường nào đủ điều kiện "nộp duyệt"
  // (EventSubmissionValidator) cũng được kiểm ở đây để organizer thấy sớm.
  const allErrors = useMemo(() => {
    const errors = {};
    if (!form.title.trim()) errors.title = "Event name is required.";

    const normalizedSlug = normalizeSlug(form.slug || form.title);
    if (normalizedSlug.length < 3) errors.slug = "Slug must contain at least 3 characters.";
    else if (slugState.available === false) errors.slug = `This slug is already used. Try '${slugState.suggestion}'.`;

    if (!form.shortDescription.trim() && !form.description.trim())
      errors.shortDescription = "Add a short description (or a full description).";
    if (!form.locationName.trim()) errors.locationName = "Venue name is required.";
    if (!form.address.trim()) errors.address = "Address is required.";
    if (!form.city) errors.city = "Please select a province/city.";

    if (!form.startsAt) errors.startsAt = "Start time is required.";
    else if (startTimeError(form.startsAt, clock)) errors.startsAt = startTimeError(form.startsAt, clock);

    if (!form.endsAt) errors.endsAt = "End time is required.";
    else if (dateRangeError) errors.endsAt = dateRangeError;

    const min = form.minTicketsPerAccount === "" ? null : Number(form.minTicketsPerAccount);
    const max = form.maxTicketsPerAccount === "" ? null : Number(form.maxTicketsPerAccount);
    if (min != null && (!Number.isInteger(min) || min < 1)) errors.minTicketsPerAccount = "Minimum must be a whole number of at least 1.";
    if (max != null && (!Number.isInteger(max) || max < 1)) errors.maxTicketsPerAccount = "Maximum must be a whole number of at least 1.";
    if (min != null && max != null && min > max && !errors.maxTicketsPerAccount)
      errors.maxTicketsPerAccount = "Maximum must be greater than or equal to minimum.";
    return errors;
  }, [form, dateRangeError, slugState, clock]);

  // Chỉ hiện lỗi của trường đã chạm vào, hoặc tất cả sau khi bấm Save.
  const fieldErrors = useMemo(() => {
    const visible = {};
    for (const [name, message] of Object.entries(allErrors)) {
      if (attempted || touched[name]) visible[name] = message;
    }
    // Start/End liên quan nhau: lỗi khoảng thời gian luôn hiện ngay khi có.
    if (dateRangeError) visible.endsAt = dateRangeError;
    return visible;
  }, [allErrors, attempted, touched, dateRangeError]);

  // Gắn aria-invalid để CSS tô viền đỏ cho ô nhập.
  const invalid = (name) => !!fieldErrors[name];

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

  // Kiểm tra trước khi gọi API. mode "draft" chỉ chặn các trường backend bắt buộc;
  // mode "next" yêu cầu đủ thông tin cần cho bước nộp duyệt.
  // Trả về true nếu hợp lệ; nếu không thì tô đỏ, báo toast và cuộn tới ô lỗi đầu tiên.
  const validateBeforeSave = (mode) => {
    setAttempted(true);
    const freshErrors = { ...allErrors };
    const timeError = startTimeError(form.startsAt);
    if (timeError) freshErrors.startsAt = timeError;
    setClock(Date.now());
    const names = Object.keys(freshErrors);
    const blocking = mode === "draft"
      ? names.filter((n) => DRAFT_REQUIRED.includes(n) || ["minTicketsPerAccount", "maxTicketsPerAccount"].includes(n))
      : names;
    if (blocking.length === 0) return true;

    setError("");
    toast.error(
      blocking.length === 1
        ? freshErrors[blocking[0]]
        : `Please fix ${blocking.length} highlighted fields before continuing.`
    );
    window.setTimeout(() => {
      const first = formRef.current?.querySelector('[aria-invalid="true"]');
      first?.scrollIntoView({ behavior: "smooth", block: "center" });
      first?.focus?.();
    }, 0);
    return false;
  };

  const save = async (mode) => {
    if (!validateBeforeSave(mode)) return null;

    setSaving(true);
    setError("");
    try {
      const dto = buildDto();

      if (!eventId) {
        const res = await eventApi.create(dto);
        const created = res.data?.data;
        onCreated(created);
        toast.success("Draft created. You can keep configuring the concert.");
        return created;
      }

      const res = await eventApi.update(eventId, dto);
      const updated = res.data?.data;
      onSaved(updated);
      toast.success("Event info saved.");
      return updated;
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      const message =
        (apiErrors && apiErrors.join(" ")) ||
        err.response?.data?.message ||
        "Could not save the information. Please try again.";
      setError(message);
      toast.error(message);
      return null;
    } finally {
      setSaving(false);
    }
  };

  const handleSaveDraft = () => save("draft");

  const handleNext = async () => {
    if (readOnly) {
      onNext();
      return;
    }
    const result = await save("next");
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

      <fieldset ref={formRef} disabled={readOnly || saving} className="ow-fieldset">
        <div className="ow-grid">
          <label className="ow-field ow-span-2">
            <span>Event name *</span>
            <input
              name="title"
              value={form.title}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, title: true }))}
              aria-invalid={invalid("title")}
              placeholder="e.g. Son Tung M-TP Live in Can Tho"
              maxLength={200}
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
              aria-invalid={invalid("slug")}
              placeholder={normalizeSlug(form.title) || "event-url-slug"}
              maxLength={200}
            />
            {slugState.checking && <span className="ow-hint">Checking availability...</span>}
            {!slugState.checking && slugState.available === true && <span className="ow-field-ok">✓ Slug is available</span>}
            {fieldErrors.slug && <span className="ow-field-error">{fieldErrors.slug}</span>}
          </label>

          <label className="ow-field ow-span-2">
            <span>Short description *</span>
            <input
              name="shortDescription"
              value={form.shortDescription}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, shortDescription: true }))}
              aria-invalid={invalid("shortDescription")}
              placeholder="A short one-line intro, shown on the event card"
              maxLength={500}
            />
            {fieldErrors.shortDescription && <span className="ow-field-error">{fieldErrors.shortDescription}</span>}
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
            <span>Starts · date & time *</span>
            <input
              type="datetime-local"
              name="startsAt"
              step="60"
              min={nextLocalMinute(clock)}
              value={form.startsAt}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, startsAt: true }))}
              aria-invalid={invalid("startsAt")}
            />
            {fieldErrors.startsAt && <span className="ow-field-error">{fieldErrors.startsAt}</span>}
          </label>

          <label className="ow-field">
            <span>Ends · date & time *</span>
            <input
              type="datetime-local"
              name="endsAt"
              step="60"
              min={form.startsAt ? nextLocalMinute(Date.parse(form.startsAt)) : nextLocalMinute(clock)}
              value={form.endsAt}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, endsAt: true }))}
              aria-invalid={invalid("endsAt")}
            />
            {fieldErrors.endsAt && (
              <span className="ow-field-error">{fieldErrors.endsAt}</span>
            )}
          </label>

          <label className="ow-field ow-span-2">
            <span>Venue name *</span>
            <input
              name="locationName"
              value={form.locationName}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, locationName: true }))}
              aria-invalid={invalid("locationName")}
              placeholder="e.g. Can Tho Convention Center"
              maxLength={200}
            />
            {fieldErrors.locationName && <span className="ow-field-error">{fieldErrors.locationName}</span>}
          </label>

          <label className="ow-field ow-span-2">
            <span>Address *</span>
            <input
              name="address"
              value={form.address}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, address: true }))}
              aria-invalid={invalid("address")}
              placeholder="Street number, street name..."
              maxLength={300}
            />
            {fieldErrors.address && <span className="ow-field-error">{fieldErrors.address}</span>}
          </label>


          <label className="ow-field">
            <span>City *</span>
            <select
              name="city"
              value={form.city}
              onChange={handleChange}
              onBlur={() => setTouched((c) => ({ ...c, city: true }))}
              aria-invalid={invalid("city")}
            >
              <option value="">-- Select a province/city --</option>
              {VN_PROVINCES.map((p) => (
                <option key={p} value={p}>
                  {p}
                </option>
              ))}
            </select>
            {fieldErrors.city && <span className="ow-field-error">{fieldErrors.city}</span>}
          </label>

          <label className="ow-field">
            <span>Min tickets / account</span>
            <input
              type="number"
              min={1}
              name="minTicketsPerAccount"
              value={form.minTicketsPerAccount}
              onChange={handleChange}
              aria-invalid={invalid("minTicketsPerAccount")}
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
              aria-invalid={invalid("maxTicketsPerAccount")}
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
          disabled={saving || slugState.checking || readOnly}
        >
          {saving ? "Saving..." : "💾 Save draft"}
        </button>
        <button
          type="button"
          className="tb-btn tb-btn-primary"
          onClick={handleNext}
          disabled={saving || slugState.checking}
        >
          {readOnly ? "Next →" : saving ? "Saving..." : "Save & Next →"}
        </button>
      </div>
    </div>
  );
}
