import { useEffect, useState } from "react";
import ticketTypeApi from "../../../api/ticketTypeApi";
import seatingApi from "../../../api/seatingApi";
import refundPolicyApi from "../../../api/refundPolicyApi";
import { formatPrice } from "../../../utils/format";
import ZoneMapCanvas from "./ZoneMapCanvas";

export default function StepTicketsSeating({
  eventId,
  event,
  onRefresh,
  onSaved,
  onBack,
  onNext,
}) {
  const readOnly = event && !["Draft", "Rejected"].includes(event.status);

  const [ticketTypes, setTicketTypes] = useState([]);
  const [chart, setChart] = useState(null); // null = general admission
  const [refundPolicies, setRefundPolicies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadAll = async () => {
    setLoading(true);
    try {
      const [ttRes, chartRes, rpRes] = await Promise.all([
        ticketTypeApi.getByEvent(eventId),
        seatingApi.getByEvent(eventId),
        refundPolicyApi.getByEvent(eventId),
      ]);
      setTicketTypes(ttRes.data?.data || []);
      setChart(chartRes.data?.data || null);
      setRefundPolicies(rpRes.data?.data || []);
    } catch (err) {
      setError(err.response?.data?.message || "Could not load ticket/seating data.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAll();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [eventId]);

  const hasLayout = !!chart;

  const handleSaveDraft = async () => {
    await loadAll();
    await onRefresh();
    onSaved();
  };

  return (
    <div className="ow-step-body">
      <h2>3. Tickets & Seating</h2>

      {readOnly && (
        <div className="ow-banner ow-banner-pending">
          This event is in <strong>{event.status}</strong> status, so tickets
          and seating can't be changed.
        </div>
      )}

      {error && <div className="ow-error">{error}</div>}
      {loading && <div className="tb-loading">Loading...</div>}

      {!loading && (
        <>
          <TicketTypesSection
            eventId={eventId}
            ticketTypes={ticketTypes}
            hasLayout={hasLayout}
            readOnly={readOnly}
            onChanged={loadAll}
          />

          <SeatingSection
            eventId={eventId}
            chart={chart}
            ticketTypes={ticketTypes}
            readOnly={readOnly}
            onChanged={loadAll}
          />

          <RefundPolicySection
            eventId={eventId}
            refundPolicies={refundPolicies}
            readOnly={readOnly}
            onChanged={loadAll}
          />
        </>
      )}

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

// =============================================================================
// Ticket types
// =============================================================================

const emptyTicket = {
  typeName: "",
  description: "",
  price: "",
  originalPrice: "",
  quantity: "",
  minPerOrder: 1,
  maxPerOrder: 10,
};

function TicketTypesSection({ eventId, ticketTypes, hasLayout, readOnly, onChanged }) {
  const [form, setForm] = useState(emptyTicket);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  const handleAdd = async (e) => {
    e.preventDefault();
    if (!form.typeName.trim()) {
      setError("Please enter a ticket type name.");
      return;
    }
    if (!hasLayout && (!form.quantity || Number(form.quantity) <= 0)) {
      setError("Please enter a ticket quantity.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      await ticketTypeApi.create(eventId, {
        typeName: form.typeName.trim(),
        description: form.description.trim() || null,
        price: Number(form.price) || 0,
        originalPrice: form.originalPrice === "" ? null : Number(form.originalPrice),
        // With a layout present, quantity is derived - the value here is ignored
        // server-side, but CreateTicketTypeDTO.Quantity is still required, so send 0.
        quantity: hasLayout ? 0 : Number(form.quantity),
        minPerOrder: Number(form.minPerOrder) || 1,
        maxPerOrder: Number(form.maxPerOrder) || 10,
      });
      setForm(emptyTicket);
      setShowForm(false);
      await onChanged();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.join(" ")) ||
          err.response?.data?.message ||
          "Could not add the ticket type."
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (ticketTypeId) => {
    setError("");
    try {
      await ticketTypeApi.remove(ticketTypeId);
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || "Could not delete the ticket type.");
    }
  };

  return (
    <section className="ow-section">
      <div className="ow-section-head">
        <h3>Ticket Types</h3>
        {!readOnly && (
          <button
            type="button"
            className="tb-btn tb-btn-outline ow-btn-sm"
            onClick={() => setShowForm((v) => !v)}
          >
            {showForm ? "Close" : "+ Add ticket type"}
          </button>
        )}
      </div>

      {hasLayout && (
        <p className="ow-hint">
          This event uses a seating chart — the quantity of each ticket type
          is automatically derived from the seats/capacity linked to it below.
        </p>
      )}

      {error && <div className="ow-error">{error}</div>}

      {ticketTypes.length === 0 && (
        <div className="ow-empty-row">No ticket types yet.</div>
      )}

      {ticketTypes.length > 0 && (
        <table className="ow-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Price</th>
              <th>Quantity</th>
              <th>Sold</th>
              <th>Per-order limit</th>
              {!readOnly && <th></th>}
            </tr>
          </thead>
          <tbody>
            {ticketTypes.map((t) => (
              <tr key={t.ticketTypeId}>
                <td>
                  <div className="ow-td-title">{t.typeName}</div>
                  {t.description && (
                    <div className="ow-td-sub">{t.description}</div>
                  )}
                </td>
                <td>
                  {formatPrice(t.price)}
                  {t.originalPrice && t.originalPrice > t.price && (
                    <div className="ow-td-strike">{formatPrice(t.originalPrice)}</div>
                  )}
                </td>
                <td>{t.quantity}</td>
                <td>{t.soldQuantity}</td>
                <td>
                  {t.minPerOrder}–{t.maxPerOrder}
                </td>
                {!readOnly && (
                  <td>
                    {t.soldQuantity === 0 && (
                      <button
                        type="button"
                        className="ow-link-danger"
                        onClick={() => handleDelete(t.ticketTypeId)}
                      >
                        Delete
                      </button>
                    )}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {!readOnly && showForm && (
        <form className="ow-inline-form" onSubmit={handleAdd}>
          <div className="ow-grid">
            <label className="ow-field">
              <span>Ticket type name *</span>
              <input
                name="typeName"
                value={form.typeName}
                onChange={handleChange}
                placeholder="e.g. VIP"
                maxLength={100}
              />
            </label>
            <label className="ow-field">
              <span>Price (₫) *</span>
              <input
                type="number"
                min={0}
                name="price"
                value={form.price}
                onChange={handleChange}
              />
            </label>
            <label className="ow-field">
              <span>Original price (if discounted)</span>
              <input
                type="number"
                min={0}
                name="originalPrice"
                value={form.originalPrice}
                onChange={handleChange}
              />
            </label>
            {!hasLayout && (
              <label className="ow-field">
                <span>Quantity *</span>
                <input
                  type="number"
                  min={1}
                  name="quantity"
                  value={form.quantity}
                  onChange={handleChange}
                />
              </label>
            )}
            <label className="ow-field">
              <span>Min per order</span>
              <input
                type="number"
                min={1}
                name="minPerOrder"
                value={form.minPerOrder}
                onChange={handleChange}
              />
            </label>
            <label className="ow-field">
              <span>Max per order</span>
              <input
                type="number"
                min={1}
                name="maxPerOrder"
                value={form.maxPerOrder}
                onChange={handleChange}
              />
            </label>
            <label className="ow-field ow-span-2">
              <span>Description</span>
              <input
                name="description"
                value={form.description}
                onChange={handleChange}
                maxLength={500}
              />
            </label>
          </div>
          <button
            type="submit"
            className="tb-btn tb-btn-primary ow-btn-sm"
            disabled={saving}
          >
            {saving ? "Saving..." : "Add ticket type"}
          </button>
        </form>
      )}
    </section>
  );
}

// =============================================================================
// Seating layout (mixed Seated + Standing zones)
// =============================================================================

const emptyZone = {
  ticketTypeId: "",
  zoneName: "",
  zoneType: "Seated",
  rows: 5,
  seatsPerRow: 10,
  rowLabelPrefix: "A",
  capacity: "",
};

function SeatingSection({ eventId, chart, ticketTypes, readOnly, onChanged }) {
  const [mode, setMode] = useState(chart ? "assigned" : "general");
  const [form, setForm] = useState(emptyZone);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    setMode(chart ? "assigned" : "general");
  }, [chart]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
  };

  const handleModeChange = async (newMode) => {
    if (newMode === mode) return;
    setError("");

    if (newMode === "general" && chart) {
      if (!window.confirm("Delete the entire seating chart and switch back to general admission?")) {
        return;
      }
      setSaving(true);
      try {
        await seatingApi.remove(eventId);
        setMode("general");
        await onChanged();
      } catch (err) {
        setError(err.response?.data?.message || "Could not delete the seating chart.");
      } finally {
        setSaving(false);
      }
      return;
    }

    setMode(newMode);
  };

  const handleAddZone = async (e) => {
    e.preventDefault();
    if (!form.ticketTypeId) {
      setError("Please select a ticket type for this zone.");
      return;
    }
    if (!form.zoneName.trim()) {
      setError("Please enter a zone name.");
      return;
    }
    if (form.zoneType === "Standing" && (!form.capacity || Number(form.capacity) <= 0)) {
      setError("A standing zone needs a capacity greater than 0.");
      return;
    }
    if (
      form.zoneType === "Seated" &&
      (!form.rows || Number(form.rows) <= 0 || !form.seatsPerRow || Number(form.seatsPerRow) <= 0)
    ) {
      setError("A seated zone needs a row count and seats-per-row greater than 0.");
      return;
    }

    const zoneDto = {
      ticketTypeId: Number(form.ticketTypeId),
      zoneName: form.zoneName.trim(),
      zoneType: form.zoneType,
      rows: Number(form.rows) || 1,
      seatsPerRow: Number(form.seatsPerRow) || 1,
      rowLabelPrefix: form.rowLabelPrefix || "A",
      capacity: form.capacity === "" ? null : Number(form.capacity),
    };

    setSaving(true);
    setError("");
    try {
      if (!chart) {
        await seatingApi.build(eventId, {
          name: "Seating Chart",
          zones: [zoneDto],
        });
      } else {
        await seatingApi.addZone(eventId, zoneDto);
      }
      setForm(emptyZone);
      setShowForm(false);
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || "Could not add the zone.");
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteZone = async (seatZoneId) => {
    setError("");
    try {
      await seatingApi.removeZone(seatZoneId);
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || "Could not delete the zone.");
    }
  };

  // Persists where the organizer dragged/resized a zone so the overview looks
  // the same next time they open this concert - like Ticketbox's venue editor.
  const handleZoneMove = async (seatZoneId, rect) => {
    try {
      await seatingApi.updateZone(seatZoneId, {
        shapeJson: JSON.stringify({
          x: Math.round(rect.x * 10) / 10,
          y: Math.round(rect.y * 10) / 10,
          w: Math.round(rect.w * 10) / 10,
          h: Math.round(rect.h * 10) / 10,
        }),
      });
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || "Could not save the zone position.");
    }
  };

  const ticketTypeName = (id) =>
    ticketTypes.find((t) => t.ticketTypeId === id)?.typeName || `#${id}`;

  return (
    <section className="ow-section">
      <div className="ow-section-head">
        <h3>Seating Chart</h3>
      </div>

      <p className="ow-hint">
        An event can combine both <strong>numbered seated</strong> zones
        (buyers pick their own seat) and <strong>standing</strong> zones
        (headcount only) in the same chart.
      </p>

      {error && <div className="ow-error">{error}</div>}

      <div className="ow-mode-toggle">
        <button
          type="button"
          className={"ow-mode-btn" + (mode === "general" ? " active" : "")}
          onClick={() => handleModeChange("general")}
          disabled={readOnly || saving}
        >
          No chart (general admission)
        </button>
        <button
          type="button"
          className={"ow-mode-btn" + (mode === "assigned" ? " active" : "")}
          onClick={() => handleModeChange("assigned")}
          disabled={readOnly || saving}
        >
          Has seating chart
        </button>
      </div>

      {mode === "general" && (
        <p className="ow-hint">
          Each ticket type's capacity comes directly from the "Quantity"
          field in the Ticket Types section above.
        </p>
      )}

      {mode === "assigned" && (
        <>
          {chart && chart.zones?.length > 0 && (
            <>
              <p className="ow-hint">
                Drag the blocks below to arrange the overview layout — the
                same way buyers will see it when choosing tickets.
              </p>
              <ZoneMapCanvas
                zones={chart.zones}
                ticketTypes={ticketTypes}
                readOnly={readOnly}
                onZoneMove={handleZoneMove}
              />
            </>
          )}

          {chart && chart.zones?.length > 0 && (
            <table className="ow-table">
              <thead>
                <tr>
                  <th>Zone</th>
                  <th>Type</th>
                  <th>Ticket type</th>
                  <th>Capacity</th>
                  <th>Available</th>
                  {!readOnly && <th></th>}
                </tr>
              </thead>
              <tbody>
                {chart.zones.map((z) => (
                  <tr key={z.seatZoneId}>
                    <td>{z.zoneName}</td>
                    <td>
                      <span
                        className={
                          "ow-zone-badge " +
                          (z.zoneType === "Seated" ? "ow-zone-seated" : "ow-zone-standing")
                        }
                      >
                        {z.zoneType === "Seated" ? "Seated" : "Standing"}
                      </span>
                    </td>
                    <td>{ticketTypeName(z.ticketTypeId)}</td>
                    <td>{z.capacity}</td>
                    <td>{z.availableSeats}</td>
                    {!readOnly && (
                      <td>
                        <button
                          type="button"
                          className="ow-link-danger"
                          onClick={() => handleDeleteZone(z.seatZoneId)}
                        >
                          Delete
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {(!chart || chart.zones?.length === 0) && (
            <div className="ow-empty-row">No zones in the chart yet.</div>
          )}

          {!readOnly && (
            <>
              {!showForm && (
                <button
                  type="button"
                  className="tb-btn tb-btn-outline ow-btn-sm"
                  onClick={() => setShowForm(true)}
                  disabled={ticketTypes.length === 0}
                >
                  + Add zone
                </button>
              )}
              {ticketTypes.length === 0 && (
                <p className="ow-hint">Add a ticket type before creating a zone.</p>
              )}

              {showForm && (
                <form className="ow-inline-form" onSubmit={handleAddZone}>
                  <div className="ow-grid">
                    <label className="ow-field">
                      <span>Zone name *</span>
                      <input
                        name="zoneName"
                        value={form.zoneName}
                        onChange={handleChange}
                        placeholder="e.g. VIP Stand"
                        maxLength={100}
                      />
                    </label>

                    <label className="ow-field">
                      <span>Zone type *</span>
                      <select name="zoneType" value={form.zoneType} onChange={handleChange}>
                        <option value="Seated">Seated (numbered)</option>
                        <option value="Standing">Standing (headcount only)</option>
                      </select>
                    </label>

                    <label className="ow-field">
                      <span>Applicable ticket type *</span>
                      <select
                        name="ticketTypeId"
                        value={form.ticketTypeId}
                        onChange={handleChange}
                      >
                        <option value="">-- Select ticket type --</option>
                        {ticketTypes.map((t) => (
                          <option key={t.ticketTypeId} value={t.ticketTypeId}>
                            {t.typeName}
                          </option>
                        ))}
                      </select>
                    </label>

                    {form.zoneType === "Seated" ? (
                      <>
                        <label className="ow-field">
                          <span>Number of rows *</span>
                          <input
                            type="number"
                            min={1}
                            name="rows"
                            value={form.rows}
                            onChange={handleChange}
                          />
                        </label>
                        <label className="ow-field">
                          <span>Seats per row *</span>
                          <input
                            type="number"
                            min={1}
                            name="seatsPerRow"
                            value={form.seatsPerRow}
                            onChange={handleChange}
                          />
                        </label>
                        <label className="ow-field">
                          <span>First row label</span>
                          <input
                            name="rowLabelPrefix"
                            value={form.rowLabelPrefix}
                            onChange={handleChange}
                            placeholder="A"
                            maxLength={3}
                          />
                        </label>
                      </>
                    ) : (
                      <label className="ow-field">
                        <span>Capacity *</span>
                        <input
                          type="number"
                          min={1}
                          name="capacity"
                          value={form.capacity}
                          onChange={handleChange}
                        />
                      </label>
                    )}
                  </div>

                  <div className="ow-inline-form-actions">
                    <button
                      type="button"
                      className="tb-btn tb-btn-outline ow-btn-sm"
                      onClick={() => {
                        setShowForm(false);
                        setForm(emptyZone);
                      }}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      className="tb-btn tb-btn-primary ow-btn-sm"
                      disabled={saving}
                    >
                      {saving ? "Saving..." : "Add zone"}
                    </button>
                  </div>
                </form>
              )}
            </>
          )}
        </>
      )}
    </section>
  );
}

// =============================================================================
// Refund policies
// =============================================================================

const emptyPolicy = {
  policyName: "",
  description: "",
  deadlineBeforeEventHours: 168,
  refundPercent: 100,
  requiresOrganizerApproval: false,
};

function RefundPolicySection({ eventId, refundPolicies, readOnly, onChanged }) {
  const [form, setForm] = useState(emptyPolicy);
  const [showForm, setShowForm] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const handleChange = (e) => {
    const { name, value, type, checked } = e.target;
    setForm((f) => ({ ...f, [name]: type === "checkbox" ? checked : value }));
  };

  const handleAdd = async (e) => {
    e.preventDefault();
    if (!form.policyName.trim()) {
      setError("Please enter a policy name.");
      return;
    }

    setSaving(true);
    setError("");
    try {
      await refundPolicyApi.create(eventId, {
        policyName: form.policyName.trim(),
        description: form.description.trim() || null,
        deadlineBeforeEventHours: Number(form.deadlineBeforeEventHours) || 1,
        refundPercent: Number(form.refundPercent) || 0,
        requiresOrganizerApproval: !!form.requiresOrganizerApproval,
        isActive: true,
      });
      setForm(emptyPolicy);
      setShowForm(false);
      await onChanged();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.join(" ")) ||
          err.response?.data?.message ||
          "Could not add the refund policy."
      );
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (refundPolicyId) => {
    setError("");
    try {
      await refundPolicyApi.remove(refundPolicyId);
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || "Could not delete the policy.");
    }
  };

  return (
    <section className="ow-section">
      <div className="ow-section-head">
        <h3>Refund Policies</h3>
        {!readOnly && (
          <button
            type="button"
            className="tb-btn tb-btn-outline ow-btn-sm"
            onClick={() => setShowForm((v) => !v)}
          >
            {showForm ? "Close" : "+ Add policy"}
          </button>
        )}
      </div>

      <p className="ow-hint">At least one policy is required before submitting for review.</p>

      {error && <div className="ow-error">{error}</div>}

      {refundPolicies.length === 0 && (
        <div className="ow-empty-row">No refund policies yet.</div>
      )}

      {refundPolicies.length > 0 && (
        <table className="ow-table">
          <thead>
            <tr>
              <th>Policy name</th>
              <th>Refund</th>
              <th>Deadline</th>
              {!readOnly && <th></th>}
            </tr>
          </thead>
          <tbody>
            {refundPolicies.map((p) => (
              <tr key={p.refundPolicyId}>
                <td>
                  <div className="ow-td-title">{p.policyName}</div>
                  {p.description && <div className="ow-td-sub">{p.description}</div>}
                </td>
                <td>{p.refundPercent}%</td>
                <td>{p.deadlineBeforeEventHours} hours before</td>
                {!readOnly && (
                  <td>
                    <button
                      type="button"
                      className="ow-link-danger"
                      onClick={() => handleDelete(p.refundPolicyId)}
                    >
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {!readOnly && showForm && (
        <form className="ow-inline-form" onSubmit={handleAdd}>
          <div className="ow-grid">
            <label className="ow-field ow-span-2">
              <span>Policy name *</span>
              <input
                name="policyName"
                value={form.policyName}
                onChange={handleChange}
                placeholder="e.g. Full refund"
                maxLength={150}
              />
            </label>
            <label className="ow-field">
              <span>Refund % *</span>
              <input
                type="number"
                min={0}
                max={100}
                name="refundPercent"
                value={form.refundPercent}
                onChange={handleChange}
              />
            </label>
            <label className="ow-field">
              <span>Applies before (hours) *</span>
              <input
                type="number"
                min={1}
                name="deadlineBeforeEventHours"
                value={form.deadlineBeforeEventHours}
                onChange={handleChange}
              />
            </label>
            <label className="ow-field ow-span-2">
              <span>Description</span>
              <input
                name="description"
                value={form.description}
                onChange={handleChange}
                maxLength={1000}
              />
            </label>
            <label className="ow-checkbox ow-span-2">
              <input
                type="checkbox"
                name="requiresOrganizerApproval"
                checked={form.requiresOrganizerApproval}
                onChange={handleChange}
              />
              <span>Require organizer approval for each refund request</span>
            </label>
          </div>
          <button
            type="submit"
            className="tb-btn tb-btn-primary ow-btn-sm"
            disabled={saving}
          >
            {saving ? "Saving..." : "Add policy"}
          </button>
        </form>
      )}
    </section>
  );
}
