import { Fragment, useEffect, useMemo, useState } from "react";
import ticketTypeApi from "../../../api/ticketTypeApi";
import seatingApi from "../../../api/seatingApi";
import seatingTemplateApi from "../../../api/seatingTemplateApi";
import refundPolicyApi from "../../../api/refundPolicyApi";
import pricingRuleApi from "../../../api/pricingRuleApi";
import { formatPrice } from "../../../utils/format";
import ZoneMapCanvas from "./ZoneMapCanvas";
import SeatGridPreview from "./SeatGridPreview";

export default function StepTicketsSeating({
  eventId,
  event,
  onRefresh,
  onSaved,
  onBack,
  onNext,
  forceEditable = false,
  only = null,
  standalone = false,
}) {
  const readOnly = !forceEditable && event && !["Draft", "Rejected"].includes(event.status);

  const [ticketTypes, setTicketTypes] = useState([]);
  const [chart, setChart] = useState(null); // null = general admission
  const [refundPolicies, setRefundPolicies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadAll = async (silent = false) => {
    if (!silent) setLoading(true);
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
      if (!silent) setLoading(false);
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
          {(!only || only === "tickets") && <TicketTypesSection
            eventId={eventId}
            ticketTypes={ticketTypes}
            chart={chart}
            hasLayout={hasLayout}
            readOnly={readOnly}
            onChanged={loadAll}
          />}

          {(!only || only === "seating") && <SeatingSection
            eventId={eventId}
            chart={chart}
            ticketTypes={ticketTypes}
            readOnly={readOnly}
            onChanged={loadAll}
            onSilentRefresh={() => loadAll(true)}
          />}

          {(!only || only === "pricing") && <DynamicPricingSection
            ticketTypes={ticketTypes}
            readOnly={readOnly}
          />}

          {(!only || only === "refunds") && <RefundPolicySection
            eventId={eventId}
            refundPolicies={refundPolicies}
            readOnly={readOnly}
            onChanged={loadAll}
          />}
        </>
      )}

      {!standalone && <div className="ow-actions">
        <button type="button" className="tb-btn tb-btn-outline" onClick={onBack}>
          ← Back
        </button>
        <button type="button" className="tb-btn tb-btn-outline" onClick={handleSaveDraft}>
          💾 Save draft
        </button>
        <button type="button" className="tb-btn tb-btn-primary" onClick={onNext}>
          Next →
        </button>
      </div>}
    </div>
  );
}

// =============================================================================
// Dynamic pricing rules
// =============================================================================

const emptyPricingRule = {
  ticketTypeId: "", ruleName: "", ruleType: "EarlyBird",
  adjustmentMode: "discount",
  adjustedPrice: "", discountPercent: "", triggerFrom: "", triggerTo: "",
  quantityThreshold: "", priority: 0,
};

