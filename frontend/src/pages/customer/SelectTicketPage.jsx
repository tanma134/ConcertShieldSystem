import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import eventApi from "../../api/eventApi";
import ticketApi from "../../api/ticketApi";
import "./SelectTicketPage.css";
import Header from "../../components/Header";
import Footer from "../../components/Footer";

const ZONE_PALETTE = [
  "#f59e0b",
  "#14b8a6",
  "#a855f7",
  "#3b82f6",
  "#ef4444",
  "#22c55e",
  "#ec4899",
  "#84cc16",
  "#06b6d4",
  "#f97316",
];

const formatPrice = (price) =>
  new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(Number(price) || 0);

const formatEventTime = (value) =>
  value
    ? new Date(value).toLocaleString("en-US", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "medium",
        timeStyle: "short",
      })
    : "Time to be announced";

function parseShape(shapeJson) {
  try {
    return JSON.parse(shapeJson || "{}");
  } catch {
    return {};
  }
}

function getZonePosition(shapeJson) {
  const shape = parseShape(shapeJson);

  if (
    shape.type === "polygon" &&
    Array.isArray(shape.points) &&
    shape.points.length > 2
  ) {
    const xs = shape.points.map((point) => Number(point.x));
    const ys = shape.points.map((point) => Number(point.y));
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    const width = Math.max(maxX - minX, 1);
    const height = Math.max(maxY - minY, 1);

    const points = shape.points
      .map((point) => {
        const x = ((Number(point.x) - minX) / width) * 100;
        const y = ((Number(point.y) - minY) / height) * 100;
        return `${x}% ${y}%`;
      })
      .join(", ");

    return {
      left: `${minX}%`,
      top: `${minY}%`,
      width: `${width}%`,
      height: `${height}%`,
      clipPath: `polygon(${points})`,
    };
  }

  return {
    left: `${Number(shape.x) || 0}%`,
    top: `${Number(shape.y) || 0}%`,
    width: `${Number(shape.w) || 20}%`,
    height: `${Number(shape.h) || 18}%`,
    transform: `rotate(${Number(shape.rot) || 0}deg)`,
  };
}

function unwrapResponse(response) {
  return response?.data?.data ?? response?.data;
}

