import { useEffect, useMemo, useState } from "react";
import ticketApi from "../api/ticketApi";
import "./ChangeSeatSelectionModal.css";
import { useToast } from "./ToastProvider";

const readData = (response) => response?.data?.data ?? response?.data;

const ZONE_PALETTE = [
  "#d98f14", "#14a89a", "#a855f7", "#3b82f6", "#d63a3a",
  "#1eaa55", "#e8439c", "#78b014", "#1da8c8", "#e06a1a",
];

function parseShape(shapeJson) {
  try {
    return JSON.parse(shapeJson || "{}");
  } catch {
    return {};
  }
}

function getZonePosition(shapeJson, index, total) {
  let shape = parseShape(shapeJson);
  const hasPolygon = shape?.type === "polygon" && Array.isArray(shape.points) && shape.points.length > 2;
  const hasRectangle = typeof shape.x === "number" && typeof shape.y === "number" && typeof shape.w === "number" && typeof shape.h === "number";
  if (!hasPolygon && !hasRectangle) {
    const columns = Math.min(4, Math.max(total, 1));
    const column = index % columns;
    const row = Math.floor(index / columns);
    const width = 90 / columns - 3;
    shape = { x: 5 + column * (90 / columns), y: 28 + row * 22, w: width, h: 16 };
  }

  if (shape.type === "polygon" && Array.isArray(shape.points) && shape.points.length > 2) {
    const xs = shape.points.map((point) => Number(point.x));
    const ys = shape.points.map((point) => Number(point.y));
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    const width = Math.max(maxX - minX, 1);
    const height = Math.max(maxY - minY, 1);
    const points = shape.points.map((point) => `${((Number(point.x) - minX) / width) * 100}% ${((Number(point.y) - minY) / height) * 100}%`).join(", ");
    const expandedLeft = Math.max(0, minX - 5);
    const expandedTop = Math.max(0, minY - 2);
    const expandedWidth = Math.min(width + 10, 100 - expandedLeft);
    const expandedHeight = Math.min(height + 12, 100 - expandedTop);
    return { left: `${expandedLeft}%`, top: `${expandedTop}%`, width: `${expandedWidth}%`, height: `${expandedHeight}%`, clipPath: `polygon(${points})` };
  }

  const originalLeft = Math.max(0, Math.min(Number(shape.x) || 0, 99));
  const originalTop = Math.max(0, Math.min(Number(shape.y) || 0, 99));
  const left = Math.max(0, originalLeft - 5);
  const top = Math.max(0, originalTop - 2);
  const width = Math.max(1, Math.min((Number(shape.w) || 20) + 10, 100 - left));
  const height = Math.max(1, Math.min((Number(shape.h) || 16) + 12, 100 - top));
  return {
    left: `${left}%`,
    top: `${top}%`,
    width: `${width}%`,
    height: `${height}%`,
    transform: `rotate(${Number(shape.rot) || 0}deg)`,
  };
}

function getSeatCanvasMinimumHeight(zones) {
  const minimumSeatPitch = 14;
  let minHeight = 440;
  zones.forEach((zone, index) => {
    const position = getZonePosition(zone.shapeJson, index, zones.length);
    const heightPercent = Number.parseFloat(position.height) || 16;
    const rows = Math.max(1, ...(zone.seats ?? []).map((seat) => Number(seat.yCoordinate) || 1));
    minHeight = Math.max(minHeight, (rows * minimumSeatPitch) / ((78 * heightPercent) / 10000));
  });
  return Math.ceil(minHeight);
}

