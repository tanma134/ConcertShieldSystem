import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import eventApi from "../../api/eventApi";
import ticketApi from "../../api/ticketApi";
import "./PaymentInfoPage.css";
import Header from "../../components/Header";
import Footer from "../../components/Footer";

const PAYMENT_METHODS = [
  {
    id: "vnpay",
    name: "VNPay",
    description: "Internet Banking, ATM card or VNPay QR",
    mark: "VNPAY",
    available: true,
    badge: "Available",
  },
  {
    id: "momo",
    name: "MoMo",
    description: "Pay with your MoMo e-wallet",
    mark: "MoMo",
    available: false,
    badge: "Coming soon",
  },
  {
    id: "zalopay",
    name: "ZaloPay",
    description: "Fast and secure wallet payment",
    mark: "ZaloPay",
    available: false,
    badge: "Coming soon",
  },
  {
    id: "card",
    name: "Debit / Credit Card",
    description: "Visa, Mastercard and other bank cards",
    mark: "CARD",
    available: false,
    badge: "Coming soon",
  },
];

const formatCurrency = (amount) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
  }).format(Number(amount) || 0);

const formatEventDate = (value) => {
  if (!value) return "Time to be announced";

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Time to be announced";

  return date.toLocaleString("en-GB", {
    timeZone: "Asia/Ho_Chi_Minh",
    dateStyle: "medium",
    timeStyle: "short",
  });
};