export default function SelectTicketPage() {
  const { eventId } = useParams();
  const navigate = useNavigate();

  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [availabilityError, setAvailabilityError] = useState("");

  const [bookedSeatIds, setBookedSeatIds] = useState([]);
  const [heldSeatIds, setHeldSeatIds] = useState([]);
  const [ticketQuantities, setTicketQuantities] = useState({});
  const [zoneQuantities, setZoneQuantities] = useState({});
  const [selectedSeatIds, setSelectedSeatIds] = useState([]);

  const [activeZone, setActiveZone] = useState(null);
  const [zoneDraftQuantity, setZoneDraftQuantity] = useState(0);
  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false);
  const [isHolding, setIsHolding] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadEvent() {
      setLoading(true);
      setError("");
      setAvailabilityError("");
      setBookedSeatIds([]);
      setHeldSeatIds([]);

      try {
        const eventResponse = await eventApi.getById(eventId);
        const eventData = unwrapResponse(eventResponse);

        if (!eventData) {
          throw new Error("The event was not found.");
        }

        if (cancelled) return;

        setEvent(eventData);
        setTicketQuantities({});
        setZoneQuantities({});
        setSelectedSeatIds([]);

        const seatingMode =
          eventData.seatingMode ||
          eventData.seatingChart?.seatingMode ||
          "GeneralAdmission";

        if (seatingMode === "ReservedSeating") {
          const allSeatIds = [
            ...new Set(
              (eventData.seatingChart?.zones ?? [])
                .flatMap((zone) => zone.seats ?? [])
                .map((seat) => Number(seat.seatId))
                .filter((seatId) => seatId > 0)
            ),
          ];

          if (allSeatIds.length > 0) {
            const [bookedResponse, heldResponse] = await Promise.all([
              ticketApi.getBookedSeatIds(eventData.eventId),
              ticketApi.getHeldSeatIds(eventData.eventId, allSeatIds),
            ]);

            const booked = unwrapResponse(bookedResponse);
            const held = unwrapResponse(heldResponse);

            if (!Array.isArray(booked) || !Array.isArray(held)) {
              throw new Error("Invalid seat availability response.");
            }

            if (!cancelled) {
              setBookedSeatIds(booked.map(Number));
              setHeldSeatIds(held.map(Number));
            }
          }
        }
      } catch (err) {
        if (!cancelled) {
          const message =
            err.response?.data?.message ||
            err.message ||
            "Could not load event details.";

          // Ghế không được chọn nếu chưa tải được trạng thái tồn kho.
          setAvailabilityError(message);
          setError(message);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadEvent();

    return () => {
      cancelled = true;
    };
  }, [eventId]);

  const ticketTypes = event?.ticketTypes ?? [];
  const zones = event?.seatingChart?.zones ?? [];
  const seatingMode =
    event?.seatingMode ||
    event?.seatingChart?.seatingMode ||
    "GeneralAdmission";

  const isStandingZones = seatingMode === "StandingZones";
  const isReservedSeating = seatingMode === "ReservedSeating";

  const maxPerAccount = Number(event?.maxTicketsPerAccount || 10);
  const minPerAccount = Number(event?.minTicketsPerAccount || 1);

  function getTicket(ticketTypeId) {
    return ticketTypes.find(
      (ticket) =>
        Number(ticket.ticketTypeId) === Number(ticketTypeId)
    );
  }

  function getTicketColor(ticketTypeId) {
    const ticket = getTicket(ticketTypeId);
    const paletteIndex =
      (Number(ticketTypeId) || 0) % ZONE_PALETTE.length;

    return ticket?.colorCode || ZONE_PALETTE[paletteIndex];
  }

  function getTicketAvailable(ticket) {
    return Math.max(
      0,
      Number(
        ticket?.availableQuantity ??
          Number(ticket?.quantity || 0) -
            Number(ticket?.soldQuantity || 0)
      )
    );
  }

  const selectedItems = useMemo(() => {
    if (!event) return [];

    if (isStandingZones) {
      return zones
        .map((zone) => {
          const ticket = ticketTypes.find(
            (item) =>
              Number(item.ticketTypeId) === Number(zone.ticketTypeId)
          );
          const quantity = Number(
            zoneQuantities[zone.seatZoneId] || 0
          );
          const price = Number(ticket?.price || 0);

          return {
            id: `zone-${zone.seatZoneId}`,
            ticketTypeId: Number(zone.ticketTypeId),
            ticketName: ticket?.typeName || zone.zoneName,
            name: zone.zoneName,
            quantity,
            price,
            total: quantity * price,
          };
        })
        .filter((item) => item.quantity > 0);
    }

    if (isReservedSeating) {
      const grouped = new Map();

      for (const zone of zones) {
        const seats = (zone.seats ?? []).filter((seat) =>
          selectedSeatIds.includes(Number(seat.seatId))
        );

        if (seats.length === 0) continue;

        const ticket = ticketTypes.find(
          (item) =>
            Number(item.ticketTypeId) === Number(zone.ticketTypeId)
        );
        const ticketTypeId = Number(zone.ticketTypeId);
        const price = Number(ticket?.price || 0);
        const existing = grouped.get(ticketTypeId);

        if (existing) {
          existing.quantity += seats.length;
          existing.total = existing.quantity * existing.price;
          existing.seatIds.push(
            ...seats.map((seat) => Number(seat.seatId))
          );
        } else {
          grouped.set(ticketTypeId, {
            id: `ticket-${ticketTypeId}`,
            ticketTypeId,
            ticketName: ticket?.typeName || zone.zoneName,
            name: zone.zoneName,
            quantity: seats.length,
            price,
            total: seats.length * price,
            seatIds: seats.map((seat) => Number(seat.seatId)),
          });
        }
      }

      return [...grouped.values()];
    }

    return ticketTypes
      .map((ticket) => {
        const quantity = Number(
          ticketQuantities[ticket.ticketTypeId] || 0
        );
        const price = Number(ticket.price || 0);

        return {
          id: `ticket-${ticket.ticketTypeId}`,
          ticketTypeId: Number(ticket.ticketTypeId),
          ticketName: ticket.typeName,
          name: ticket.typeName,
          quantity,
          price,
          total: quantity * price,
        };
      })
      .filter((item) => item.quantity > 0);
  }, [
    event,
    isStandingZones,
    isReservedSeating,
    zones,
    ticketTypes,
    zoneQuantities,
    selectedSeatIds,
    ticketQuantities,
  ]);

  const totalQuantity = selectedItems.reduce(
    (sum, item) => sum + item.quantity,
    0
  );

  const totalAmount = selectedItems.reduce(
    (sum, item) => sum + item.total,
    0
  );

  function getZoneAvailable(zone) {
    const ticket = getTicket(zone.ticketTypeId);
    const zoneCapacity = Number(zone.capacity || Infinity);

    return Math.max(
      0,
      Math.min(zoneCapacity, getTicketAvailable(ticket))
    );
  }

  function getZoneMax(zone) {
    const ticket = getTicket(zone.ticketTypeId);

    return Math.min(
      getZoneAvailable(zone),
      Number(ticket?.maxPerOrder || maxPerAccount),
      maxPerAccount
    );
  }

  function getTicketMax(ticket) {
    return Math.min(
      getTicketAvailable(ticket),
      Number(ticket?.maxPerOrder || maxPerAccount),
      maxPerAccount
    );
  }

  function openZonePicker(zone) {
    const existingZoneId = Object.keys(zoneQuantities).find(
      (id) => Number(zoneQuantities[id]) > 0
    );

    if (
      existingZoneId &&
      Number(existingZoneId) !== Number(zone.seatZoneId)
    ) {
      window.alert("Please select tickets from only one zone at a time.");
      return;
    }

    if (getZoneAvailable(zone) <= 0) return;

    setActiveZone(zone);
    setZoneDraftQuantity(Number(zoneQuantities[zone.seatZoneId] || 0));
    setIsZoneModalOpen(true);
  }

  function updateTicketQuantity(ticket, nextQuantity) {
    const currentOtherQuantity =
      totalQuantity -
      Number(ticketQuantities[ticket.ticketTypeId] || 0);

    const allowedForAccount = Math.max(
      0,
      maxPerAccount - currentOtherQuantity
    );

    const quantity = Math.max(
      0,
      Math.min(nextQuantity, getTicketMax(ticket), allowedForAccount)
    );

    setTicketQuantities((current) => ({
      ...current,
      [ticket.ticketTypeId]: quantity,
    }));
  }

  function updateZoneDraft(nextQuantity) {
    if (!activeZone) return;

    const quantity = Math.max(
      0,
      Math.min(nextQuantity, getZoneMax(activeZone), maxPerAccount)
    );

    setZoneDraftQuantity(quantity);
  }

  function toggleSeat(seat, zone) {
    const seatId = Number(seat.seatId);
    const unavailable =
      bookedSeatIds.includes(seatId) ||
      heldSeatIds.includes(seatId);

    if (availabilityError || unavailable || isHolding) return;

    const alreadySelected = selectedSeatIds.includes(seatId);

    if (alreadySelected) {
      setSelectedSeatIds((current) =>
        current.filter((id) => id !== seatId)
      );
      return;
    }

    const ticket = getTicket(zone.ticketTypeId);

    const selectedForType = zones
      .filter(
        (item) =>
          Number(item.ticketTypeId) === Number(zone.ticketTypeId)
      )
      .flatMap((item) => item.seats ?? [])
      .filter((item) =>
        selectedSeatIds.includes(Number(item.seatId))
      ).length;

    if (selectedForType >= getTicketMax(ticket)) {
      window.alert(
        `The limit for ${ticket?.typeName || zone.zoneName} is ${getTicketMax(ticket)} ticket(s).`
      );
      return;
    }

    if (selectedSeatIds.length >= maxPerAccount) {
      window.alert(
        `You can select up to ${maxPerAccount} ticket(s) per order.`
      );
      return;
    }

    setSelectedSeatIds((current) => [...current, seatId]);
  }

  function buildZoneItem(zone, quantity) {
    const ticket = getTicket(zone.ticketTypeId);
    const price = Number(ticket?.price || 0);

    return {
      id: `zone-${zone.seatZoneId}`,
      ticketTypeId: Number(zone.ticketTypeId),
      ticketName: ticket?.typeName || zone.zoneName,
      name: zone.zoneName,
      quantity,
      price,
      total: quantity * price,
    };
  }

  async function handleContinue(zoneOverride = null) {
    let items = selectedItems;

    if (zoneOverride) {
      items =
        zoneOverride.quantity > 0
          ? [buildZoneItem(zoneOverride.zone, zoneOverride.quantity)]
          : [];
    }

    const quantity = items.reduce(
      (sum, item) => sum + item.quantity,
      0
    );

    if (quantity < minPerAccount) {
      window.alert(`Please select at least ${minPerAccount} ticket(s).`);
      return;
    }

    if (quantity > maxPerAccount) {
      window.alert(`You can select up to ${maxPerAccount} ticket(s).`);
      return;
    }

    if (isReservedSeating && availabilityError) {
      window.alert(availabilityError);
      return;
    }

    const ticketsByType = new Map();

    for (const item of items) {
      const id = Number(item.ticketTypeId);
      ticketsByType.set(
        id,
        (ticketsByType.get(id) || 0) + item.quantity
      );
    }

    const payload = {
      eventId: Number(event.eventId),
      tickets: [...ticketsByType.entries()].map(
        ([ticketTypeId, ticketQuantity]) => ({
          ticketTypeId,
          quantity: ticketQuantity,
        })
      ),
      seatIds: isReservedSeating ? selectedSeatIds : [],
    };

    setIsHolding(true);

    try {
      const response = await ticketApi.createHoldSession(payload);

      const holdId = response.data?.data?.holdId;

      if (typeof holdId !== "string" || !holdId.trim()) {
        console.error("Unexpected create-hold response:", response.data);
        throw new Error("Hold API did not return a valid holdId.");
      }

      navigate(`/checkout/question-form/${encodeURIComponent(holdId)}`);
    } catch (err) {
      const body = err.response?.data;
      const message =
        typeof body === "string"
          ? body
          : body?.message ||
            body?.Message ||
            err.message ||
            "Could not reserve the selected tickets.";

      window.alert(message);
    } finally {
      setIsHolding(false);
    }
  }

  function confirmZoneSelection() {
    if (!activeZone) return;

    const quantity = zoneDraftQuantity;

    setZoneQuantities((current) => ({
      ...current,
      [activeZone.seatZoneId]: quantity,
    }));

    setIsZoneModalOpen(false);
    handleContinue({ zone: activeZone, quantity });
  }

  if (loading) {
    return (
      <>
        <Header />

        <main className="ticket-loading-page" aria-live="polite">
          <section className="ticket-loading-card">
            <div className="ticket-loading-spinner" aria-hidden="true">
              <span />
            </div>

            <h2>Preparing your event</h2>
            <p>Loading ticket types and seating information...</p>

            <div className="ticket-loading-skeleton" aria-hidden="true">
              <i />
              <i />
              <i />
            </div>
          </section>
        </main>

        <Footer />
      </>
    );
  }

  if (error) {
    return <div className="tb-error">{error}</div>;
  }

  if (!event) {
    return <div className="tb-error">Event not found.</div>;
  }

  return (
    <>
    <Header />
    <div className="tb-select-ticket-page">
      <div className="tb-container tb-select-layout">
        <main className="tb-col-7">
          <div className="tb-section-header">
            <h2 className="tb-section-title">
              {isStandingZones
                ? "Choose a zone"
                : isReservedSeating
                  ? "Choose your seats"
                  : "Choose ticket types"}
            </h2>

            {isReservedSeating && (
              <div className="tb-seat-legend" aria-label="Seat legend">
                <span>
                  <i className="tb-legend-dot is-available" />
                  Available
                </span>
                <span>
                  <i className="tb-legend-dot is-selected" />
                  Selected
                </span>
                <span>
                  <i className="tb-legend-dot is-held" />
                  On hold
                </span>
                <span>
                  <i className="tb-legend-dot is-sold" />
                  Sold
                </span>
              </div>
            )}
          </div>

          {availabilityError && isReservedSeating && (
            <div className="tb-availability-error">
              Seat availability could not be verified. Please refresh and try
              again.
            </div>
          )}

          {isStandingZones && (
            <>
              <div className="tb-zone-map-wrapper">
                <div className="tb-zone-map">
                  <div className="tb-map-stage">STAGE</div>

                  {zones.map((zone) => {
                    const ticket = getTicket(zone.ticketTypeId);
                    const color = getTicketColor(zone.ticketTypeId);
                    const soldOut = getZoneAvailable(zone) <= 0;
                    const quantity = Number(
                      zoneQuantities[zone.seatZoneId] || 0
                    );

                    return (
                      <button
                        type="button"
                        key={zone.seatZoneId}
                        className={`tb-zone ${
                          quantity > 0 ? "tb-zone-selected" : ""
                        } ${soldOut ? "tb-zone-sold-out" : ""}`}
                        style={{
                          ...getZonePosition(zone.shapeJson),
                          "--zone-color": soldOut ? "#71717a" : color,
                        }}
                        disabled={soldOut}
                        onClick={() => openZonePicker(zone)}
                      >
                        <span className="tb-zone-name">{zone.zoneName}</span>
                        <span className="tb-zone-price">
                          {soldOut ? "Sold out" : formatPrice(ticket?.price)}
                        </span>
                        {quantity > 0 && (
                          <span className="tb-zone-selected-count">
                            {quantity} selected
                          </span>
                        )}
                      </button>
                    );
                  })}
                </div>
              </div>

              <p className="tb-hint-text">
                Select tickets from one zone at a time.
              </p>
            </>
          )}

          {isReservedSeating && (
            <div className="tb-seat-map">
              <div className="tb-seat-canvas">
                <div className="tb-seat-stage">STAGE</div>

                {zones.map((zone) => {
                  const color = getTicketColor(zone.ticketTypeId);
                  const seats = zone.seats ?? [];
                  const columns = Math.max(
                    1,
                    ...seats.map((seat) =>
                      Number(seat.xCoordinate || 1)
                    )
                  );
                  const rows = Math.max(
                    1,
                    ...seats.map((seat) =>
                      Number(seat.yCoordinate || 1)
                    )
                  );

                  return (
                    <section
                      key={zone.seatZoneId}
                      className="tb-seat-zone"
                      style={{
                        ...getZonePosition(zone.shapeJson),
                        "--zone-color": color,
                        "--seat-columns": columns,
                        "--seat-rows": rows,
                      }}
                    >
                      <div className="tb-seat-zone-title">
                        {zone.zoneName}
                      </div>

                      <div
                        className="tb-seat-grid"
                        style={{
                          gridTemplateColumns: `repeat(${columns}, var(--seat-size))`,
                          gridTemplateRows: `repeat(${rows}, var(--seat-size))`,
                        }}
                      >
                        {seats.map((seat) => {
                          const seatId = Number(seat.seatId);
                          const sold = bookedSeatIds.includes(seatId);
                          const held = heldSeatIds.includes(seatId);
                          const selected =
                            selectedSeatIds.includes(seatId);

                          const statusClass = sold
                            ? "is-sold"
                            : held
                              ? "is-held"
                              : selected
                                ? "is-selected"
                                : "is-available";

                          const statusText = sold
                            ? "sold"
                            : held
                              ? "on hold"
                              : selected
                                ? "selected"
                                : "available";

                          return (
                            <button
                              type="button"
                              key={seatId}
                              className={`tb-seat-dot ${statusClass}`}
                              style={{
                                gridColumn:
                                  Number(seat.xCoordinate) || 1,
                                gridRow: Number(seat.yCoordinate) || 1,
                                "--zone-color": color,
                              }}
                              title={`Row ${seat.rowLabel}, Seat ${seat.seatNumber} — ${statusText}`}
                              aria-label={`Row ${seat.rowLabel}, Seat ${seat.seatNumber}, ${statusText}`}
                              disabled={
                                sold ||
                                held ||
                                Boolean(availabilityError) ||
                                isHolding
                              }
                              onClick={() => toggleSeat(seat, zone)}
                            />
                          );
                        })}
                      </div>
                    </section>
                  );
                })}
              </div>
            </div>
          )}

          {!isStandingZones && !isReservedSeating && (
            <div className="tb-ticket-list">
              {ticketTypes.map((ticket) => {
                const quantity = Number(
                  ticketQuantities[ticket.ticketTypeId] || 0
                );
                const available = getTicketAvailable(ticket);
                const max = getTicketMax(ticket);

                return (
                  <div className="tb-ticket-row" key={ticket.ticketTypeId}>
                    <div className="tb-ticket-info">
                      <div className="tb-ticket-name">{ticket.typeName}</div>
                      <div className="tb-ticket-avail">
                        {available} ticket(s) available
                      </div>
                    </div>

                    <div className="tb-ticket-price">
                      {formatPrice(ticket.price)}
                    </div>

                    <div className="tb-qty-control">
                      <button
                        type="button"
                        onClick={() =>
                          updateTicketQuantity(ticket, quantity - 1)
                        }
                        disabled={quantity <= 0}
                      >
                        −
                      </button>
                      <input
                        type="number"
                        min="0"
                        max={max}
                        value={quantity}
                        onChange={(e) =>
                          updateTicketQuantity(
                            ticket,
                            Number(e.target.value) || 0
                          )
                        }
                      />
                      <button
                        type="button"
                        onClick={() =>
                          updateTicketQuantity(ticket, quantity + 1)
                        }
                        disabled={quantity >= max}
                      >
                        +
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </main>

        <aside className="tb-col-3">
          <section className="tb-sidebar-card tb-event-info">
            <h3 className="tb-card-title">Event information</h3>
            <div className="tb-event-name">{event.title}</div>
            <div className="tb-meta-item">
              <span>📅</span>
              {formatEventTime(event.startsAt)}
            </div>
            <div className="tb-meta-item">
              <span>📍</span>
              {[event.locationName, event.city].filter(Boolean).join(", ") ||
                "Location to be announced"}
            </div>
            <div className="tb-meta-item">
              <span>🎟</span>
              Maximum {maxPerAccount} ticket(s) per order
            </div>
          </section>

          {zones.length > 0 && (
            <section className="tb-sidebar-card">
              <h3 className="tb-card-title">
                {isStandingZones ? "Zones and prices" : "Ticket types"}
              </h3>

              {zones.map((zone) => (
                <div className="tb-price-row" key={zone.seatZoneId}>
                  <span className="tb-price-name">
                    <i
                      className="tb-zone-color-dot"
                      style={{
                        background: getTicketColor(zone.ticketTypeId),
                      }}
                    />
                    {zone.zoneName}
                  </span>
                  <span className="tb-price-value">
                    {formatPrice(getTicket(zone.ticketTypeId)?.price)}
                  </span>
                </div>
              ))}
            </section>
          )}

          <section className="tb-sidebar-card tb-order-summary">
            <h3 className="tb-card-title">
              Your selection ({totalQuantity})
            </h3>

            {selectedItems.length === 0 ? (
              <p className="tb-empty-msg">No tickets selected yet.</p>
            ) : (
              <div className="tb-selected-list">
                {selectedItems.map((item) => (
                  <div className="tb-selected-row" key={item.id}>
                    <div>
                      {item.name}
                      <span className="tb-badge">×{item.quantity}</span>

                      {item.seatIds?.length > 0 && (
                        <small className="tb-seat-summary">
                          {item.seatIds
                            .map((id) => {
                              const seat = zones
                                .flatMap((zone) => zone.seats ?? [])
                                .find(
                                  (entry) =>
                                    Number(entry.seatId) === Number(id)
                                );

                              return seat
                                ? `${seat.rowLabel}${seat.seatNumber}`
                                : id;
                            })
                            .join(", ")}
                        </small>
                      )}
                    </div>
                    <div className="tb-row-total">
                      {formatPrice(item.total)}
                    </div>
                  </div>
                ))}
              </div>
            )}

            <div className="tb-grand-total">
              <span>Total</span>
              <span className="tb-total-amount">
                {formatPrice(totalAmount)}
              </span>
            </div>

            <button
              type="button"
              className="tb-btn-continue"
              onClick={() => handleContinue()}
              disabled={
                totalQuantity === 0 ||
                isHolding ||
                (isReservedSeating && Boolean(availabilityError))
              }
            >
              {isHolding ? "Reserving..." : "Continue"}
            </button>
          </section>
        </aside>
      </div>

      {isZoneModalOpen && activeZone && (
        <div
          className="tb-modal-overlay"
          onMouseDown={() => setIsZoneModalOpen(false)}
        >
          <div
            className="tb-modal-content"
            onMouseDown={(e) => e.stopPropagation()}
          >
            <div className="tb-modal-header">
              <h3>{activeZone.zoneName}</h3>
              <span className="tb-modal-price">
                {formatPrice(getTicket(activeZone.ticketTypeId)?.price)} / ticket
              </span>
            </div>

            <p className="tb-zone-modal-availability">
              {getZoneAvailable(activeZone)} ticket(s) available
            </p>

            <div className="tb-modal-qty">
              <button
                type="button"
                onClick={() => updateZoneDraft(zoneDraftQuantity - 1)}
                disabled={zoneDraftQuantity <= 0}
              >
                −
              </button>
              <input
                type="number"
                min="0"
                max={getZoneMax(activeZone)}
                value={zoneDraftQuantity}
                onChange={(e) =>
                  updateZoneDraft(Number(e.target.value) || 0)
                }
              />
              <button
                type="button"
                onClick={() => updateZoneDraft(zoneDraftQuantity + 1)}
                disabled={zoneDraftQuantity >= getZoneMax(activeZone)}
              >
                +
              </button>
            </div>

            <p className="tb-zone-modal-limit">
              Maximum {getZoneMax(activeZone)} ticket(s) for this zone.
            </p>

            <div className="tb-modal-actions">
              <button
                type="button"
                className="tb-btn-outline"
                onClick={() => setIsZoneModalOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="tb-btn-primary"
                disabled={zoneDraftQuantity <= 0 || isHolding}
                onClick={confirmZoneSelection}
              >
                {isHolding ? "Reserving..." : "Continue"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
    <Footer />
  </>
  );
}