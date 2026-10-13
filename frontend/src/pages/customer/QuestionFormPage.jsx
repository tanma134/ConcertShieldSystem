import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import ticketApi from "../../api/ticketApi";
import eventApi from "../../api/eventApi";
import "./QuestionFormPage.css";
import Header from "../../components/Header";
import Footer from "../../components/Footer";

const formatCurrency = (amount) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
  }).format(Number(amount) || 0);

const formatTime = (seconds) => {
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;

  return `${String(minutes).padStart(2, "0")}:${String(remainder).padStart(
    2,
    "0"
  )}`;
};

function buildAttendeeRows(hold, eventData, savedAttendees = []) {
  const ticketItems = hold.items ?? [];
  const heldSeatIds = (hold.seatIds ?? []).map(Number);

  if (new Set(heldSeatIds).size !== heldSeatIds.length) {
    throw new Error("The reservation contains duplicate seat IDs.");
  }

  const heldSeatSet = new Set(heldSeatIds);
  const seatInfoById = new Map();

  for (const zone of eventData.seatingChart?.zones ?? []) {
    for (const seat of zone.seats ?? []) {
      const seatId = Number(seat.seatId);

      if (!heldSeatSet.has(seatId)) continue;

      seatInfoById.set(seatId, {
        seatId,
        ticketTypeId: Number(seat.ticketTypeId ?? zone.ticketTypeId),
        rowLabel: seat.rowLabel,
        seatNumber: seat.seatNumber,
      });
    }
  }

  if (heldSeatIds.length > 0 && seatInfoById.size !== heldSeatIds.length) {
    throw new Error(
      "Some selected seats could not be matched to their ticket zones. Please select your seats again."
    );
  }

  const seatsByTicketType = new Map();

  // Giữ thứ tự seatIds từ hold để mapping ổn định.
  for (const seatId of heldSeatIds) {
    const seat = seatInfoById.get(seatId);
    const seats = seatsByTicketType.get(seat.ticketTypeId) ?? [];

    seats.push(seat);
    seatsByTicketType.set(seat.ticketTypeId, seats);
  }

  const rows = [];

  for (const item of ticketItems) {
    const ticketTypeId = Number(item.ticketTypeId);
    const quantity = Number(item.quantity);
    const seats = seatsByTicketType.get(ticketTypeId) ?? [];

    if (!Number.isInteger(quantity) || quantity < 1) {
      throw new Error(`Invalid quantity for ${item.typeName}.`);
    }

    if (heldSeatIds.length > 0 && seats.length !== quantity) {
      throw new Error(
        `The selected seats do not match the quantity for ${item.typeName}.`
      );
    }

    for (let index = 0; index < quantity; index += 1) {
      const seat = seats[index] ?? null;
      const saved = savedAttendees[rows.length] ?? {};

      rows.push({
        ticketTypeId,
        ticketName: item.typeName,
        seatId: seat?.seatId ?? null,
        seatLabel:
          seat?.rowLabel && seat?.seatNumber
            ? `Row ${seat.rowLabel} · Seat ${seat.seatNumber}`
            : null,

        // Không mặc định attendee đầu tiên làm chủ tài khoản.
        isPrimaryBuyer: saved.isPrimaryBuyer === true,

        fullName: saved.fullName ?? "",
        citizenId: saved.citizenId ?? saved.cccd ?? "",
        phone: saved.phone ?? "",
        email: saved.email ?? "",
      });
    }
  }

  if (heldSeatIds.length > 0 && rows.length !== heldSeatIds.length) {
    throw new Error(
      "The number of selected seats does not match the ticket quantity."
    );
  }

  return rows;
}

