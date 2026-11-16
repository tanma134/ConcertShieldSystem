import { useEffect, useState } from "react";
import AdminShell from "./AdminShell";
import seatingTemplateApi from "../../api/seatingTemplateApi";
import ZoneMapCanvas from "../organizer/steps/ZoneMapCanvas";
import "../organizer/OrganizerWizard.css";
import "./AdminPages.css";

// `_id` is a client-only key so the canvas has something stable to drag/resize
// by before the zone has ever been saved (the backend only assigns a real
// SeatZoneId once a zone belongs to an actual event, which template zones
// never do). It never leaves the browser.
let nextLocalId = 1;
const blankZone = () => ({
  _id: nextLocalId++,
  zoneName: "",
  zoneType: "Seated",
  rows: 10,
  seatsPerRow: 10,
  rowLabelPrefix: "A",
  capacity: 100,
  shapeJson: null,
});

export default function AdminSeatingTemplatesPage() {
  const [templates, setTemplates] = useState([]);
  const [form, setForm] = useState({ name: "", description: "", zones: [blankZone()] });
  const [error, setError] = useState("");
  const [loadError, setLoadError] = useState("");
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState("");

  const load = async () => {
    try {
      const r = await seatingTemplateApi.getVisible();
      setTemplates((r.data?.data || []).filter((t) => t.isPublic));
      setLoadError("");
    } catch (e) {
      setLoadError(e.response?.data?.message || "Could not load templates.");
    }
  };
  useEffect(() => {
    load();
  }, []);

  const updateZone = (id, key, value) =>
    setForm((current) => ({
      ...current,
      zones: current.zones.map((z) => (z._id === id ? { ...z, [key]: value } : z)),
    }));

  const addZone = () =>
    setForm((current) => ({ ...current, zones: [...current.zones, blankZone()] }));

  const removeZone = (id) =>
    setForm((current) => ({ ...current, zones: current.zones.filter((z) => z._id !== id) }));

  const resetForm = () => setForm({ name: "", description: "", zones: [blankZone()] });

  const submit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
    setSaving(true);
    try {
      await seatingTemplateApi.create({
        name: form.name.trim(),
        description: form.description.trim() || null,
        isPublic: true,
        zones: form.zones.map((z) => ({
          zoneName: z.zoneName,
          zoneType: z.zoneType,
          shapeJson: z.shapeJson,
          rows: Number(z.rows),
          seatsPerRow: Number(z.seatsPerRow),
          rowLabelPrefix: z.rowLabelPrefix,
          capacity: Number(z.capacity),
        })),
      });
      resetForm();
      setSuccess("Public template created.");
      await load();
    } catch (err) {
      setError(err.response?.data?.message || "Could not create system template.");
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id) => {
    if (!window.confirm("Delete this system template?")) return;
    try {
      await seatingTemplateApi.remove(id);
      await load();
    } catch (err) {
      setLoadError(err.response?.data?.message || "Could not delete this template.");
    }
  };

  // ZoneMapCanvas keys shapes by `seatZoneId` and colors them by `ticketTypeId`.
  // Template zones have neither, so the client-only `_id` fills both roles here
  // (varying the color per zone) — the canvas never sends it back to the API.
  const canvasZones = form.zones.map((z) => ({
    seatZoneId: z._id,
    ticketTypeId: z._id,
    zoneName: z.zoneName || "Untitled zone",
    zoneType: z.zoneType,
    shapeJson: z.shapeJson,
  }));

  const handleZoneMove = (id, rect) => updateZone(id, "shapeJson", JSON.stringify(rect));

  return (
    <AdminShell title="System Seating Templates">
      <div className="ow-wrap admin-page-content">
        <div className="ow-head">
          <div>
            <h1>System Seating Templates</h1>
            <p className="ow-sub">
              Draw reusable public layouts on the canvas below — drag and resize
              zones the same way an organizer builds a concert's seating chart.
              Ticket types and final capacity are chosen by the Organizer when
              they apply the template to one of their concerts.
            </p>
          </div>
        </div>

        {loadError && <div className="ow-error">{loadError}</div>}

        <section className="ow-section">
          <h3>Template Library</h3>
          {!templates.length && <div className="ow-empty-row">No public templates yet.</div>}
          {templates.map((t) => (
            <div className="admin-template-row" key={t.seatingTemplateId}>
              <div>
                <strong>{t.name}</strong>
                <div className="ow-hint">
                  {t.totalZones} zones · suggested capacity {t.estimatedCapacity}
                </div>
              </div>
              <button
                type="button"
                className="ow-link-danger"
                onClick={() => remove(t.seatingTemplateId)}
              >
                Delete
              </button>
            </div>
          ))}
        </section>

        <form className="ow-section" onSubmit={submit}>
          <h3>Build Seating Chart — Public Template</h3>
          {error && <div className="ow-error">{error}</div>}
          {success && <div className="ow-banner ow-banner-published">{success}</div>}

          <div className="ow-inline-form">
            <label className="ow-field">
              <span>Name *</span>
              <input
                required
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </label>
            <label className="ow-field ow-span-2">
              <span>Description</span>
              <input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </label>
          </div>

          <ZoneMapCanvas
            key={form.zones.map((z) => z._id).join("-")}
            zones={canvasZones}
            ticketTypes={[]}
            onZoneMove={handleZoneMove}
          />

          <h4>Zones</h4>
          {form.zones.map((z) => (
            <div className="ow-inline-form" key={z._id}>
              <label className="ow-field">
                <span>Zone name *</span>
                <input
                  required
                  value={z.zoneName}
                  onChange={(e) => updateZone(z._id, "zoneName", e.target.value)}
                />
              </label>
              <label className="ow-field">
                <span>Type</span>
                <select
                  value={z.zoneType}
                  onChange={(e) => updateZone(z._id, "zoneType", e.target.value)}
                >
                  <option>Seated</option>
                  <option>Standing</option>
                </select>
              </label>
              {z.zoneType === "Seated" ? (
                <>
                  <label className="ow-field">
                    <span>Rows</span>
                    <input
                      type="number"
                      min="1"
                      value={z.rows}
                      onChange={(e) => updateZone(z._id, "rows", e.target.value)}
                    />
                  </label>
                  <label className="ow-field">
                    <span>Seats per row</span>
                    <input
                      type="number"
                      min="1"
                      value={z.seatsPerRow}
                      onChange={(e) => updateZone(z._id, "seatsPerRow", e.target.value)}
                    />
                  </label>
                  <label className="ow-field">
                    <span>Row label prefix</span>
                    <input
                      maxLength={3}
                      value={z.rowLabelPrefix}
                      onChange={(e) => updateZone(z._id, "rowLabelPrefix", e.target.value)}
                    />
                  </label>
                </>
              ) : (
                <label className="ow-field">
                  <span>Suggested capacity</span>
                  <input
                    type="number"
                    min="1"
                    value={z.capacity}
                    onChange={(e) => updateZone(z._id, "capacity", e.target.value)}
                  />
                </label>
              )}
              {form.zones.length > 1 && (
                <button type="button" className="ow-link-danger" onClick={() => removeZone(z._id)}>
                  Remove zone
                </button>
              )}
            </div>
          ))}

          <div className="ow-form-actions">
            <button type="button" className="tb-btn tb-btn-outline" onClick={addZone}>
              + Add zone
            </button>
            <button className="tb-btn tb-btn-primary" disabled={saving}>
              {saving ? "Saving..." : "Create public template"}
            </button>
          </div>
        </form>
      </div>
    </AdminShell>
  );
}