export default function PaymentInfoPage() {
  const { holdId } = useParams();
  const navigate = useNavigate();

  const [holdData, setHoldData] = useState(null);
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  const [timeLeft, setTimeLeft] = useState(0);
  const [isExpired, setIsExpired] = useState(false);
  const [voucherCode, setVoucherCode] = useState("");
  const [selectedPaymentMethod, setSelectedPaymentMethod] =
    useState("vnpay");
  const [isProcessing, setIsProcessing] = useState(false);

  const items = holdData?.items ?? [];
  const attendees = holdData?.attendees ?? [];

  // Người mua chính là attendee được chọn ở QuestionForm,
  // không mặc định attendee đầu tiên.
  const primaryHolder = useMemo(
    () => attendees.find((attendee) => attendee.isPrimaryBuyer === true) ?? {},
    [attendees]
  );

  const totalAmount = Number(holdData?.totalAmount) || 0;

  useEffect(() => {
    let cancelled = false;

    async function loadCheckout() {
      try {
        setLoading(true);
        setLoadError("");

        if (!holdId) {
          throw new Error("Hold ID is missing.");
        }

        const holdResponse = await ticketApi.getHoldDetails(holdId);
        const hold = holdResponse.data?.data ?? holdResponse.data;

        if (!hold?.eventId) {
          throw new Error(
            "Reservation was not found or has expired. Please select tickets again."
          );
        }

        const eventResponse = await eventApi.getById(hold.eventId);
        const eventData = eventResponse.data?.data ?? eventResponse.data;

        if (!eventData) {
          throw new Error("Event information was not returned.");
        }

        const ticketItems = (hold.tickets ?? []).map((ticket) => {
          const ticketType = eventData.ticketTypes?.find(
            (type) =>
              Number(type.ticketTypeId) === Number(ticket.ticketTypeId)
          );

          if (!ticketType) {
            throw new Error(
              `Ticket type ${ticket.ticketTypeId} was not found.`
            );
          }

          const price = Number(ticketType.price) || 0;
          const quantity = Number(ticket.quantity) || 0;

          return {
            ticketTypeId: Number(ticket.ticketTypeId),
            ticketName: ticketType.typeName,
            quantity,
            price,
            total: quantity * price,
          };
        });

        const attendeeList = hold.attendees ?? [];

        if (cancelled) return;

        if (attendeeList.length === 0) {
          navigate(`/checkout/question-form/${holdId}`, {
            replace: true,
          });
          return;
        }

        setEvent(eventData);
        setHoldData({
          ...hold,
          attendees: attendeeList,
          items: ticketItems,
          totalAmount: ticketItems.reduce(
            (sum, item) => sum + item.total,
            0
          ),
        });
      } catch (error) {
        if (!cancelled) {
          setLoadError(
            error.response?.data?.message ||
              error.message ||
              "Could not load reservation."
          );
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadCheckout();

    return () => {
      cancelled = true;
    };
  }, [holdId, navigate]);

  useEffect(() => {
    if (!holdData?.expiresAtUtc) return undefined;

    const expiresAt = new Date(holdData.expiresAtUtc).getTime();

    if (Number.isNaN(expiresAt)) {
      setLoadError("The reservation expiration time is invalid.");
      return undefined;
    }

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

  const formatTime = (seconds) => {
    const minutes = Math.floor(seconds / 60);
    const remainder = seconds % 60;

    return `${String(minutes).padStart(2, "0")}:${String(remainder).padStart(
      2,
      "0"
    )}`;
  };

  const handlePayment = async () => {
    if (!holdData) {
      window.alert("Reservation information could not be loaded.");
      return;
    }

    if (timeLeft <= 0) {
      setIsExpired(true);
      window.alert(
        "Your reservation has expired. Please select tickets again."
      );
      return;
    }

    if (selectedPaymentMethod !== "vnpay") {
      window.alert("This payment method is not available yet.");
      return;
    }

    if (attendees.length === 0) {
      window.alert("Attendee information is missing.");
      return;
    }

    const primaryAttendees = attendees.filter(
      (attendee) => attendee.isPrimaryBuyer === true
    );

    if (primaryAttendees.length !== 1) {
      window.alert(
        "Please return to attendee details and select exactly one account holder."
      );
      return;
    }

    if (
      !primaryHolder.fullName?.trim() ||
      !primaryHolder.email?.trim() ||
      !primaryHolder.phone?.trim()
    ) {
      window.alert(
        "The account holder's name, email, or phone is missing. Please check attendee details."
      );
      return;
    }

    setIsProcessing(true);

    try {
      const heldSeatIds = [
        ...new Set((holdData.seatIds ?? []).map(Number)),
      ];
      const isReservedSeating = heldSeatIds.length > 0;
      const seatIdsByTicketType = new Map();

      // Ghép từng ghế đang giữ với TicketTypeId tương ứng trong sơ đồ.
      if (isReservedSeating) {
        for (const zone of event?.seatingChart?.zones ?? []) {
          const zoneTicketTypeId = Number(zone.ticketTypeId);

          for (const seat of zone.seats ?? []) {
            const seatId = Number(seat.seatId);

            if (!heldSeatIds.includes(seatId)) continue;

            const ticketTypeId = Number(
              seat.ticketTypeId ?? zoneTicketTypeId
            );

            const currentSeatIds =
              seatIdsByTicketType.get(ticketTypeId) ?? [];

            currentSeatIds.push(seatId);
            seatIdsByTicketType.set(ticketTypeId, currentSeatIds);
          }
        }

        const groupedSeatCount = [...seatIdsByTicketType.values()].reduce(
          (total, seatIds) => total + seatIds.length,
          0
        );

        if (groupedSeatCount !== heldSeatIds.length) {
          throw new Error(
            "Some held seats could not be matched to their ticket types. Please reload the reservation."
          );
        }
      }

      const orderDetails = items.map((item) => ({
        ticketTypeId: Number(item.ticketTypeId),
        quantity: Number(item.quantity),
        seatIds: isReservedSeating
          ? seatIdsByTicketType.get(Number(item.ticketTypeId)) ?? []
          : [],
      }));

      if (
        isReservedSeating &&
        orderDetails.some(
          (detail) => detail.seatIds.length !== detail.quantity
        )
      ) {
        throw new Error(
          "The number of selected seats does not match the ticket quantity."
        );
      }

      const payload = {
        eventId: Number(holdData.eventId),
        isReservedSeating,
        orderDetails,

        // Các cột thông tin liên hệ trong Order lấy từ đúng chủ tài khoản.
        fullName: primaryHolder.fullName.trim(),
        email: primaryHolder.email.trim(),
        phone: primaryHolder.phone.trim(),

        voucherCode: voucherCode.trim() || null,

        // Gửi đủ dữ liệu để backend ghép attendee với đúng loại vé/ghế.
        attendees: attendees.map((attendee) => ({
          fullName: attendee.fullName?.trim() || "",
          citizenId:
            attendee.citizenId?.trim() ??
            attendee.cccd?.trim() ??
            "",
          phone: attendee.phone?.trim() || "",
          email: attendee.isPrimaryBuyer
            ? attendee.email?.trim() || ""
            : null,
          ticketTypeId: Number(attendee.ticketTypeId),
          seatId:
            attendee.seatId == null ? null : Number(attendee.seatId),
          isPrimaryBuyer: attendee.isPrimaryBuyer === true,
        })),
      };

      console.log("Create order payload:", payload);

      const response = await ticketApi.createOrder(payload);
      const result = response.data?.data ?? response.data;
      const paymentUrl = result?.paymentUrl;

      if (!paymentUrl) {
        console.error("TicketAPI response:", response.data);
        throw new Error("TicketAPI did not return a payment URL.");
      }

      window.location.assign(paymentUrl);
    } catch (error) {
      console.error("Payment error:", error);
      console.error("Backend response:", error.response?.data);

      const responseBody = error.response?.data;
      const message =
        responseBody?.message ||
        responseBody?.errors ||
        error.message ||
        "Could not start the payment.";

      window.alert(
        typeof message === "string" ? message : JSON.stringify(message)
      );

      setIsProcessing(false);
    }
  };

  if (loading) {
    return (
      <main className="qf-page qf-state" aria-live="polite">
        <section className="qf-loading-card">
          <div className="qf-spinner" aria-hidden="true">
            <span />
          </div>

          <h2>Preparing your checkout</h2>
          <p>Loading reservation</p>

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
      <main className="tb-payment-page">
        <section className="tb-expired-card">
          <div className="tb-expired-icon">!</div>
          <h1>Could not load checkout</h1>
          <p>{loadError}</p>
          <Link to="/" className="tb-primary-link">
            Back to events
          </Link>
        </section>
      </main>
    );
  }

  if (isExpired) {
    return (
      <main className="tb-payment-page">
        <section className="tb-expired-card">
          <div className="tb-expired-icon">!</div>
          <h1>Reservation expired</h1>
          <p>Your held tickets have expired. Please select tickets again.</p>
          <Link to="/" className="tb-primary-link">
            Back to events
          </Link>
        </section>
      </main>
    );
  }

  const eventTitle = event?.title || "Concert event";
  const eventLocation = [event?.locationName, event?.city]
    .filter(Boolean)
    .join(" · ");

  return (
    <>
    <Header />
    <main className="tb-payment-page">
      <div className="tb-page-container tb-payment-layout">
        <section className="tb-payment-main">
          <article className="tb-panel">
            <div className="tb-panel-heading">
              <div>
                <span className="tb-eyebrow">YOUR BOOKING</span>
                <h2>Event information</h2>
              </div>
              <span className="tb-event-tag">TICKET RESERVATION</span>
            </div>

            <div className="tb-event-card">
              {event?.posterUrl ? (
                <img
                  className="tb-event-poster"
                  src={event.posterUrl}
                  alt={`${eventTitle} poster`}
                />
              ) : (
                <div className="tb-event-poster tb-poster-fallback">
                  EVENT
                </div>
              )}

              <div className="tb-event-copy">
                <h3>{eventTitle}</h3>
                <div className="tb-event-meta">
                  <span>
                    <b>When</b>
                    {formatEventDate(event?.startsAt || event?.startDate)}
                  </span>
                  <span>
                    <b>Where</b>
                    {eventLocation ||
                      event?.address ||
                      "Location to be announced"}
                  </span>
                </div>
              </div>
            </div>
          </article>

          <article className="tb-panel">
            <div className="tb-panel-heading">
              <div>
                <span className="tb-eyebrow">TICKET DELIVERY</span>
                <h2>Contact information</h2>
              </div>
            </div>

            <div className="tb-contact-grid">
              <div className="tb-contact-item">
                <span>Account holder</span>
                <strong>{primaryHolder.fullName || "—"}</strong>
              </div>
              <div className="tb-contact-item">
                <span>Email address</span>
                <strong>{primaryHolder.email || "—"}</strong>
              </div>
              <div className="tb-contact-item">
                <span>Phone number</span>
                <strong>{primaryHolder.phone || "—"}</strong>
              </div>
            </div>
          </article>

          <article className="tb-panel">
            <div className="tb-panel-heading">
              <div>
                <span className="tb-eyebrow">SECURE CHECKOUT</span>
                <h2>Choose payment method</h2>
              </div>
              <span className="tb-secure-badge">Secure</span>
            </div>

            <div className="tb-payment-method-list">
              {PAYMENT_METHODS.map((method) => (
                <label
                  key={method.id}
                  className={[
                    "tb-method-card",
                    selectedPaymentMethod === method.id ? "is-selected" : "",
                    !method.available ? "is-unavailable" : "",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                >
                  <input
                    type="radio"
                    name="paymentMethod"
                    value={method.id}
                    checked={selectedPaymentMethod === method.id}
                    disabled={!method.available}
                    onChange={() => setSelectedPaymentMethod(method.id)}
                  />

                  <span className={`tb-method-mark tb-mark-${method.id}`}>
                    {method.mark}
                  </span>

                  <span className="tb-method-copy">
                    <strong>{method.name}</strong>
                    <small>{method.description}</small>
                  </span>

                  <span
                    className={`tb-method-badge ${
                      method.available ? "is-ready" : ""
                    }`}
                  >
                    {method.badge}
                  </span>
                </label>
              ))}
            </div>

            <div className="tb-voucher-box">
              <label htmlFor="voucherCode">Promo code</label>
              <div className="tb-voucher-control">
                <input
                  id="voucherCode"
                  type="text"
                  value={voucherCode}
                  onChange={(event) => setVoucherCode(event.target.value)}
                  placeholder="Enter a promo code"
                />
              </div>
              <small>
                The promo code will be validated when the order is submitted.
              </small>
            </div>
          </article>
        </section>

        <aside className="tb-payment-sidebar">
          <section className="tb-panel tb-countdown-panel">
            <div className="tb-countdown-label">TIME LEFT TO COMPLETE</div>
            <div
              className={`tb-countdown ${
                timeLeft < 120 ? "is-urgent" : ""
              }`}
            >
              {formatTime(timeLeft)}
            </div>
            <p>Your reservation will be released when the timer expires.</p>
          </section>

          <section className="tb-panel tb-summary-panel">
            <div className="tb-panel-heading">
              <div>
                <span className="tb-eyebrow">ORDER</span>
                <h2>Booking summary</h2>
              </div>
            </div>

            <div className="tb-hold-reference">
              <span>Reservation ID</span>
              <code>{holdId?.slice(0, 12).toUpperCase()}</code>
            </div>

            <div className="tb-summary-items">
              {items.map((item, index) => (
                <div
                  className="tb-summary-item"
                  key={`${item.ticketTypeId}-${index}`}
                >
                  <div>
                    <strong>
                      {item.ticketName || `Ticket ${item.ticketTypeId}`}
                    </strong>
                    <span>
                      {item.quantity} × {formatCurrency(item.price)}
                    </span>
                  </div>
                  <b>{formatCurrency(item.total)}</b>
                </div>
              ))}
            </div>

            <div className="tb-summary-total">
              <span>Total due</span>
              <strong>{formatCurrency(totalAmount)}</strong>
            </div>

            <button
              type="button"
              className="tb-pay-button"
              onClick={handlePayment}
              disabled={isProcessing || timeLeft <= 0 || items.length === 0}
            >
              {isProcessing
                ? "Preparing payment..."
                : `Continue with ${
                    selectedPaymentMethod === "vnpay" ? "VNPay" : "payment"
                  }`}
              {!isProcessing && <span aria-hidden="true">→</span>}
            </button>

            <div className="tb-payment-note">
              <span aria-hidden="true">🔒</span>
              Your payment is processed through a secure checkout.
            </div>
          </section>
        </aside>
      </div>
    </main>
    <Footer />
  </>
  );
}