function DynamicPricingSection({ ticketTypes, readOnly }) {
  const [rules, setRules] = useState([]);
  const [form, setForm] = useState(emptyPricingRule);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const loadRules = async () => {
    if (!ticketTypes.length) { setRules([]); return; }
    try {
      const results = await Promise.all(ticketTypes.map((t) => pricingRuleApi.getByTicketType(t.ticketTypeId)));
      setRules(results.flatMap((r) => r.data?.data || []));
    } catch (err) { setError(err.response?.data?.message || "Could not load dynamic pricing rules."); }
  };

  useEffect(() => { loadRules(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [ticketTypes]);

  const submit = async (e) => {
    e.preventDefault();
    if (!form.ticketTypeId || !form.ruleName.trim()) { setError("Ticket type and rule name are required."); return; }
    if (form.adjustmentMode === "price" && form.adjustedPrice === "") { setError("Enter the adjusted price."); return; }
    if (form.adjustmentMode === "discount" && form.discountPercent === "") { setError("Enter the discount percentage."); return; }
    if (form.ruleType === "QuantityBased" && (!form.quantityThreshold || Number(form.quantityThreshold) <= 0)) { setError("Quantity Based rules require a sold-ticket threshold."); return; }
    if (form.ruleType !== "QuantityBased" && !form.triggerFrom && !form.triggerTo) { setError("Time-based rules require at least one trigger time."); return; }
    if (form.triggerFrom && form.triggerTo && new Date(form.triggerTo) <= new Date(form.triggerFrom)) {
      setError("Rule end time must be after its start time."); return;
    }
    setSaving(true); setError("");
    try {
      await pricingRuleApi.create({
        ticketTypeId: Number(form.ticketTypeId), ruleName: form.ruleName.trim(), ruleType: form.ruleType,
        adjustedPrice: form.adjustmentMode === "price" ? Number(form.adjustedPrice) : null,
        discountPercent: form.adjustmentMode === "discount" ? Number(form.discountPercent) : null,
        triggerFrom: form.triggerFrom ? new Date(form.triggerFrom).toISOString() : null,
        triggerTo: form.triggerTo ? new Date(form.triggerTo).toISOString() : null,
        quantityThreshold: form.quantityThreshold === "" ? null : Number(form.quantityThreshold),
        priority: Number(form.priority) || 0, isActive: true,
      });
      setForm(emptyPricingRule); setShowForm(false); await loadRules();
    } catch (err) { setError(err.response?.data?.message || "Could not create the pricing rule."); }
    finally { setSaving(false); }
  };

  const remove = async (id) => {
    try { await pricingRuleApi.remove(id); await loadRules(); }
    catch (err) { setError(err.response?.data?.message || "Could not delete the pricing rule."); }
  };

  return <section className="ow-section">
    <div className="ow-section-head"><h3>Dynamic Pricing Rules</h3>{!readOnly && <button type="button" className="tb-btn tb-btn-outline ow-btn-sm" onClick={() => setShowForm((v) => !v)}>{showForm ? "Close" : "+ Add pricing rule"}</button>}</div>
    {error && <div className="ow-error">{error}</div>}
    {!rules.length && <div className="ow-empty-row">No dynamic pricing rules yet.</div>}
    {!!rules.length && <table className="ow-table"><thead><tr><th>Rule</th><th>Ticket</th><th>Type</th><th>Adjustment</th>{!readOnly && <th></th>}</tr></thead><tbody>{rules.map((r) => <tr key={r.pricingRuleId}><td>{r.ruleName}</td><td>{ticketTypes.find((t) => t.ticketTypeId === r.ticketTypeId)?.typeName}</td><td>{r.ruleType}</td><td>{r.adjustedPrice != null ? formatPrice(r.adjustedPrice) : `${r.discountPercent}% off`}</td>{!readOnly && <td><button type="button" className="ow-link-danger" onClick={() => remove(r.pricingRuleId)}>Delete</button></td>}</tr>)}</tbody></table>}
    {!readOnly && showForm && <form className="ow-inline-form" onSubmit={submit}><div className="ow-grid">
      <label className="ow-field"><span>Ticket class *</span><select value={form.ticketTypeId} onChange={(e) => setForm({ ...form, ticketTypeId: e.target.value })}><option value="">-- Select --</option>{ticketTypes.map((t) => <option key={t.ticketTypeId} value={t.ticketTypeId}>{t.typeName}</option>)}</select></label>
      <label className="ow-field"><span>Rule name *</span><input value={form.ruleName} onChange={(e) => setForm({ ...form, ruleName: e.target.value })} /></label>
      <label className="ow-field"><span>Rule type *</span><select value={form.ruleType} onChange={(e) => setForm({ ...form, ruleType: e.target.value })}><option>EarlyBird</option><option>LastMinute</option><option>TimeBased</option><option>QuantityBased</option></select></label>
      <label className="ow-field"><span>Adjustment method *</span><select value={form.adjustmentMode} onChange={(e) => setForm({ ...form, adjustmentMode: e.target.value })}><option value="discount">Discount percentage</option><option value="price">Fixed adjusted price</option></select></label>
      {form.adjustmentMode === "price" ? <label className="ow-field"><span>Adjusted price *</span><input type="number" min="0" value={form.adjustedPrice} onChange={(e) => setForm({ ...form, adjustedPrice: e.target.value })} /></label> : <label className="ow-field"><span>Discount % *</span><input type="number" min="0" max="100" value={form.discountPercent} onChange={(e) => setForm({ ...form, discountPercent: e.target.value })} /></label>}
      {form.ruleType === "QuantityBased" ? <label className="ow-field"><span>Sold-ticket threshold *</span><input type="number" min="1" value={form.quantityThreshold} onChange={(e) => setForm({ ...form, quantityThreshold: e.target.value })} /></label> : <>
        <label className="ow-field"><span>Starts</span><input type="datetime-local" value={form.triggerFrom} onChange={(e) => setForm({ ...form, triggerFrom: e.target.value })} /></label>
        <label className="ow-field"><span>Ends</span><input type="datetime-local" value={form.triggerTo} onChange={(e) => setForm({ ...form, triggerTo: e.target.value })} /></label></>}
    </div><button type="submit" className="tb-btn tb-btn-primary ow-btn-sm" disabled={saving}>{saving ? "Saving..." : "Add rule"}</button></form>}
  </section>;
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

function TicketTypesSection({ eventId, ticketTypes, chart, hasLayout, readOnly, onChanged }) {
  const [form, setForm] = useState(emptyTicket);
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState(null); // null = adding, else TicketTypeId being edited
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  // Which fields the user has actually interacted with — a field's error
  // only renders once it's touched (or a submit attempt was made), instead
  // of showing every error the moment the form opens.
  const [touched, setTouched] = useState({});

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
    setTouched((t) => ({ ...t, [name]: true }));
  };

  const handleBlur = (e) => {
    const { name } = e.target;
    setTouched((t) => ({ ...t, [name]: true }));
  };

  const startEdit = (t) => {
    setEditingId(t.ticketTypeId);
    setForm({
      typeName: t.typeName || "",
      description: t.description || "",
      price: t.price ?? "",
      originalPrice: t.originalPrice ?? "",
      quantity: t.quantity ?? "",
      minPerOrder: t.minPerOrder ?? 1,
      maxPerOrder: t.maxPerOrder ?? 10,
    });
    setError("");
    setTouched({});
    setShowForm(true);
  };

  const cancelEdit = () => {
    setEditingId(null);
    setForm(emptyTicket);
    setShowForm(false);
    setError("");
    setTouched({});
  };

  const duplicateName = useMemo(() => {
    const name = form.typeName.trim().toLowerCase();
    return (
      !!name &&
      ticketTypes.some(
        (t) => t.typeName.trim().toLowerCase() === name && t.ticketTypeId !== editingId
      )
    );
  }, [form.typeName, ticketTypes, editingId]);

  // TicketTypeIds that are actually referenced by at least one seating zone.
  // Must mirror the backend's real constraint (TicketTypeService.IsPlacedInLayoutAsync):
  // Quantity is only backend-managed for a ticket type that a zone points to —
  // NOT for every ticket type just because the concert happens to have a chart.
  const placedTicketTypeIds = useMemo(
    () => new Set((chart?.zones || []).map((z) => z.ticketTypeId)),
    [chart]
  );
  const isEditingPlacedTicketType = !!editingId && placedTicketTypeIds.has(editingId);

  // Dynamic, per-field validation: recomputed on every keystroke so each
  // field can show its own message as soon as it's touched, instead of
  // waiting for submit and surfacing a single generic banner.
  const fieldErrors = useMemo(() => {
    const errors = {};
    if (!form.typeName.trim()) errors.typeName = "Please enter a ticket type name.";
    else if (duplicateName) errors.typeName = "This ticket type name already exists for the concert.";

    if (form.price === "") errors.price = "Please enter a price (use 0 for free tickets).";
    else if (Number(form.price) < 0) errors.price = "Price cannot be negative.";

    if (form.originalPrice !== "" && Number(form.originalPrice) < 0) {
      errors.originalPrice = "Original price cannot be negative.";
    } else if (
      form.originalPrice !== "" &&
      form.price !== "" &&
      Number(form.originalPrice) < Number(form.price)
    ) {
      errors.originalPrice = "Original price should be at least the current price.";
    }

    if (form.quantity === "") errors.quantity = "Please enter a ticket quantity.";
    else if (Number(form.quantity) <= 0) errors.quantity = "Quantity must be greater than 0.";

    if (form.minPerOrder === "" || Number(form.minPerOrder) < 1) {
      errors.minPerOrder = "Min per order must be at least 1.";
    }
    if (form.maxPerOrder === "" || Number(form.maxPerOrder) < 1) {
      errors.maxPerOrder = "Max per order must be at least 1.";
    } else if (Number(form.maxPerOrder) < Number(form.minPerOrder)) {
      errors.maxPerOrder = "Max per order must be at least Min per order.";
    }

    return errors;
  }, [form, duplicateName]);

  const hasBlockingErrors = Object.keys(fieldErrors).length > 0;

  const handleAdd = async (e) => {
    e.preventDefault();
    // Reveal every field's error in case the user jumped straight to
    // submit without leaving/touching some of the fields.
    setTouched({
      typeName: true,
      price: true,
      originalPrice: true,
      quantity: true,
      minPerOrder: true,
      maxPerOrder: true,
    });
    if (hasBlockingErrors) {
      return;
    }

    setSaving(true);
    setError("");
    try {
      const payload = {
        typeName: form.typeName.trim(),
        description: form.description.trim() || null,
        price: Number(form.price) || 0,
        originalPrice: form.originalPrice === "" ? null : Number(form.originalPrice),
        quantity: Number(form.quantity),
        minPerOrder: Number(form.minPerOrder) || 1,
        maxPerOrder: Number(form.maxPerOrder) || 10,
      };

      if (editingId) {
        await ticketTypeApi.update(editingId, payload);
      } else {
        await ticketTypeApi.create(eventId, payload);
      }

      setForm(emptyTicket);
      setShowForm(false);
      setEditingId(null);
      await onChanged();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.join(" ")) ||
          err.response?.data?.message ||
          `Could not ${editingId ? "update" : "add"} the ticket type.`
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
            onClick={() => (showForm ? cancelEdit() : setShowForm(true))}
          >
            {showForm ? "Close" : "+ Add ticket type"}
          </button>
        )}
      </div>

      {hasLayout && (
        <p className="ow-hint">
          Ticket quantity is the quota. The total capacity of zones linked to a
          ticket type cannot exceed it and must match it before submission.
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
              {hasLayout && <th>Assigned to zones</th>}
              {hasLayout && <th>Remaining</th>}
              <th>Sold</th>
              <th>Per-order limit</th>
              {!readOnly && <th></th>}
            </tr>
          </thead>
          <tbody>
            {ticketTypes.map((t) => {
              const assigned = (chart?.zones || [])
                .filter((z) => z.ticketTypeId === t.ticketTypeId)
                .reduce((sum, z) => sum + (z.capacity || z.totalSeats || 0), 0);
              const remaining = t.quantity - assigned;
              return (
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
                {hasLayout && <td>{assigned}</td>}
                {hasLayout && (
                  <td className={remaining === 0 ? "ow-capacity-ok" : "ow-capacity-warning"}>
                    {remaining === 0 ? "✓ 0" : remaining}
                  </td>
                )}
                <td>{t.soldQuantity}</td>
                <td>
                  {t.minPerOrder}–{t.maxPerOrder}
                </td>
                {!readOnly && (
                  <td>
                    <button
                      type="button"
                      className="ow-link"
                      onClick={() => startEdit(t)}
                    >
                      Edit
                    </button>
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
              );
            })}
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
                onBlur={handleBlur}
                placeholder="e.g. VIP"
                maxLength={100}
                aria-invalid={!!(touched.typeName && fieldErrors.typeName)}
              />
              {touched.typeName && fieldErrors.typeName && (
                <span className="ow-field-error">{fieldErrors.typeName}</span>
              )}
            </label>
            <label className="ow-field">
              <span>Price (₫) *</span>
              <input
                type="number"
                min={0}
                name="price"
                value={form.price}
                onChange={handleChange}
                onBlur={handleBlur}
                aria-invalid={!!(touched.price && fieldErrors.price)}
              />
              {touched.price && fieldErrors.price && (
                <span className="ow-field-error">{fieldErrors.price}</span>
              )}
            </label>
            <label className="ow-field">
              <span>Original price (if discounted)</span>
              <input
                type="number"
                min={0}
                name="originalPrice"
                value={form.originalPrice}
                onChange={handleChange}
                onBlur={handleBlur}
                aria-invalid={!!(touched.originalPrice && fieldErrors.originalPrice)}
              />
              {touched.originalPrice && fieldErrors.originalPrice && (
                <span className="ow-field-error">{fieldErrors.originalPrice}</span>
              )}
            </label>
            <label className="ow-field">
              <span>Quantity *</span>
              <input
                type="number"
                min={1}
                name="quantity"
                value={form.quantity}
                onChange={handleChange}
                onBlur={handleBlur}
                disabled={isEditingPlacedTicketType}
                aria-invalid={!!(touched.quantity && fieldErrors.quantity)}
              />
              {touched.quantity && fieldErrors.quantity && (
                <span className="ow-field-error">{fieldErrors.quantity}</span>
              )}
            </label>
            <label className="ow-field">
              <span>Min per order</span>
              <input
                type="number"
                min={1}
                name="minPerOrder"
                value={form.minPerOrder}
                onChange={handleChange}
                onBlur={handleBlur}
                aria-invalid={!!(touched.minPerOrder && fieldErrors.minPerOrder)}
              />
              {touched.minPerOrder && fieldErrors.minPerOrder && (
                <span className="ow-field-error">{fieldErrors.minPerOrder}</span>
              )}
            </label>
            <label className="ow-field">
              <span>Max per order</span>
              <input
                type="number"
                min={1}
                name="maxPerOrder"
                value={form.maxPerOrder}
                onChange={handleChange}
                onBlur={handleBlur}
                aria-invalid={!!(touched.maxPerOrder && fieldErrors.maxPerOrder)}
              />
              {touched.maxPerOrder && fieldErrors.maxPerOrder && (
                <span className="ow-field-error">{fieldErrors.maxPerOrder}</span>
              )}
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
          <div className="ow-form-actions">
            <button
              type="submit"
              className="tb-btn tb-btn-primary ow-btn-sm"
              disabled={saving || hasBlockingErrors}
            >
              {saving ? "Saving..." : editingId ? "Save changes" : "Add ticket type"}
            </button>
            {editingId && (
              <button
                type="button"
                className="tb-btn tb-btn-outline ow-btn-sm"
                onClick={cancelEdit}
              >
                Cancel
              </button>
            )}
          </div>
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

function SeatingSection({ eventId, chart, ticketTypes, readOnly, onChanged, onSilentRefresh }) {
  const [mode, setMode] = useState(chart ? "assigned" : "general");
  const [form, setForm] = useState(emptyZone);
  const [showForm, setShowForm] = useState(false);
  const [editingZoneId, setEditingZoneId] = useState(null);
  // Snapshot of the Seated zone's grid (rows/seatsPerRow/rowLabelPrefix) at
  // the moment editing started, so handleAddZone can tell whether the user
  // actually changed the seat grid vs. just renaming/re-linking the zone.
  const [initialSeatedShape, setInitialSeatedShape] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [expandedZoneId, setExpandedZoneId] = useState(null);
  // Which fields the user has actually interacted with — a field's error only
  // renders once it's touched (or a submit attempt was made), same pattern as
  // the Ticket Types form above.
  const [touched, setTouched] = useState({});

  useEffect(() => {
    setMode(chart ? "assigned" : "general");
  }, [chart]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((f) => ({ ...f, [name]: value }));
    setTouched((t) => ({ ...t, [name]: true }));
  };

  const handleBlur = (e) => {
    const { name } = e.target;
    setTouched((t) => ({ ...t, [name]: true }));
  };

  const selectedTicket = ticketTypes.find((t) => t.ticketTypeId === Number(form.ticketTypeId));
  const enteredCapacity = form.zoneType === "Seated"
    ? Math.max(Number(form.rows) || 0, 0) * Math.max(Number(form.seatsPerRow) || 0, 0)
    : Math.max(Number(form.capacity) || 0, 0);

  // Dynamic, per-field validation: recomputed on every keystroke so each
  // field can show its own message as soon as it's touched, instead of
  // waiting for submit and surfacing a single generic banner.
  const fieldErrors = useMemo(() => {
    const errors = {};
    if (!form.zoneName.trim()) errors.zoneName = "Please enter a zone name.";
    if (!form.ticketTypeId) errors.ticketTypeId = "Please select a ticket type for this zone.";
    if (form.zoneType === "Standing") {
      if (form.capacity === "" || Number(form.capacity) <= 0) {
        errors.capacity = "Capacity must be greater than 0.";
      }
    } else {
      if (form.rows === "" || Number(form.rows) <= 0) {
        errors.rows = "Number of rows must be greater than 0.";
      }
      if (form.seatsPerRow === "" || Number(form.seatsPerRow) <= 0) {
        errors.seatsPerRow = "Seats per row must be greater than 0.";
      }
    }
    return errors;
  }, [form]);

  const hasBlockingErrors = Object.keys(fieldErrors).length > 0;

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

  const startEditZone = (zone) => {
    const rows = zone.zoneType === "Seated"
      ? new Set((zone.seats || []).map((seat) => seat.rowLabel)).size || 1 : 1;
    const seatsPerRow = zone.zoneType === "Seated" && rows
      ? Math.max(1, Math.round((zone.totalSeats || zone.capacity || 1) / rows)) : 1;
    const rowLabelPrefix = zone.seats?.[0]?.rowLabel || "A";
    setEditingZoneId(zone.seatZoneId);
    setForm({
      ticketTypeId: String(zone.ticketTypeId), zoneName: zone.zoneName,
      zoneType: zone.zoneType, rows, seatsPerRow,
      rowLabelPrefix,
      capacity: zone.zoneType === "Standing" ? zone.capacity : "",
    });
    // Only Seated zones have a grid whose change we need to detect; Standing
    // zones resize freely via `capacity`.
    setInitialSeatedShape(
      zone.zoneType === "Seated" ? { rows, seatsPerRow, rowLabelPrefix } : null
    );
    setShowForm(true);
    setError("");
    setTouched({});
  };

  const resetZoneForm = () => {
    setEditingZoneId(null);
    setForm(emptyZone);
    setInitialSeatedShape(null);
    setShowForm(false);
    setTouched({});
  };

  const handleAddZone = async (e) => {
    e.preventDefault();
    // Reveal every field's error in case the user jumped straight to submit
    // without leaving/touching some of the fields.
    setTouched({
      zoneName: true,
      ticketTypeId: true,
      capacity: true,
      rows: true,
      seatsPerRow: true,
    });
    if (hasBlockingErrors) {
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

    // Only send grid dimensions when they changed. This avoids deleting and
    // regenerating physical Seat rows during a simple rename or ticket-class change.
    const seatedShapeChanged =
      zoneDto.zoneType === "Seated" &&
      !!initialSeatedShape &&
      (zoneDto.rows !== initialSeatedShape.rows ||
        zoneDto.seatsPerRow !== initialSeatedShape.seatsPerRow ||
        zoneDto.rowLabelPrefix !== initialSeatedShape.rowLabelPrefix);

    setSaving(true);
    setError("");
    try {
      if (editingZoneId) {
        await seatingApi.updateZone(editingZoneId, {
          zoneName: zoneDto.zoneName,
          ticketTypeId: zoneDto.ticketTypeId,
          ...(zoneDto.zoneType === "Seated"
            ? (seatedShapeChanged
                ? { rows: zoneDto.rows, seatsPerRow: zoneDto.seatsPerRow, rowLabelPrefix: zoneDto.rowLabelPrefix }
                : {})
            : { capacity: zoneDto.capacity }),
        });
      } else if (!chart) {
        await seatingApi.build(eventId, {
          name: "Seating Chart",
          zones: [zoneDto],
        });
      } else {
        await seatingApi.addZone(eventId, zoneDto);
      }
      resetZoneForm();
      await onChanged();
    } catch (err) {
      setError(err.response?.data?.message || `Could not ${editingZoneId ? "update" : "add"} the zone.`);
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
  // Uses a SILENT refresh: the canvas already shows the new position/size
  // optimistically, so re-fetching with the full-page loading state here would
  // make the whole section flicker/reload on every single drag or resize.
  // Lưu nguyên geometry của zone (rectangle hoặc polygon) vào ShapeJson.
  // EventAPI đã dùng jsonb nên không cần đổi schema khi thêm loại hình mới.
  const handleZoneShapeChange = async (seatZoneId, shape) => {
    try {
      await seatingApi.updateZone(seatZoneId, {
        shapeJson: JSON.stringify(shape),
      });
      await onSilentRefresh();
    } catch (err) {
      setError(err.response?.data?.message || "Could not save the zone shape.");
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
          {!readOnly && (
            <SeatingTemplateBar
              eventId={eventId}
              chart={chart}
              ticketTypes={ticketTypes}
              onChanged={onChanged}
            />
          )}

          {chart && chart.zones?.length > 0 && (
            <>
              <p className="ow-hint">
                Drag the blocks below to arrange the overview layout — the
                same way buyers will see it when choosing tickets.
              </p>
              <ZoneMapCanvas
                key={chart.zones.map((z) => z.seatZoneId).join("-")}
                zones={chart.zones}
                ticketTypes={ticketTypes}
                readOnly={readOnly}
                onZoneShapeChange={handleZoneShapeChange}
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
                  <th>Seats</th>
                  {!readOnly && <th></th>}
                </tr>
              </thead>
              <tbody>
                {chart.zones.map((z) => (
                  <Fragment key={z.seatZoneId}>
                    <tr>
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
                      <td>
                        {z.zoneType === "Seated" && (
                          <button
                            type="button"
                            className="ow-link"
                            onClick={() =>
                              setExpandedZoneId((id) => (id === z.seatZoneId ? null : z.seatZoneId))
                            }
                          >
                            {expandedZoneId === z.seatZoneId ? "Hide seats" : "View seats"}
                          </button>
                        )}
                      </td>
                      {!readOnly && (
                        <td>
                          <button type="button" className="ow-link" onClick={() => startEditZone(z)}>
                            Edit
                          </button>
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
                    {expandedZoneId === z.seatZoneId && z.zoneType === "Seated" && (
                      <tr key={`${z.seatZoneId}-seats`}>
                        <td colSpan={readOnly ? 6 : 7}>
                          <SeatGridPreview mode="actual" seats={z.seats} />
                        </td>
                      </tr>
                    )}
                  </Fragment>
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
                        onBlur={handleBlur}
                        placeholder="e.g. VIP Stand"
                        maxLength={100}
                        aria-invalid={!!(touched.zoneName && fieldErrors.zoneName)}
                      />
                      {touched.zoneName && fieldErrors.zoneName && (
                        <span className="ow-field-error">{fieldErrors.zoneName}</span>
                      )}
                    </label>

                    <label className="ow-field">
                      <span>Zone type *</span>
                      <select
                        name="zoneType"
                        value={form.zoneType}
                        onChange={handleChange}
                        disabled={!!editingZoneId}
                      >
                        <option value="Seated">Seated (numbered)</option>
                        <option value="Standing">Standing (headcount only)</option>
                      </select>
                      {/* The backend has no "change zone type" operation (UpdateSeatZoneDTO
                          has no ZoneType field) — a Seated zone's identity is its generated
                          Seat rows, a Standing zone's is its bare headcount. Letting this
                          stay editable during Edit used to submit a payload shaped for the
                          NEW type against a zone that is still the OLD type underneath,
                          which the backend rejected with a confusing capacity/rows error. */}
                      {editingZoneId && (
                        <span className="ow-hint">
                          Zone type can't be changed after creation — delete this zone and add
                          a new one instead.
                        </span>
                      )}
                    </label>

                    <label className="ow-field">
                      <span>Applicable ticket type *</span>
                      <select
                        name="ticketTypeId"
                        value={form.ticketTypeId}
                        onChange={handleChange}
                        onBlur={handleBlur}
                        aria-invalid={!!(touched.ticketTypeId && fieldErrors.ticketTypeId)}
                      >
                        <option value="">-- Select ticket type --</option>
                        {ticketTypes.map((t) => (
                          <option key={t.ticketTypeId} value={t.ticketTypeId}>
                            {t.typeName}
                          </option>
                        ))}
                      </select>
                      {touched.ticketTypeId && fieldErrors.ticketTypeId ? (
                        <span className="ow-field-error">{fieldErrors.ticketTypeId}</span>
                      ) : selectedTicket ? (
                        <span className="ow-hint">
                          Zone capacity must stay within {selectedTicket.typeName}'s configured quantity.
                        </span>
                      ) : null}
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
                            onBlur={handleBlur}
                            aria-invalid={!!(touched.rows && fieldErrors.rows)}
                          />
                          {touched.rows && fieldErrors.rows && (
                            <span className="ow-field-error">{fieldErrors.rows}</span>
                          )}
                        </label>
                        <label className="ow-field">
                          <span>Seats per row *</span>
                          <input
                            type="number"
                            min={1}
                            name="seatsPerRow"
                            value={form.seatsPerRow}
                            onChange={handleChange}
                            onBlur={handleBlur}
                            aria-invalid={!!(touched.seatsPerRow && fieldErrors.seatsPerRow)}
                          />
                          {touched.seatsPerRow && fieldErrors.seatsPerRow && (
                            <span className="ow-field-error">{fieldErrors.seatsPerRow}</span>
                          )}
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
                          onBlur={handleBlur}
                          aria-invalid={!!(touched.capacity && fieldErrors.capacity)}
                        />
                        {touched.capacity && fieldErrors.capacity && (
                          <span className="ow-field-error">{fieldErrors.capacity}</span>
                        )}
                      </label>
                    )}
                  </div>

                  <div className="ow-hint">Calculated zone capacity: {enteredCapacity}. This is the source of truth for ticket quantity.</div>

                  {form.zoneType === "Seated" && (
                    <SeatGridPreview
                      mode="plan"
                      rows={form.rows}
                      seatsPerRow={form.seatsPerRow}
                      rowLabelPrefix={form.rowLabelPrefix}
                    />
                  )}

                  <div className="ow-inline-form-actions">
                    <button
                      type="button"
                      className="tb-btn tb-btn-outline ow-btn-sm"
                      onClick={() => {
                        resetZoneForm();
                      }}
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      className="tb-btn tb-btn-primary ow-btn-sm"
                      disabled={saving || hasBlockingErrors}
                    >
                      {saving ? "Saving..." : editingZoneId ? "Save zone" : "Add zone"}
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
// Seating templates (UC_26.2 Apply Seating Template / UC_26.3 Save as Template)
// =============================================================================

/**
 * Lets the organizer either draw zones by hand (the form below, unchanged) or
 * load a reusable layout from the template library and map its zones onto this
 * concert's ticket types — mirroring how Ticketbox/Ticketmaster-style editors
 * offer a template library alongside a blank canvas.
 */
function SeatingTemplateBar({ eventId, chart, ticketTypes, onChanged }) {
  const hasZones = !!(chart && chart.zones?.length > 0);

  const [pickerOpen, setPickerOpen] = useState(false);
  const [saveOpen, setSaveOpen] = useState(false);

  return (
    <div className="ow-template-bar">
      <div className="ow-template-bar-actions">
        {!hasZones && (
          <button
            type="button"
            className="tb-btn tb-btn-outline ow-btn-sm"
            onClick={() => setPickerOpen((v) => !v)}
          >
            {pickerOpen ? "Close template picker" : "📐 Use a saved template"}
          </button>
        )}
        {hasZones && (
          <button
            type="button"
            className="tb-btn tb-btn-outline ow-btn-sm"
            onClick={() => setSaveOpen((v) => !v)}
          >
            {saveOpen ? "Close" : "💾 Save this layout as a template"}
          </button>
        )}
      </div>

      {pickerOpen && !hasZones && (
        <TemplatePicker
          eventId={eventId}
          ticketTypes={ticketTypes}
          onApplied={async () => {
            setPickerOpen(false);
            await onChanged();
          }}
        />
      )}

      {saveOpen && hasZones && (
        <SaveAsTemplateForm
          eventId={eventId}
          onSaved={() => setSaveOpen(false)}
        />
      )}
    </div>
  );
}

function TemplatePicker({ eventId, ticketTypes, onApplied }) {
  const [templates, setTemplates] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [selected, setSelected] = useState(null); // full SeatingTemplateResponseDTO
  const [mappings, setMappings] = useState({}); // zoneIndex -> editable zone copy
  const [applying, setApplying] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError("");
      try {
        const res = await seatingTemplateApi.getVisible();
        if (!cancelled) setTemplates(res.data?.data || []);
      } catch (err) {
        if (!cancelled) {
          setError(err.response?.data?.message || "Could not load templates.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const openTemplate = async (id) => {
    setError("");
    try {
      const res = await seatingTemplateApi.getById(id);
      const full = res.data?.data;
      setSelected(full);
      setMappings(Object.fromEntries((full.zones || []).map((zone, index) => [index, {
        ticketTypeId: "", zoneName: zone.zoneName, zoneType: zone.zoneType,
        rows: zone.rows || 1, seatsPerRow: zone.seatsPerRow || 1,
        rowLabelPrefix: zone.rowLabelPrefix || "A", capacity: zone.capacity || 1,
        shapeJson: zone.shapeJson || null,
      }])));
    } catch (err) {
      setError(err.response?.data?.message || "Could not load this template.");
    }
  };

  const handleApply = async () => {
    if (!selected) return;
    const zoneMappings = selected.zones.map((_, index) => ({ zoneIndex: index,
      ...mappings[index], ticketTypeId: Number(mappings[index]?.ticketTypeId) || 0,
      rows: Number(mappings[index]?.rows) || 1,
      seatsPerRow: Number(mappings[index]?.seatsPerRow) || 1,
      capacity: Number(mappings[index]?.capacity) || 1,
    }));

    if (zoneMappings.some((m) => !m.ticketTypeId)) {
      setError("Map every zone to a ticket type before applying the template.");
      return;
    }

    setApplying(true);
    setError("");
    try {
      await seatingTemplateApi.apply(selected.seatingTemplateId, eventId, {
        zoneMappings,
      });
      await onApplied();
    } catch (err) {
      const apiErrors = err.response?.data?.errors;
      setError(
        (apiErrors && apiErrors.join(" ")) ||
          err.response?.data?.message ||
          "Could not apply the template."
      );
    } finally {
      setApplying(false);
    }
  };

  if (loading) return <div className="tb-loading">Loading templates...</div>;

  return (
    <div className="ow-template-picker">
      {error && <div className="ow-error">{error}</div>}

      {!selected && (
        <>
          {templates.length === 0 && (
            <div className="ow-empty-row">
              No templates yet. Build a layout by hand once, then save it as a
              template to reuse it on future concerts.
            </div>
          )}
          {templates.length > 0 && (
            <div className="ow-template-grid">
              {templates.map((t) => (
                <button
                  type="button"
                  key={t.seatingTemplateId}
                  className="ow-template-card"
                  onClick={() => openTemplate(t.seatingTemplateId)}
                >
                  <div className="ow-template-card-title">
                    {t.name}
                    {t.isPublic && !t.isMine && (
                      <span className="ow-template-badge">Shared</span>
                    )}
                  </div>
                  {t.description && (
                    <div className="ow-hint">{t.description}</div>
                  )}
                  <div className="ow-hint">
                    {t.totalZones} zone(s) · ~{t.estimatedCapacity} capacity
                  </div>
                </button>
              ))}
            </div>
          )}
        </>
      )}

      {selected && (
        <div className="ow-template-mapping">
          <p className="ow-hint">
            Customize every zone in <strong>{selected.name}</strong>, map it to a
            ticket type, then apply. The mapped zone capacity must stay within that ticket type's Quantity quota.
          </p>

          {ticketTypes.length === 0 && (
            <div className="ow-error">
              Add at least one ticket type before applying a template.
            </div>
          )}

          <table className="ow-table">
            <thead>
              <tr>
                <th>Zone name</th>
                <th>Type</th>
                <th>Rows / Capacity</th>
                <th>Ticket type *</th>
              </tr>
            </thead>
            <tbody>
              {selected.zones.map((z, index) => (
                <tr key={index}>
                  <td><input value={mappings[index]?.zoneName || ""} onChange={(e) => setMappings((m) => ({ ...m, [index]: { ...m[index], zoneName: e.target.value } }))} /></td>
                  <td>
                    <span
                      className={
                        "ow-zone-badge " +
                        (z.zoneType === "Seated" ? "ow-zone-seated" : "ow-zone-standing")
                      }
                    >
                      {mappings[index]?.zoneType === "Seated" ? "Seated" : "Standing"}
                    </span>
                    <select value={mappings[index]?.zoneType || z.zoneType} onChange={(e) => setMappings((m) => ({ ...m, [index]: { ...m[index], zoneType: e.target.value } }))}>
                      <option value="Seated">Seated</option><option value="Standing">Standing</option>
                    </select>
                  </td>
                  <td>{mappings[index]?.zoneType === "Seated" ? <div className="ow-template-dimensions">
                    <input type="number" min="1" aria-label="Rows" value={mappings[index]?.rows || 1} onChange={(e) => setMappings((m) => ({ ...m, [index]: { ...m[index], rows: e.target.value } }))} />
                    <span>×</span><input type="number" min="1" aria-label="Seats per row" value={mappings[index]?.seatsPerRow || 1} onChange={(e) => setMappings((m) => ({ ...m, [index]: { ...m[index], seatsPerRow: e.target.value } }))} />
                  </div> : <input type="number" min="1" aria-label="Capacity" value={mappings[index]?.capacity || 1} onChange={(e) => setMappings((m) => ({ ...m, [index]: { ...m[index], capacity: e.target.value } }))} />}</td>
                  <td>
                    <select
                      value={mappings[index]?.ticketTypeId || ""}
                      onChange={(e) =>
                        setMappings((m) => ({ ...m, [index]: { ...m[index], ticketTypeId: e.target.value } }))
                      }
                    >
                      <option value="">-- Select ticket type --</option>
                      {ticketTypes.map((tt) => (
                        <option key={tt.ticketTypeId} value={tt.ticketTypeId}>
                          {tt.typeName}
                        </option>
                      ))}
                    </select>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="ow-inline-form-actions">
            <button
              type="button"
              className="tb-btn tb-btn-outline ow-btn-sm"
              onClick={() => setSelected(null)}
            >
              ← Back to templates
            </button>
            <button
              type="button"
              className="tb-btn tb-btn-primary ow-btn-sm"
              onClick={handleApply}
              disabled={applying || ticketTypes.length === 0}
            >
              {applying ? "Applying..." : "Apply template"}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function SaveAsTemplateForm({ eventId, onSaved }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  const handleSave = async (e) => {
    e.preventDefault();
    if (!name.trim()) {
      setError("Please name this template.");
      return;
    }

    setSaving(true);
    setError("");
    setSuccess("");
    try {
      await seatingTemplateApi.saveFromEvent({
        eventId,
        name: name.trim(),
        description: description.trim() || null,
      });
      setSuccess("Saved! You'll find it in the template picker on future concerts.");
      setName("");
      setDescription("");
      onSaved();
    } catch (err) {
      setError(err.response?.data?.message || "Could not save this layout as a template.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <form className="ow-inline-form" onSubmit={handleSave}>
      {error && <div className="ow-error">{error}</div>}
      {success && <div className="ow-banner ow-banner-pending">{success}</div>}
      <div className="ow-grid">
        <label className="ow-field ow-span-2">
          <span>Template name *</span>
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Standard theater layout"
            maxLength={150}
          />
        </label>
        <label className="ow-field ow-span-2">
          <span>Description</span>
          <input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={500}
          />
        </label>
      </div>
      <button
        type="submit"
        className="tb-btn tb-btn-primary ow-btn-sm"
        disabled={saving}
      >
        {saving ? "Saving..." : "Save as template"}
      </button>
    </form>
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
  const [editingId, setEditingId] = useState(null);

  const startEdit = (policy) => {
    setEditingId(policy.refundPolicyId);
    setForm({ policyName: policy.policyName, description: policy.description || "",
      deadlineBeforeEventHours: policy.deadlineBeforeEventHours, refundPercent: policy.refundPercent,
      requiresOrganizerApproval: !!policy.requiresOrganizerApproval });
    setShowForm(true); setError("");
  };

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
    if (refundPolicies.some((p) => p.refundPolicyId !== editingId && Number(p.deadlineBeforeEventHours) === Number(form.deadlineBeforeEventHours))) {
      setError("Another refund policy already uses this cutoff deadline."); return;
    }

    setSaving(true);
    setError("");
    try {
      const payload = {
        policyName: form.policyName.trim(),
        description: form.description.trim() || null,
        deadlineBeforeEventHours: Number(form.deadlineBeforeEventHours) || 1,
        refundPercent: Number(form.refundPercent) || 0,
        requiresOrganizerApproval: !!form.requiresOrganizerApproval,
        isActive: true,
      };
      if (editingId) await refundPolicyApi.update(editingId, payload);
      else await refundPolicyApi.create(eventId, payload);
      setForm(emptyPolicy);
      setEditingId(null);
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
                    <button type="button" className="ow-link" onClick={() => startEdit(p)}>Edit</button>
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
            {saving ? "Saving..." : editingId ? "Save policy" : "Add policy"}
          </button>
        </form>
      )}
    </section>
  );
}
