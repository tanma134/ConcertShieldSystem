import { useEffect, useRef, useState } from "react";
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
  const [editingId, setEditingId] = useState(null);
  const [editingName, setEditingName] = useState("");
  const [viewingTemplate, setViewingTemplate] = useState(null);
  const [fetchingDetail, setFetchingDetail] = useState(false);
  const [error, setError] = useState("");
  const [loadError, setLoadError] = useState("");
  const [saving, setSaving] = useState(false);
  const [success, setSuccess] = useState("");

  const formRef = useRef(null);

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

  const resetForm = () => {
    setEditingId(null);
    setEditingName("");
    setForm({ name: "", description: "", zones: [blankZone()] });
  };

  const handleView = async (t) => {
    setFetchingDetail(true);
    setError("");
    try {
      const res = await seatingTemplateApi.getById(t.seatingTemplateId);
      const detail = res.data?.data || res.data;
      setViewingTemplate(detail);
    } catch (err) {
      setError(err.response?.data?.message || "Could not load template details.");
    } finally {
      setFetchingDetail(false);
    }
  };

  const handleEdit = async (t) => {
    setFetchingDetail(true);
    setError("");
    setSuccess("");
    try {
      const res = await seatingTemplateApi.getById(t.seatingTemplateId);
      const detail = res.data?.data || res.data;
      
      const loadedZones = (detail.zones || []).map((z) => ({
        _id: nextLocalId++,
        zoneName: z.zoneName || z.name || "",
        zoneType: z.zoneType || "Seated",
        rows: z.rows ?? 10,
        seatsPerRow: z.seatsPerRow ?? 10,
        rowLabelPrefix: z.rowLabelPrefix || "A",
        capacity: z.capacity ?? (Number(z.rows || 10) * Number(z.seatsPerRow || 10)),
        shapeJson: z.shapeJson || null,
      }));

      setForm({
        name: detail.name || "",
        description: detail.description || "",
        zones: loadedZones.length > 0 ? loadedZones : [blankZone()],
      });
      setEditingId(t.seatingTemplateId);
      setEditingName(detail.name || t.name);

      if (viewingTemplate) {
        setViewingTemplate(null);
      }

      // Smooth scroll to form
      setTimeout(() => {
        formRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
      }, 100);
    } catch (err) {
      setError(err.response?.data?.message || "Could not load template for editing.");
    } finally {
      setFetchingDetail(false);
    }
  };

  const submit = async (e) => {
    e.preventDefault();
    setError("");
    setSuccess("");
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
        setSuccess(`Template "${payload.name}" updated successfully.`);
      } else {
        await seatingTemplateApi.create(payload);
        setSuccess("Public template created successfully.");
      }

      resetForm();
      await load();
    } catch (err) {
      setError(err.response?.data?.message || (editingId ? "Could not update template." : "Could not create system template."));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id) => {
    if (!window.confirm("Delete this system template?")) return;
    try {
      await seatingTemplateApi.remove(id);
      if (editingId === id) resetForm();
      if (viewingTemplate?.seatingTemplateId === id) setViewingTemplate(null);
      await load();
    } catch (err) {
      setLoadError(err.response?.data?.message || "Could not delete this template.");
    }
  };

  // ZoneMapCanvas keys shapes by `seatZoneId` and colors them by `ticketTypeId`.
  const canvasZones = form.zones.map((z) => ({
    seatZoneId: z._id,
    ticketTypeId: z._id,
    zoneName: z.zoneName || "Untitled zone",
    zoneType: z.zoneType,
    shapeJson: z.shapeJson,
  }));

  const handleZoneMove = (id, rect) => updateZone(id, "shapeJson", JSON.stringify(rect));

  // Canvas for viewing modal
  const viewCanvasZones = viewingTemplate
    ? (viewingTemplate.zones || []).map((z, idx) => ({
        seatZoneId: idx + 1,
        ticketTypeId: idx + 1,
        zoneName: z.zoneName || "Untitled zone",
        zoneType: z.zoneType || "Seated",
        shapeJson: z.shapeJson,
      }))
    : [];

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
        {error && <div className="ow-error">{error}</div>}
        {success && <div className="ow-banner ow-banner-published">{success}</div>}

        <section className="ow-section">
          <div className="ow-section-header-row" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "16px" }}>
            <h3 style={{ margin: 0 }}>Template Library</h3>
            {fetchingDetail && <span className="ow-hint">Loading template details...</span>}
          </div>

          {!templates.length && <div className="ow-empty-row">No public templates yet.</div>}

          {templates.map((t) => (
            <div className="admin-template-row" key={t.seatingTemplateId}>
              <div>
                <strong style={{ fontSize: "15px", color: "#1e1b2e" }}>{t.name}</strong>
                <div className="ow-hint">
                  {t.totalZones} zones · suggested capacity {t.estimatedCapacity}
                </div>
              </div>
              <div className="admin-template-actions" style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  style={{ textDecoration: "none" }}
                  onClick={() => handleView(t)}
                  title="View layout preview"
                >
                  View
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  style={{ textDecoration: "none" }}
                  onClick={() => handleEdit(t)}
                  title="Edit template zones & details"
                >
                  Edit
                </button>
                <button
                  type="button"
                  className="ow-link-danger"
                  style={{ textDecoration: "none", cursor: "pointer" }}
                  onClick={() => remove(t.seatingTemplateId)}
                >
                  Delete
                </button>
              </div>
            </div>
          ))}
        </section>

        {/* View Template Modal */}
        {viewingTemplate && (
          <div className="rf-backdrop" onClick={() => setViewingTemplate(null)}>
            <div
              className="rf-modal"
              style={{
                maxWidth: "740px",
                width: "92%",
                maxHeight: "85vh",
                display: "flex",
                flexDirection: "column",
                borderRadius: "16px",
                overflow: "hidden",
                background: "#ffffff",
                boxShadow: "0 20px 50px rgba(40, 20, 70, 0.35)",
              }}
              onClick={(e) => e.stopPropagation()}
            >
              <div
                className="rf-header"
                style={{
                  padding: "16px 22px",
                  borderBottom: "1px solid #e9d5ff",
                  background: "#faf5ff",
                  flexShrink: 0,
                }}
              >
                <div>
                  <h2 style={{ fontSize: "18px", margin: 0, color: "#1e1b2e" }}>{viewingTemplate.name}</h2>
                  <p style={{ margin: "4px 0 0", fontSize: "13px", color: "#6b6284" }}>
                    {viewingTemplate.description || "Public system seating template"}
                  </p>
                </div>
                <button
                  type="button"
                  className="rf-close"
                  onClick={() => setViewingTemplate(null)}
                  aria-label="Close"
                >
                  ✕
                </button>
              </div>

              <div
                style={{
                  padding: "16px 22px",
                  overflowY: "auto",
                  flex: 1,
                  display: "flex",
                  flexDirection: "column",
                  gap: "14px",
                }}
              >
                <div style={{ display: "flex", gap: "16px", flexWrap: "wrap", fontSize: "13px" }}>
                  <span className="ow-hint" style={{ color: "#4c1d95" }}>
                    <strong>Total Zones:</strong> {viewingTemplate.totalZones || viewingTemplate.zones?.length || 0}
                  </span>
                  <span className="ow-hint" style={{ color: "#4c1d95" }}>
                    <strong>Suggested Capacity:</strong> {viewingTemplate.estimatedCapacity}
                  </span>
                </div>

                <div
                  style={{
                    border: "1.5px solid #d8b4fe",
                    borderRadius: "12px",
                    overflow: "hidden",
                    background: "#fbf7ff",
                    maxHeight: "260px",
                  }}
                >
                  <ZoneMapCanvas
                    zones={viewCanvasZones}
                    ticketTypes={[]}
                    readOnly={true}
                  />
                </div>

                <div>
                  <h4 style={{ margin: "0 0 8px 0", fontSize: "14px", color: "#1e1b2e" }}>Zones Configuration</h4>
                  <div
                    className="od-table-wrap"
                    style={{
                      maxHeight: "180px",
                      overflowY: "auto",
                      border: "1px solid #e9d5ff",
                      borderRadius: "10px",
                    }}
                  >
                    <table className="ow-table" style={{ width: "100%", fontSize: "12.5px" }}>
                      <thead>
                        <tr>
                          <th>Zone Name</th>
                          <th>Type</th>
                          <th>Rows</th>
                          <th>Seats / Row</th>
                          <th>Prefix</th>
                          <th>Capacity</th>
                        </tr>
                      </thead>
                      <tbody>
                        {(viewingTemplate.zones || []).map((z, i) => (
                          <tr key={i}>
                            <td><strong style={{ color: "#1e1b2e" }}>{z.zoneName}</strong></td>
                            <td>{z.zoneType}</td>
                            <td>{z.zoneType === "Seated" ? z.rows : "-"}</td>
                            <td>{z.zoneType === "Seated" ? z.seatsPerRow : "-"}</td>
                            <td>{z.zoneType === "Seated" ? z.rowLabelPrefix : "-"}</td>
                            <td>{z.capacity || (Number(z.rows || 0) * Number(z.seatsPerRow || 0))}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <div
                style={{
                  display: "flex",
                  justifyContent: "flex-end",
                  gap: "10px",
                  padding: "12px 22px",
                  borderTop: "1px solid #e9d5ff",
                  background: "#faf5ff",
                  flexShrink: 0,
                }}
              >
                <button
                  type="button"
                  className="tb-btn tb-btn-outline"
                  style={{ padding: "8px 18px", fontSize: "13px" }}
                  onClick={() => setViewingTemplate(null)}
                >
                  Close
                </button>
                <button
                  type="button"
                  className="tb-btn tb-btn-primary"
                  style={{ padding: "8px 18px", fontSize: "13px" }}
                  onClick={() => handleEdit(viewingTemplate)}
                >
                  ✏️ Edit This Template
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Create / Edit Template Form */}
        <form className="ow-section" ref={formRef} onSubmit={submit}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "14px" }}>
            <h3 style={{ margin: 0 }}>
              {editingId ? `Edit Seating Chart — Public Template (${editingName})` : "Build Seating Chart — Public Template"}
            </h3>
            {editingId && (
              <button
                type="button"
                className="tb-btn tb-btn-outline ow-btn-sm"
                onClick={resetForm}
              >
                Cancel Edit
              </button>
            )}
          </div>

          {editingId && (
            <div className="ow-banner ow-banner-pending" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span>You are currently editing <strong>{editingName}</strong>. Save changes when done or click Cancel.</span>
              <button type="button" onClick={resetForm} style={{ background: "none", border: "none", color: "#854d0e", cursor: "pointer", fontWeight: 700, textDecoration: "underline" }}>
                Cancel
              </button>
            </div>
          )}

          <div className="ow-inline-form">
            <label className="ow-field">
              <span>Name *</span>
              <input
                required
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Standard Arena 3-Zone"
              />
            </label>
            <label className="ow-field ow-span-2">
              <span>Description</span>
              <input
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                placeholder="Brief description of this seating chart..."
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
                  className="modern-select"
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
              {saving ? "Saving..." : editingId ? "Save template changes" : "Create public template"}
            </button>
          </div>
        </form>
      </div>
    </AdminShell>
  );
}