export default function ChangeSeatSelectionModal({
  open,
  holdId,
  eventData,
  holdItems,
  attendees,
  currentSeatIds,
  onClose,
  onSaved,
}) {
  const [selectedSeatIds, setSelectedSeatIds] = useState([]);
  const [attendeeSeatIds, setAttendeeSeatIds] = useState([]);
  const [bookedSeatIds, setBookedSeatIds] = useState([]);
  const [heldSeatIds, setHeldSeatIds] = useState([]);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [availabilityError, setAvailabilityError] = useState("");
  const [saveError, setSaveError] = useState("");
  const [seatMapZoom, setSeatMapZoom] = useState(1);
  const toast = useToast();

  const zones = useMemo(
    () =>
      (eventData?.seatingChart?.zones ?? []).filter(
        (zone) =>
          zone.zoneType !== "Standing" &&
          (zone.seats ?? []).length > 0
      ),
    [eventData]
  );

  const seatById = useMemo(() => {
    const result = new Map();

    for (const zone of zones) {
      for (const seat of zone.seats ?? []) {
        result.set(Number(seat.seatId), {
          ...seat,
          ticketTypeId: Number(zone.ticketTypeId),
          zoneName: zone.zoneName,
        });
      }
    }

    return result;
  }, [zones]);

  const requiredCounts = useMemo(
    () =>
      new Map(
        (holdItems ?? []).map((item) => [
          Number(item.ticketTypeId),
          Number(item.quantity),
        ])
      ),
    [holdItems]
  );

  const currentSeatSet = useMemo(
    () => new Set((currentSeatIds ?? []).map(Number)),
    [currentSeatIds]
  );
  const seatCanvasMinHeight = useMemo(
    () => getSeatCanvasMinimumHeight(zones),
    [zones]
  );

  useEffect(() => {
    if (!open) return undefined;

    let cancelled = false;

    async function loadSeatAvailability() {
      setLoading(true);
      setAvailabilityError("");
      setSaveError("");
      setSelectedSeatIds((currentSeatIds ?? []).map(Number));
      setSeatMapZoom(1);
      setAttendeeSeatIds(
        (attendees ?? []).map((attendee) =>
          attendee.seatId == null ? "" : String(attendee.seatId)
        )
      );

      const allSeatIds = zones.flatMap((zone) =>
        (zone.seats ?? []).map((seat) => Number(seat.seatId))
      );

      if (!allSeatIds.length) {
        setAvailabilityError(
          "This event does not have an assigned seating chart."
        );
        setLoading(false);
        return;
      }

      try {
        const [bookedResponse, heldResponse] = await Promise.all([
          ticketApi.getBookedSeatIds(eventData.eventId),
          ticketApi.getHeldSeatIds(eventData.eventId, allSeatIds),
        ]);

        const booked = readData(bookedResponse);
        const held = readData(heldResponse);

        if (!Array.isArray(booked) || !Array.isArray(held)) {
          throw new Error("Seat availability response was invalid.");
        }

        if (!cancelled) {
          setBookedSeatIds(booked.map(Number));
          setHeldSeatIds(held.map(Number));
        }
      } catch (loadError) {
        if (!cancelled) {
          setAvailabilityError(
            loadError.response?.data?.message ||
              loadError.message ||
              "Could not verify seat availability."
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadSeatAvailability();

    return () => {
      cancelled = true;
    };
  }, [open, currentSeatIds, eventData, zones, attendees]);

  useEffect(() => {
    if (!open) return undefined;

    const onKeyDown = (event) => {
      if (event.key === "Escape" && !saving) {
        onClose();
      }
    };

    window.addEventListener("keydown", onKeyDown);

    return () => window.removeEventListener("keydown", onKeyDown);
  }, [open, saving, onClose]);

  const selectedCounts = useMemo(() => {
    const counts = new Map();

    for (const seatId of selectedSeatIds) {
      const seat = seatById.get(Number(seatId));

      if (seat) {
        counts.set(
          seat.ticketTypeId,
          (counts.get(seat.ticketTypeId) ?? 0) + 1
        );
      }
    }

    return counts;
  }, [selectedSeatIds, seatById]);

  // Used to display and validate the selected seats.
  // The API request is ordered by attendee below.
  const orderedSeatIds = useMemo(() => {
    const selected = new Set(selectedSeatIds.map(Number));
    const ordered = [];

    for (const item of holdItems ?? []) {
      const ticketTypeId = Number(item.ticketTypeId);

      for (const zone of zones) {
        if (Number(zone.ticketTypeId) !== ticketTypeId) continue;

        for (const seat of zone.seats ?? []) {
          const seatId = Number(seat.seatId);
          if (selected.has(seatId)) ordered.push(seatId);
        }
      }
    }

    return ordered;
  }, [holdItems, selectedSeatIds, zones]);

  const selectionIsComplete =
    orderedSeatIds.length === (currentSeatIds ?? []).length &&
    [...requiredCounts].every(
      ([ticketTypeId, quantity]) =>
        (selectedCounts.get(ticketTypeId) ?? 0) === quantity
    );

  const attendeeAssignmentsAreValid =
    (attendees ?? []).length === orderedSeatIds.length &&
    attendeeSeatIds.length === (attendees ?? []).length &&
    (attendees ?? []).every((attendee, index) => {
      const seatId = Number(attendeeSeatIds[index]);
      const seat = seatById.get(seatId);

      return (
        seat &&
        Number(seat.ticketTypeId) === Number(attendee.ticketTypeId)
      );
    }) &&
    new Set(attendeeSeatIds.map(Number)).size ===
      (attendees ?? []).length &&
    new Set(attendeeSeatIds.map(Number)).size === orderedSeatIds.length &&
    orderedSeatIds.every((seatId) =>
      attendeeSeatIds.map(Number).includes(Number(seatId))
    );

  if (!open) return null;

  const toggleSeat = (seat) => {
    const seatId = Number(seat.seatId);
    const ticketTypeId = Number(seat.ticketTypeId);

    if (selectedSeatIds.includes(seatId)) {
      setSelectedSeatIds((current) => current.filter((id) => id !== seatId));
      setAttendeeSeatIds((attendees ?? []).map(() => ""));
      return;
    }

    const required = requiredCounts.get(ticketTypeId) ?? 0;
    const selectedForType = selectedSeatIds.filter(
      (id) => seatById.get(Number(id))?.ticketTypeId === ticketTypeId
    ).length;

    if (selectedForType >= required) return;

    setSelectedSeatIds((current) => [...current, seatId]);
    setAttendeeSeatIds((attendees ?? []).map(() => ""));
  };

  const getTicketColor = (ticketTypeId) => {
    const ticketType = (eventData?.ticketTypes ?? []).find(
      (item) => Number(item.ticketTypeId) === Number(ticketTypeId)
    );
    return ticketType?.colorCode || ZONE_PALETTE[(Number(ticketTypeId) || 0) % ZONE_PALETTE.length];
  };

  const saveSelection = async () => {
    if (
      !selectionIsComplete ||
      !attendeeAssignmentsAreValid ||
      saving ||
      loading ||
      availabilityError
    ) {
      return;
    }

    try {
      setSaving(true);
      setSaveError("");

      // Backend maps SeatIds[i] to Attendees[i].SeatId.
      const seatsInAttendeeOrder = attendeeSeatIds.map(Number);

      const response = await ticketApi.changeHoldSeats(
        holdId,
        seatsInAttendeeOrder
      );

      const updatedHold = readData(response);
      toast.success("Seats and attendee assignments updated successfully.");
      onSaved(updatedHold);
      onClose();
    } catch (saveError) {
      const errMsg =
        saveError.response?.data?.message ||
        saveError.message ||
        "Could not change seats. Your current seats are still held.";
      setSaveError(errMsg);
      toast.error(errMsg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div
      className="cssm-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !saving) {
          onClose();
        }
      }}
    >
      <section
        className="cssm-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="cssm-title"
      >
        <header className="cssm-header">
          <div>
            <p className="cssm-eyebrow">
              UPDATE YOUR RESERVED SEATS
            </p>
            <h2 id="cssm-title">Change Seat Selection</h2>
            <p className="cssm-intro">
              Keep the same ticket types and quantities. Your current
              seats remain held until the new selection is saved.
            </p>
          </div>

          <button
            type="button"
            className="cssm-close"
            aria-label="Close seat selection"
            disabled={saving}
            onClick={onClose}
          >
            ×
          </button>
        </header>

        <div className="cssm-content">
          <section className="cssm-seat-panel" aria-label="Choose replacement seats">
            <div className="cssm-legend" aria-label="Seat status legend">
              <span><i className="is-available" /> Available</span>
              <span><i className="is-selected" /> Selected</span>
              <span><i className="is-held" /> Held</span>
              <span><i className="is-sold" /> Sold</span>
            </div>

          <div className={`cssm-map${seatMapZoom > 1 ? " is-zoomed" : ""}`}>
            <div className="cssm-map-controls" aria-label="Seat map zoom controls">
              <button type="button" aria-label="Zoom out" disabled={seatMapZoom <= 1} onClick={() => setSeatMapZoom((zoom) => Math.max(1, Number((zoom - 0.25).toFixed(2))))}>-</button>
              <button type="button" className="cssm-map-zoom-level" aria-label="Reset seat map zoom" onClick={() => setSeatMapZoom(1)}>{Math.round(seatMapZoom * 100)}%</button>
              <button type="button" aria-label="Zoom in" disabled={seatMapZoom >= 2.5} onClick={() => setSeatMapZoom((zoom) => Math.min(2.5, Number((zoom + 0.25).toFixed(2))))}>+</button>
            </div>
            <div className="cssm-map-viewport">
              <div className="cssm-seat-canvas" style={{ width: `${seatMapZoom * 100}%`, minHeight: `${Math.min(seatCanvasMinHeight, 320) * seatMapZoom}px` }}>
                <div className="cssm-stage">STAGE</div>
                {zones.map((zone, zoneIndex) => {
                  const ticketTypeId = Number(zone.ticketTypeId);
                  const columns = Math.max(1, ...(zone.seats ?? []).map((seat) => Number(seat.xCoordinate) || 1));
                  const rows = Math.max(1, ...(zone.seats ?? []).map((seat) => Number(seat.yCoordinate) || 1));
                  const columnCoordinates = [...new Set((zone.seats ?? []).map((seat) => Number(seat.xCoordinate) || 1))].sort((a, b) => a - b);
                  const rowLabels = [...new Map((zone.seats ?? []).map((seat) => [Number(seat.yCoordinate) || 1, seat.rowLabel || String(Number(seat.yCoordinate) || 1)])).entries()].sort(([a], [b]) => a - b);
                  const color = getTicketColor(ticketTypeId);

                  return (
                    <section
                      className="cssm-zone"
                      key={zone.seatZoneId}
                      style={{
                        ...getZonePosition(zone.shapeJson, zoneIndex, zones.length),
                        "--zone-color": color,
                        "--seat-columns": columns,
                        "--seat-rows": rows,
                      }}
                    >
                      <div className="cssm-zone-heading">
                        <strong>{zone.zoneName || "Seating zone"}</strong>
                        <span>{selectedCounts.get(ticketTypeId) ?? 0}/{requiredCounts.get(ticketTypeId) ?? 0} selected</span>
                      </div>
                      <div className="cssm-seat-grid">
                        {columnCoordinates.map((column) => (
                          <span key={`${zone.seatZoneId}-column-${column}`} className="cssm-coordinate is-column" style={{ left: `${1.5 + ((column - 0.5) * 97) / columns}%` }} aria-hidden="true">{column}</span>
                        ))}
                        {rowLabels.map(([row, label]) => (
                          <span key={`${zone.seatZoneId}-row-${row}`} className="cssm-coordinate is-row" style={{ top: `${30 + ((row - 0.5) * 65) / rows}%` }} aria-hidden="true">{label}</span>
                        ))}
                        {(zone.seats ?? []).map((seat) => {
                    const seatId = Number(seat.seatId);
                    const isSelected =
                      selectedSeatIds.includes(seatId);
                    const isOwnSeat = currentSeatSet.has(seatId);

                    const isSold =
                      bookedSeatIds.includes(seatId) ||
                      (!isOwnSeat &&
                        ["sold", "reserved", "blocked"].includes(
                          String(seat.status).toLowerCase()
                        ));

                    const isHeldByOther =
                      !isOwnSeat && heldSeatIds.includes(seatId);

                    const statusUnavailable =
                      !isOwnSeat &&
                      String(seat.status).toLowerCase() !== "available";

                    const disabled =
                      loading ||
                      Boolean(availabilityError) ||
                      isSold ||
                      isHeldByOther ||
                      statusUnavailable ||
                      (!isSelected &&
                        (selectedCounts.get(ticketTypeId) ?? 0) >=
                          (requiredCounts.get(ticketTypeId) ?? 0));

                    return (
                      <button
                        key={seatId}
                        type="button"
                        className={`cssm-seat-dot ${
                          isSelected
                            ? "is-selected"
                            : isSold
                              ? "is-sold"
                              : isHeldByOther || statusUnavailable
                                ? "is-held"
                                : "is-available"
                        }`}
                        style={{
                          left: `${1.5 + (((Number(seat.xCoordinate) || 1) - 0.5) * 97) / columns}%`,
                          top: `${30 + (((Number(seat.yCoordinate) || 1) - 0.5) * 65) / rows}%`,
                        }}
                        title={`Row ${seat.rowLabel}, Seat ${seat.seatNumber}`}
                        aria-label={`Row ${seat.rowLabel}, Seat ${seat.seatNumber}${
                          isSelected ? ", selected" : ""
                        }`}
                        aria-pressed={isSelected}
                        disabled={disabled}
                        onClick={() => {
                          setSaveError("");
                          toggleSeat({
                            ...seat,
                            ticketTypeId,
                          });
                        }}
                      />
                    );
                  })}
                      </div>
                    </section>
                  );
                })}
              </div>
            </div>
          </div>
          </section>

          <aside className="cssm-attendees">
            <h3 className="cssm-attendees-title">
              Assign attendees to seats
            </h3>

            {(attendees ?? []).map((attendee, index) => {
              const assignedSeatId = String(
                attendeeSeatIds[index] ?? ""
              );
              const assignedSeat = seatById.get(
                Number(assignedSeatId)
              );

              const availableSeatsForAttendee = orderedSeatIds
                .map((seatId) => seatById.get(Number(seatId)))
                .filter(
                  (seat) =>
                    seat &&
                    Number(seat.ticketTypeId) ===
                      Number(attendee.ticketTypeId) &&
                    !attendeeSeatIds.some(
                      (otherSeatId, otherIndex) =>
                        otherIndex !== index &&
                        Number(otherSeatId) ===
                          Number(seat.seatId)
                    )
                );

              if (
                assignedSeat &&
                Number(assignedSeat.ticketTypeId) ===
                  Number(attendee.ticketTypeId) &&
                !availableSeatsForAttendee.some(
                  (seat) =>
                    Number(seat.seatId) ===
                    Number(assignedSeat.seatId)
                )
              ) {
                availableSeatsForAttendee.push(assignedSeat);
              }

              const attendeeName =
                attendee.fullName?.trim() ||
                `Attendee ${index + 1}`;

              return (
                <label
                  key={`${attendee.ticketTypeId}-${index}`}
                  className="cssm-attendee-row"
                >
                  <span>
                    {attendeeName}
                    {attendee.isPrimaryBuyer
                      ? " (Account holder)"
                      : ""}
                  </span>

                  <select
                    value={assignedSeatId}
                    onChange={(event) => {
                      const updated = [...attendeeSeatIds];
                      updated[index] = event.target.value;
                      setAttendeeSeatIds(updated);
                      setSaveError("");
                    }}
                  >
                    <option value="">Select a seat</option>

                    {availableSeatsForAttendee.map((seat) => (
                      <option
                        key={seat.seatId}
                        value={String(seat.seatId)}
                      >
                        {seat.zoneName || "Zone"} · Row{" "}
                        {seat.rowLabel} · Seat {seat.seatNumber}
                      </option>
                    ))}
                  </select>
                </label>
              );
            })}

            {!attendeeAssignmentsAreValid && (
              <p className="cssm-error">
                Assign each attendee to a different selected seat in the
                matching ticket type.
              </p>
            )}
          </aside>
        </div>

        <div className="cssm-footer">
          <div>
            <strong>
              {orderedSeatIds.length} / {currentSeatIds.length} seats
            </strong>

            {(availabilityError || saveError) && (
              <p className="cssm-error" role="alert">
                {availabilityError || saveError}
              </p>
            )}
          </div>

          <div className="cssm-actions">
            <button
              type="button"
              className="cssm-cancel"
              disabled={saving}
              onClick={onClose}
            >
              Keep current seats
            </button>

            <button
              type="button"
              className="cssm-save"
              disabled={
                !selectionIsComplete ||
                !attendeeAssignmentsAreValid ||
                loading ||
                saving ||
                Boolean(availabilityError)
              }
              onClick={saveSelection}
            >
              {saving ? <span className="app-loading-spinner" role="status" aria-label="Loading" /> : "Save changes"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