export default function QuestionFormPage() {
  const { holdId } = useParams();
  const navigate = useNavigate();

  const [holdData, setHoldData] = useState(null);
  const [eventData, setEventData] = useState(null);
  const [attendees, setAttendees] = useState([]);
  const [timeLeft, setTimeLeft] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isExpired, setIsExpired] = useState(false);
  const [loadError, setLoadError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function loadReservation() {
      try {
        setIsLoading(true);
        setLoadError("");

        if (!holdId) {
          throw new Error("Reservation ID is missing.");
        }

        const holdResponse = await ticketApi.getHoldDetails(holdId);
        const hold = holdResponse.data?.data ?? holdResponse.data;

        if (!hold?.eventId) {
          throw new Error("Reservation was not found or has expired.");
        }

        const eventResponse = await eventApi.getById(hold.eventId);
        const event = eventResponse.data?.data ?? eventResponse.data;

        if (!event) {
          throw new Error("Could not load event information.");
        }

        const items = (hold.tickets ?? []).map((ticket) => {
          const ticketType = event.ticketTypes?.find(
            (type) =>
              Number(type.ticketTypeId) === Number(ticket.ticketTypeId)
          );

          if (!ticketType && ticket.typeName == null) {
            throw new Error(
              `Ticket type ${ticket.ticketTypeId} was not found.`
            );
          }

          return {
            ticketTypeId: Number(ticket.ticketTypeId),
            typeName: ticketType?.typeName ?? ticket.typeName,
            quantity: Number(ticket.quantity),
            price: Number(ticketType?.price ?? ticket.unitPrice) || 0,
          };
        });

        const remainingSeconds = Math.max(
          0,
          Number(hold.remainingSeconds) || 0
        );

        const reservation = {
          ...hold,
          items,
          totalAmount: items.reduce(
            (sum, item) => sum + item.price * item.quantity,
            0
          ),
          expiresAtUtc:
            hold.expiresAtUtc ??
            new Date(Date.now() + remainingSeconds * 1000).toISOString(),
        };

        const attendeeRows = buildAttendeeRows(
          reservation,
          event,
          hold.attendees ?? []
        );

        if (cancelled) return;

        setHoldData(reservation);
        setEventData(event);
        setAttendees(attendeeRows);
        setTimeLeft(remainingSeconds);
      } catch (error) {
        if (!cancelled) {
          setLoadError(
            error.response?.data?.message ||
              error.message ||
              "Could not load the reservation."
          );
        }
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    loadReservation();

    return () => {
      cancelled = true;
    };
  }, [holdId]);

  useEffect(() => {
    if (!holdData?.expiresAtUtc) return undefined;

    const expiresAt = new Date(holdData.expiresAtUtc).getTime();
    if (Number.isNaN(expiresAt)) return undefined;

    const updateCountdown = () => {
      const remaining = Math.max(
        0,
        Math.ceil((expiresAt - Date.now()) / 1000)
      );

      setTimeLeft(remaining);
      setIsExpired(remaining <= 0);
    };

    updateCountdown();

    const timer = window.setInterval(updateCountdown, 1000);
    return () => window.clearInterval(timer);
  }, [holdData?.expiresAtUtc]);

  const handleInputChange = useCallback((index, field, value) => {
    setAttendees((current) =>
      current.map((attendee, attendeeIndex) =>
        attendeeIndex === index
          ? { ...attendee, [field]: value }
          : attendee
      )
    );
  }, []);

  const handlePrimaryBuyerChange = (selectedIndex) => {
    setAttendees((current) =>
      current.map((attendee, index) => ({
        ...attendee,
        isPrimaryBuyer: index === selectedIndex,
      }))
    );
  };

  const handleSubmit = async (event) => {
    event.preventDefault();

    if (isExpired || timeLeft <= 0) {
      setIsExpired(true);
      return;
    }

    const primaryAttendees = attendees.filter(
      (attendee) => attendee.isPrimaryBuyer
    );

    if (primaryAttendees.length !== 1) {
      window.alert("Please select exactly one primary account holder.");
      return;
    }

    const allFieldsValid = attendees.every((attendee) => {
      const commonFieldsFilled =
        attendee.fullName.trim() !== "" &&
        attendee.citizenId.trim() !== "" &&
        attendee.phone.trim() !== "";

      return attendee.isPrimaryBuyer
        ? commonFieldsFilled && attendee.email.trim() !== ""
        : commonFieldsFilled;
    });

    if (!allFieldsValid) {
      window.alert(
        "Please complete the required details for every attendee. The primary account holder must also provide an email address."
      );
      return;
    }

    const citizenIds = attendees.map((attendee) =>
      attendee.citizenId.trim().toLowerCase()
    );
    const phoneNumbers = attendees.map((attendee) =>
      attendee.phone.trim()
    );

    if (new Set(citizenIds).size !== citizenIds.length) {
      window.alert(
        "Each attendee must have a unique ID card or passport number."
      );
      return;
    }

    if (new Set(phoneNumbers).size !== phoneNumbers.length) {
      window.alert("Each attendee must have a unique phone number.");
      return;
    }

    setIsSubmitting(true);

    try {
      const attendeePayload = attendees.map((attendee) => ({
        fullName: attendee.fullName.trim(),
        citizenId: attendee.citizenId.trim(),
        phone: attendee.phone.trim(),
        email: attendee.isPrimaryBuyer
          ? attendee.email.trim()
          : null,
        isPrimaryBuyer: attendee.isPrimaryBuyer,
        ticketTypeId: Number(attendee.ticketTypeId),
        seatId:
          attendee.seatId == null ? null : Number(attendee.seatId),
      }));

      await ticketApi.saveHoldAttendees(holdId, attendeePayload);

      navigate(`/checkout/payment-info/${holdId}`);
    } catch (error) {
      console.error("Could not save attendees:", error);

      window.alert(
        error.response?.data?.message ||
          "Could not save attendee information. Please try again."
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  if (isLoading) {
    return (
      <main className="qf-page qf-state" aria-live="polite">
        <section className="qf-loading-card">
          <div className="qf-spinner" aria-hidden="true">
            <span />
          </div>

          <h2>Preparing your checkout</h2>
          <p>Loading reservation and attendee details...</p>

          <div className="qf-loading-skeleton" aria-hidden="true">
            <i />
            <i />
            <i />
          </div>
        </section>
      </main>
    );
  }

  if (loadError) {
    return (
      <main className="qf-page qf-state">
        <section className="qf-message-card">
          <div className="qf-status-icon is-error">!</div>
          <h1>Could not load reservation</h1>
          <p>{loadError}</p>
          <Link to="/" className="qf-button">
            Back to events
          </Link>
        </section>
      </main>
    );
  }

  if (isExpired) {
    return (
      <main className="qf-page qf-state">
        <section className="qf-message-card">
          <div className="qf-status-icon is-error">!</div>
          <h1>Reservation expired</h1>
          <p>
            Your held tickets have been released. Please select tickets again.
          </p>
          <Link to="/" className="qf-button">
            Back to events
          </Link>
        </section>
      </main>
    );
  }

  if (!holdData || !eventData) return null;

  return (
    <>
    <Header />
    <main className="qf-page">
      <div className="qf-container qf-layout">
        <section className="qf-main">
          <form id="attendee-form" onSubmit={handleSubmit}>
            <div className="qf-section-heading">
              <div>
                <p className="qf-eyebrow">TICKET HOLDERS</p>
                <h2>Attendee details</h2>
              </div>

              <span className="qf-count">
                {attendees.length} tickets
              </span>
            </div>

            <p className="qf-primary-hint">
              Select which attendee is the account holder. Every attendee must
              provide an ID card or passport number.
            </p>

            {attendees.map((attendee, index) => (
              <article
                className={`qf-attendee-card ${
                  attendee.isPrimaryBuyer ? "is-primary" : ""
                }`}
                key={`${attendee.ticketTypeId}-${attendee.seatId ?? "no-seat"}-${index}`}
              >
                <div className="qf-ticket-summary">
                  <div className="qf-ticket-index">{index + 1}</div>

                  <div className="qf-ticket-copy">
                    <span className="qf-ticket-label">
                      Attendee {index + 1}
                    </span>
                    <strong>{attendee.ticketName}</strong>
                  </div>

                  <div
                    className={`qf-seat ${
                      attendee.seatLabel ? "" : "qf-no-seat"
                    }`}
                  >
                    <span>
                      {attendee.seatLabel ? "Assigned seat" : "Admission"}
                    </span>
                    <strong>
                      {attendee.seatLabel ??
                        "Zone / General admission"}
                    </strong>
                  </div>
                </div>

                <label className="qf-primary-choice">
                  <input
                    type="radio"
                    name="primaryBuyer"
                    checked={attendee.isPrimaryBuyer}
                    onChange={() => handlePrimaryBuyerChange(index)}
                  />
                  <span>This ticket is for me (account holder)</span>
                </label>

                <div className="qf-form-grid">
                  <label className="qf-field">
                    <span>
                      Full name <b>*</b>
                    </span>
                    <input
                      type="text"
                      autoComplete="name"
                      value={attendee.fullName}
                      onChange={(event) =>
                        handleInputChange(
                          index,
                          "fullName",
                          event.target.value
                        )
                      }
                      placeholder="Enter full name"
                      required
                    />
                  </label>

                  <label className="qf-field">
                    <span>
                      ID card / Passport <b>*</b>
                    </span>
                    <input
                      type="text"
                      value={attendee.citizenId}
                      onChange={(event) =>
                        handleInputChange(
                          index,
                          "citizenId",
                          event.target.value
                        )
                      }
                      placeholder="Enter ID card or passport number"
                      required
                    />
                  </label>

                  <label className="qf-field">
                    <span>
                      Phone number <b>*</b>
                    </span>
                    <input
                      type="tel"
                      autoComplete="tel"
                      value={attendee.phone}
                      onChange={(event) =>
                        handleInputChange(index, "phone", event.target.value)
                      }
                      placeholder="Enter phone number"
                      required
                    />
                  </label>

                  {attendee.isPrimaryBuyer && (
                    <label className="qf-field">
                      <span>
                        Email address <b>*</b>
                      </span>
                      <input
                        type="email"
                        autoComplete="email"
                        value={attendee.email}
                        onChange={(event) =>
                          handleInputChange(
                            index,
                            "email",
                            event.target.value
                          )
                        }
                        placeholder="name@example.com"
                        required
                      />
                    </label>
                  )}
                </div>
              </article>
            ))}
          </form>
        </section>

        <aside className="qf-sidebar">
          <section className="qf-panel qf-countdown-panel">
            <p className="qf-eyebrow">TIME LEFT TO COMPLETE</p>
            <strong className={timeLeft < 120 ? "is-urgent" : ""}>
              {formatTime(timeLeft)}
            </strong>
            <span>
              Your reservation will be released when the timer expires.
            </span>
          </section>

          <section className="qf-panel">
            <p className="qf-eyebrow">YOUR ORDER</p>
            <h2>{eventData.title ?? "Booking summary"}</h2>

            <div className="qf-summary-list">
              {holdData.items.map((item) => (
                <div
                  className="qf-summary-item"
                  key={item.ticketTypeId}
                >
                  <span>
                    {item.typeName} <small>× {item.quantity}</small>
                  </span>
                  <strong>
                    {formatCurrency(item.price * item.quantity)}
                  </strong>
                </div>
              ))}
            </div>

            <div className="qf-total">
              <span>Total</span>
              <strong>{formatCurrency(holdData.totalAmount)}</strong>
            </div>

            <button
              type="submit"
              form="attendee-form"
              className="qf-button qf-submit"
              disabled={isSubmitting || timeLeft <= 0}
            >
              {isSubmitting
                ? "Saving attendee details..."
                : "Continue to payment"}
              {!isSubmitting && <span aria-hidden="true">→</span>}
            </button>
          </section>
        </aside>
      </div>
    </main>
    <Footer />
  </>
  );
}