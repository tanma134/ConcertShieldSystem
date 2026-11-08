import { useCallback, useEffect, useState } from "react";
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
  const [editingId, setEditingId] = useState(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [form, setForm] = useState({ name: "", description: "", zones: [blankZone()] });
  const [error, setError] = useState("");
  const [loadError, setLoadError] = useState("");
  const [saving, setSaving] = useState(false);
  const [loadingEdit, setLoadingEdit] = useState(false);
  const [success, setSuccess] = useState("");

  const load = useCallback(async () => {
    try {
      const r = await seatingTemplateApi.getVisible();
      setTemplates((r.data?.data || []).filter((t) => t.isPublic));
      setLoadError("");
    } catch (e) {
      setLoadError(e.response?.data?.message || "Could not load templates.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const r = await seatingTemplateApi.getVisible();
        if (active) {
          setTemplates((r.data?.data || []).filter((t) => t.isPublic));
          setLoadError("");
        }
      } catch (e) {
        if (active) {
          setLoadError(e.response?.data?.message || "Could not load templates.");
        }
      }
    })();
    return () => {
      active = false;
    };
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

  const openCreateModal = () => {
    setEditingId(null);
    setForm({ name: "", description: "", zones: [blankZone()] });
    setError("");
    setIsModalOpen(true);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingId(null);
    setForm({ name: "", description: "", zones: [blankZone()] });
    setError("");
  };

  const startEdit = async (template) => {
    setError("");
    setLoadingEdit(true);
    try {
      const res = await seatingTemplateApi.getById(template.seatingTemplateId);
      const data = res.data?.data || res.data;
      setEditingId(data.seatingTemplateId);
      setForm({
        name: data.name || "",
        description: data.description || "",
        zones:
          data.zones && data.zones.length > 0
            ? data.zones.map((z) => ({
                _id: nextLocalId++,
                zoneName: z.zoneName || "",
                zoneType: z.zoneType || "Seated",
                rows: z.rows || 10,
                seatsPerRow: z.seatsPerRow || 10,
                rowLabelPrefix: z.rowLabelPrefix || "A",
                capacity: z.capacity || 100,
                shapeJson: z.shapeJson || null,
              }))
            : [blankZone()],
      });
      setIsModalOpen(true);
    } catch (err) {
      setLoadError(err.response?.data?.message || "Could not load template details.");
    } finally {
      setLoadingEdit(false);
    }
  };

  const submit = async (e) => {
    e.preventDefault();
    setError("");
    setSaving(true);
    try {
      const payload = {
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
      };

      if (editingId) {
        await seatingTemplateApi.update(editingId, payload);
        setSuccess(`Template #${editingId} "${form.name.trim()}" updated successfully.`);
      } else {
        await seatingTemplateApi.create(payload);
        setSuccess(`Public template "${form.name.trim()}" created successfully.`);
      }
      closeModal();
      await load();
    } catch (err) {
      setError(
        err.response?.data?.message ||
          (editingId ? "Could not update template." : "Could not create system template.")
      );
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id) => {
    if (!window.confirm("Are you sure you want to delete this system template?")) return;
    try {
      await seatingTemplateApi.remove(id);
      if (editingId === id) closeModal();
      await load();
    } catch (err) {
      setLoadError(err.response?.data?.message || "Could not delete this template.");
    }
  };

  // ZoneMapCanvas keys shapes by `seatZoneId` and colors them by `ticketTypeId`.
  // Template zones have neither, so the client-only `_id` fills both roles here
  // (varying the color per zone) — the canvas never sends it back to the API.
  const canvasZones = form.zones.map((z) => {
    const cap =
      z.zoneType === "Standing"
        ? Number(z.capacity) || 0
        : (Number(z.rows) || 1) * (Number(z.seatsPerRow) || 1);
    return {
      seatZoneId: z._id,
      ticketTypeId: z._id,
      zoneName: z.zoneName || "Untitled zone",
      zoneType: z.zoneType,
      shapeJson: z.shapeJson,
      capacity: cap,
      totalSeats: cap,
      rows: Number(z.rows) || 1,
      seatsPerRow: Number(z.seatsPerRow) || 1,
    };
  });

  const handleZoneMove = (id, rect) => updateZone(id, "shapeJson", JSON.stringify(rect));

  return (
    <AdminShell title="System Seating Templates">
      <div className="ow-wrap admin-page-content">
        <div
          className="ow-head"
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            flexWrap: "wrap",
            gap: "16px",
          }}
        >
          <div>
            <h1>System Seating Templates</h1>
            <p className="ow-sub">
              Manage public seating layouts for the system. Organizers can select and apply
              these templates directly when designing their concert seating charts.
            </p>
          </div>
          <button
            type="button"
            className="tb-btn tb-btn-primary"
            onClick={openCreateModal}
            style={{ display: "inline-flex", alignItems: "center", gap: "8px", whiteSpace: "nowrap" }}
          >
            + Create Template
          </button>
        </div>

        {loadError && <div className="ow-error">{loadError}</div>}
        {success && <div className="ow-banner ow-banner-published">{success}</div>}

        <section className="ow-section">
          <div style={{ marginBottom: "16px" }}>
            <h3 style={{ margin: 0 }}>Template Library</h3>
          </div>

          {!templates.length && <div className="ow-empty-row">No public templates yet.</div>}
          {templates.map((t) => (
            <div className="admin-template-row" key={t.seatingTemplateId}>
              <div>
                <strong>{t.name}</strong>
                <div className="ow-hint">
                  {t.totalZones} zones · suggested capacity {t.estimatedCapacity}
                </div>
              </div>
              <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                <button
                  type="button"
                  className="ow-link"
                  disabled={loadingEdit}
                  onClick={() => startEdit(t)}
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="ow-link-danger"
                  onClick={() => remove(t.seatingTemplateId)}
                >
                  Delete
                </button>
              </div>
            </div>
          ))}
        </section>

        {/* Modal Popup for Create / Edit Template */}
        {isModalOpen && (
          <div
            className="admin-template-modal-backdrop"
            onClick={(e) => {
              if (e.target === e.currentTarget) closeModal();
            }}
          >
            <div className="admin-template-modal">
              <div className="admin-template-modal-head">
                <h3>
                  {editingId
                    ? `Edit Template #${editingId}: ${form.name || "(Unnamed)"}`
                    : "Create Public Seating Template"}
                </h3>
                <button
                  type="button"
                  className="admin-template-modal-close"
                  onClick={closeModal}
                  aria-label="Close"
                >
                  ✕
                </button>
              </div>

              <form onSubmit={submit} style={{ display: "contents" }}>
                <div className="admin-template-modal-body">
                  {error && <div className="ow-error" style={{ marginBottom: "16px" }}>{error}</div>}

                  <div className="ow-inline-form">
                    <label className="ow-field">
                      <span>Name *</span>
                      <input
                        required
                        value={form.name}
                        onChange={(e) => setForm({ ...form, name: e.target.value })}
                        placeholder="e.g. Standard Stadium Arena"
                      />
                    </label>
                    <label className="ow-field ow-span-2">
                      <span>Description</span>
                      <input
                        value={form.description}
                        onChange={(e) => setForm({ ...form, description: e.target.value })}
                        placeholder="Brief overview of this venue configuration"
                      />
                    </label>
                  </div>

                  <div style={{ marginTop: "16px", marginBottom: "16px" }}>
                    <span className="ow-hint" style={{ display: "block", marginBottom: "8px" }}>
                      💡 Drag and resize zones on the canvas to configure the layout geometry:
                    </span>
                    <ZoneMapCanvas
                      key={form.zones.map((z) => z._id).join("-")}
                      zones={canvasZones}
                      ticketTypes={[]}
                      onZoneMove={handleZoneMove}
                    />
                  </div>

                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "20px", marginBottom: "10px" }}>
                    <h4 style={{ margin: 0 }}>Zones ({form.zones.length})</h4>
                    <button type="button" className="tb-btn tb-btn-outline ow-btn-sm" onClick={addZone}>
                      + Add zone
                    </button>
                  </div>

                  {form.zones.map((z) => (
                    <div className="ow-inline-form" key={z._id} style={{ marginBottom: "12px" }}>
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
                            <span>Prefix</span>
                            <input
                              maxLength={3}
                              style={{ width: "60px" }}
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
                        <button
                          type="button"
                          className="ow-link-danger"
                          onClick={() => removeZone(z._id)}
                          style={{ alignSelf: "flex-end" }}
                        >
                          Remove
                        </button>
                      )}
                    </div>
                  ))}
                </div>

                <div className="admin-template-modal-foot">
                  <button type="button" className="tb-btn tb-btn-outline" onClick={closeModal}>
                    Cancel
                  </button>
                  <button className="tb-btn tb-btn-primary" disabled={saving}>
                    {saving
                      ? "Saving..."
                      : editingId
                      ? "Update Template"
                      : "Create Template"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AdminShell>
  );
}
