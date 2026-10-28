import { useEffect, useMemo, useState } from "react";
import ticketApi from "../api/ticketApi";
import "./ChangeSeatSelectionModal.css";
import { useToast } from "./ToastProvider";

const readData = (response) => response?.data?.data ?? response?.data;

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

  useEffect(() => {
    if (!open) return undefined;

    let cancelled = false;

    async function loadSeatAvailability() {
      setLoading(true);
      setAvailabilityError("");
      setSaveError("");
      setSelectedSeatIds((currentSeatIds ?? []).map(Number));
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

    setSelectedSeatIds((current) => {
      if (current.includes(seatId)) {
        return current.filter((id) => id !== seatId);
      }

      const required = requiredCounts.get(ticketTypeId) ?? 0;
      const selectedForType = current.filter(
        (id) =>
          seatById.get(Number(id))?.ticketTypeId === ticketTypeId
      ).length;

      if (selectedForType >= required) return current;

      return [...current, seatId];
    });
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
      setSaveError(
        saveError.response?.data?.message ||
          saveError.message ||
          "Could not change seats. Your current seats are still held."
      );
      setSaveError(message);
      toast.error(message);
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

        <div className="cssm-legend" aria-label="Seat status legend">
          <span>
            <i className="is-available" /> Available
          </span>
          <span>
            <i className="is-selected" /> Selected
          </span>
          <span>
            <i className="is-held" /> Held
          </span>
          <span>
            <i className="is-sold" /> Sold
          </span>
        </div>

        <div className="cssm-stage">STAGE</div>

        <div className="cssm-zones">
          {zones.map((zone) => {
            const ticketTypeId = Number(zone.ticketTypeId);
            const columns = Math.max(
              1,
              ...zone.seats.map(
                (seat) => Number(seat.xCoordinate) || 1
              )
            );
            const rows = Math.max(
              1,
              ...zone.seats.map(
                (seat) => Number(seat.yCoordinate) || 1
              )
            );

            return (
              <section className="cssm-zone" key={zone.seatZoneId}>
                <div className="cssm-zone-heading">
                  <strong>{zone.zoneName}</strong>
                  <span>
                    {selectedCounts.get(ticketTypeId) ?? 0}/
                    {requiredCounts.get(ticketTypeId) ?? 0} selected
                  </span>
                </div>

                <div
                  className="cssm-seat-grid"
                  style={{
                    gridTemplateColumns: `repeat(${columns}, minmax(22px, 1fr))`,
                    gridTemplateRows: `repeat(${rows}, 30px)`,
                  }}
                >
                  {zone.seats.map((seat) => {
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
                        className={`cssm-seat ${
                          isSelected
                            ? "is-selected"
                            : isSold
                              ? "is-sold"
                              : isHeldByOther || statusUnavailable
                                ? "is-held"
                                : "is-available"
                        }`}
                        style={{
                          gridColumn:
                            Number(seat.xCoordinate) || 1,
                          gridRow:
                            Number(seat.yCoordinate) || 1,
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
                      >
                        {seat.seatNumber}
                      </button>
                    );
                  })}
                </div>
              </section>
            );
          })}

          <div
            style={{
              padding: "16px",
              borderTop: "1px solid #333",
            }}
          >
            <h3 style={{ margin: "0 0 12px" }}>
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
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "12px",
                    margin: "10px 0",
                  }}
                >
                  <span style={{ minWidth: "180px" }}>
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
          </div>
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
              {saving ? "Saving seats…" : "Save changes"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}