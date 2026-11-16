import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import eventApi from "../../api/eventApi";
import ticketApi from "../../api/ticketApi";
import voucherApi from "../../api/voucherApi";
import ChangeSeatSelectionModal from "../../components/ChangeSeatSelectionModal";
import "./PaymentInfoPage.css";
import Header from "../../components/Header";
import Footer from "../../components/Footer";
import { useToast } from "../../components/ToastProvider";

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

const getResponseData = (response) =>
  response?.data?.data ?? response?.data ?? {};

const getVoucherCode = (voucher) =>
  voucher?.code ?? voucher?.Code ?? "";

const getVoucherId = (voucher) =>
  voucher?.voucher_id ?? voucher?.voucherId ?? getVoucherCode(voucher);

const getVoucherDiscount = (voucher) =>
  Number(voucher?.discount_amount ?? voucher?.discountAmount) || 0;

const getVoucherDiscountLabel = (voucher) => {
  const type = voucher?.discount_type ?? voucher?.discountType;

  if (type === "PERCENT") {
    const percent =
      voucher?.discount_percent ?? voucher?.discountPercent ?? 0;

    return `${percent}% off`;
  }

  return `${formatCurrency(
    voucher?.discount_amount ?? voucher?.discountAmount
  )} off`;
};

const hasCodePrefix = (voucher, prefix) =>
  getVoucherCode(voucher)
    .trim()
    .toUpperCase()
    .startsWith(prefix);

export default function PaymentInfoPage() {
  const { holdId } = useParams();
  const navigate = useNavigate();
  const toast = useToast();

  const [holdData, setHoldData] = useState(null);
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");

  const [timeLeft, setTimeLeft] = useState(0);
  const [isExpired, setIsExpired] = useState(false);

  const [voucherCode, setVoucherCode] = useState("");
  const [availableVouchers, setAvailableVouchers] = useState([]);
  const [voucherLoading, setVoucherLoading] = useState(false);
  const [voucherError, setVoucherError] = useState("");
  const [voucherMessage, setVoucherMessage] = useState("");
  const [appliedVoucher, setAppliedVoucher] = useState(null);
  const [showVoucherModal, setShowVoucherModal] = useState(false);
  const [isApplyingVoucher, setIsApplyingVoucher] = useState(false);

  const [selectedPaymentMethod, setSelectedPaymentMethod] =
    useState("vnpay");
  const [isProcessing, setIsProcessing] = useState(false);

  const [showCancelHoldDialog, setShowCancelHoldDialog] = useState(false);
  const [isCancellingHold, setIsCancellingHold] = useState(false);
  const [cancelHoldError, setCancelHoldError] = useState("");
  const [showSeatSelection, setShowSeatSelection] = useState(false);

  const handleSeatSelectionSaved = (updatedHold) => {
    setHoldData((current) => {
      if (!current) return current;

      return {
        ...current,
        seatIds: updatedHold.seatIds,
        attendees: updatedHold.attendees,
      };
    });
  };

  const items = holdData?.items ?? [];
  const attendees = holdData?.attendees ?? [];

  const primaryHolder = useMemo(
    () =>
      attendees.find((attendee) => attendee.isPrimaryBuyer === true) ?? {},
    [attendees]
  );

  const totalAmount = Number(holdData?.totalAmount) || 0;
  const eventId = event?.eventId ?? event?.id;

  const adminVouchers = useMemo(
    () =>
      availableVouchers.filter((voucher) =>
        hasCodePrefix(voucher, "A")
      ),
    [availableVouchers]
  );

  const organizerVouchers = useMemo(
    () =>
      availableVouchers.filter((voucher) =>
        hasCodePrefix(voucher, "OR")
      ),
    [availableVouchers]
  );

  const otherVouchers = useMemo(
    () =>
      availableVouchers.filter(
        (voucher) =>
          !hasCodePrefix(voucher, "A") &&
          !hasCodePrefix(voucher, "OR")
      ),
    [availableVouchers]
  );

  const appliedDiscount = appliedVoucher
    ? getVoucherDiscount(appliedVoucher)
    : 0;

  const payableAmount = appliedVoucher
    ? Number(
        appliedVoucher.final_amount ??
          appliedVoucher.finalAmount ??
          Math.max(0, totalAmount - appliedDiscount)
      )
    : totalAmount;

  const confirmCancelHold = async () => {
    try {
      setIsCancellingHold(true);
      setCancelHoldError("");

      await ticketApi.cancelHoldSession(holdId);

      navigate(`/events/${holdData.eventId}/select-tickets`, {
        replace: true,
      });
    } catch (error) {
      setCancelHoldError(
        error.response?.data?.message ||
          "Could not cancel your reservation. Please try again."
      );
    } finally {
      setIsCancellingHold(false);
    }
  };

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

    toast.success("Checkout is ready. Review your order and continue to payment.");
    loadCheckout();

    return () => {
      cancelled = true;
    };
  }, [holdId, navigate]);

  useEffect(() => {
    if (!eventId) return undefined;

    let cancelled = false;

    async function loadAvailableVouchers() {
      try {
        setVoucherLoading(true);
        setVoucherError("");

        const response =
          await voucherApi.getAvailableVouchers(eventId);

        const data = getResponseData(response);
        const vouchers = Array.isArray(data)
          ? data
          : data?.items ?? [];

        if (!cancelled) {
          setAvailableVouchers(vouchers);
        }
      } catch (error) {
        if (!cancelled) {
          setVoucherError(
            error.response?.data?.message ||
              "Could not load available vouchers."
          );
        }
      } finally {
        if (!cancelled) {
          setVoucherLoading(false);
        }
      }
    }

    loadAvailableVouchers();

    return () => {
      cancelled = true;
    };
  }, [eventId]);

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

  useEffect(() => {
    if (!showVoucherModal) return undefined;

    const onKeyDown = (event) => {
      if (event.key === "Escape") {
        setShowVoucherModal(false);
      }
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [showVoucherModal]);

  const formatTime = (seconds) => {
    const minutes = Math.floor(seconds / 60);
    const remainder = seconds % 60;

    return `${String(minutes).padStart(2, "0")}:${String(remainder).padStart(
      2,
      "0"
    )}`;
  };

  const handleApplyVoucher = async () => {
    const code = voucherCode.trim();

    if (!code) {
      setVoucherMessage("Enter or select a voucher code.");
      return;
    }

    try {
      setIsApplyingVoucher(true);
      setVoucherMessage("");

      const response = await voucherApi.applyVoucher({
        code,
        event_id: Number(eventId),
        order_amount: totalAmount,
      });

      const result = getResponseData(response);
      const isValid = result?.is_valid ?? result?.isValid;

      if (!isValid) {
        setAppliedVoucher(null);
        setVoucherMessage(
          result?.message || "This voucher cannot be applied."
        );
        return;
      }

      setAppliedVoucher(result);
      setVoucherCode(getVoucherCode(result) || code);
      setVoucherMessage(result.message || "Voucher applied.");
      setShowVoucherModal(false);
    } catch (error) {
      setAppliedVoucher(null);
      setVoucherMessage(
        error.response?.data?.message ||
          "Could not apply this voucher."
      );
    } finally {
      setIsApplyingVoucher(false);
    }
  };

  const handleRemoveVoucher = () => {
    setAppliedVoucher(null);
    setVoucherCode("");
    setVoucherMessage("");
  };

  const handlePayment = async () => {
    if (!holdData) {
      toast.error("Reservation information could not be loaded.");
      return;
    }

    if (timeLeft <= 0) {
      setIsExpired(true);
      toast.error("Your reservation has expired. Please select tickets again.");
      return;
    }

    if (selectedPaymentMethod !== "vnpay") {
      toast.error("This payment method is not available yet.");
      return;
    }

    if (attendees.length === 0) {
      toast.error("Attendee information is missing.");
      return;
    }

    const primaryAttendees = attendees.filter(
      (attendee) => attendee.isPrimaryBuyer === true
    );

    if (primaryAttendees.length !== 1) {
      toast.error(
        "Please return to attendee details and select exactly one account holder."
      );
      return;
    }

    if (
      !primaryHolder.fullName?.trim() ||
      !primaryHolder.email?.trim() ||
      !primaryHolder.phone?.trim()
    ) {
      toast.error(
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
        holdId,
        eventId: Number(holdData.eventId),
        isReservedSeating,
        orderDetails,
        fullName: primaryHolder.fullName.trim(),
        email: primaryHolder.email.trim(),
        phone: primaryHolder.phone.trim(),
        voucherCode: appliedVoucher
          ? getVoucherCode(appliedVoucher)
          : null,
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

      setLoadError(message);
      toast.error(message);

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

  const renderVoucherGroup = (title, vouchers, emptyText) => (
    <section className="voucher-group">
      <div className="voucher-group-heading">
        <h3>{title}</h3>
        <span>{vouchers.length}</span>
      </div>

      {vouchers.length === 0 ? (
        <p className="voucher-empty">{emptyText}</p>
      ) : (
        <div className="voucher-options">
          {vouchers.map((voucher) => {
            const code = getVoucherCode(voucher);
            const selected = voucherCode === code;

            return (
              <button
                type="button"
                key={getVoucherId(voucher)}
                className={`voucher-option ${
                  selected ? "is-selected" : ""
                }`}
                onClick={() => {
                  setVoucherCode(code);
                  setVoucherMessage("");
                }}
              >
                <span className="voucher-option-mark">%</span>
                <span className="voucher-option-copy">
                  <strong>{code}</strong>
                  <small>{getVoucherDiscountLabel(voucher)}</small>
                </span>
                <span className="voucher-option-action">
                  {selected ? "Selected" : "Select"}
                </span>
              </button>
            );
          })}
        </div>
      )}
    </section>
  );

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

            <article className="tb-panel tb-checkout-voucher-panel">
              <div className="tb-panel-heading">
                <div>
                  <span className="tb-eyebrow">DISCOUNT</span>
                  <h2>Apply voucher</h2>
                </div>
              </div>

              {appliedVoucher ? (
                <div className="tb-applied-voucher">
                  <div>
                    <strong>{getVoucherCode(appliedVoucher)} applied</strong>
                    <small>You saved {formatCurrency(appliedDiscount)}</small>
                  </div>
                  <button
                    type="button"
                    className="tb-voucher-remove"
                    onClick={handleRemoveVoucher}
                    disabled={isProcessing}
                  >
                    Remove
                  </button>
                </div>
              ) : (
                <button
                  type="button"
                  className="tb-voucher-open-button"
                  onClick={() => {
                    setVoucherMessage("");
                    setShowVoucherModal(true);
                  }}
                >
                  <span className="tb-voucher-open-icon">%</span>
                  <span className="tb-voucher-open-copy">
                    <strong>Choose or enter a voucher</strong>
                    <small>Admin and organizer offers</small>
                  </span>
                  <span className="tb-voucher-open-arrow">→</span>
                </button>
              )}

              {voucherError && (
                <p className="tb-voucher-inline-error" role="alert">
                  {voucherError}
                </p>
              )}
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
                      selectedPaymentMethod === method.id
                        ? "is-selected"
                        : "",
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
                      onChange={() =>
                        setSelectedPaymentMethod(method.id)
                      }
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
            </article>
          </section>

          <aside className="tb-payment-sidebar">
            <section className="tb-panel tb-countdown-panel">
              <div className="tb-countdown-label">
                TIME LEFT TO COMPLETE
              </div>
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
              <div className="tb-panel-heading tb-order-panel-heading">
                <div className="tb-order-heading-copy">
                  <span className="tb-eyebrow">ORDER</span>
                  <h2>Booking summary</h2>
                </div>

                <button
                  type="button"
                  className="tb-change-tickets"
                  onClick={() => {
                    setCancelHoldError("");
                    setShowCancelHoldDialog(true);
                  }}
                >
                  Choose different tickets
                </button>
              </div>

              {(holdData.seatIds ?? []).length > 0 && (
                <button
                  type="button"
                  className="tb-change-seats"
                  onClick={() => setShowSeatSelection(true)}
                  disabled={isExpired || isProcessing}
                >
                  Change Seat Selection
                </button>
              )}

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
                        {item.ticketName ||
                          `Ticket ${item.ticketTypeId}`}
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
                <span>Subtotal</span>
                <strong>{formatCurrency(totalAmount)}</strong>
              </div>

              {appliedVoucher && (
                <div className="tb-summary-total tb-summary-discount">
                  <span>Voucher discount</span>
                  <strong>-{formatCurrency(appliedDiscount)}</strong>
                </div>
              )}

              <div className="tb-summary-total">
                <span>Total due</span>
                <strong>{formatCurrency(payableAmount)}</strong>
              </div>

              <button
                type="button"
                className="tb-pay-button"
                onClick={handlePayment}
                disabled={
                  isProcessing ||
                  timeLeft <= 0 ||
                  items.length === 0 ||
                  isApplyingVoucher
                }
              >
                {isProcessing
                  ? "Preparing payment..."
                  : `Continue with ${
                      selectedPaymentMethod === "vnpay"
                        ? "VNPay"
                        : "payment"
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

      {showVoucherModal && (
        <div
          className="voucher-modal-backdrop"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              setShowVoucherModal(false);
            }
          }}
        >
          <section
            className="voucher-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="voucher-modal-title"
          >
            <header className="voucher-modal-header">
              <div>
                <span className="tb-eyebrow">CHECKOUT DISCOUNTS</span>
                <h2 id="voucher-modal-title">Apply a voucher</h2>
                <p>Choose an offer or enter a voucher code.</p>
              </div>
              <button
                type="button"
                className="voucher-modal-close"
                aria-label="Close"
                onClick={() => setShowVoucherModal(false)}
              >
                ×
              </button>
            </header>

            <form
              className="voucher-manual-form"
              onSubmit={(event) => {
                event.preventDefault();
                handleApplyVoucher();
              }}
            >
              <label htmlFor="voucherCode">Voucher code</label>
              <div className="voucher-manual-control">
                <input
                  id="voucherCode"
                  value={voucherCode}
                  onChange={(event) => {
                    setVoucherCode(event.target.value);
                    setVoucherMessage("");
                  }}
                  placeholder="Enter code, e.g. A111 or OR2"
                  autoComplete="off"
                />
                <button
                  type="submit"
                  disabled={!voucherCode.trim() || isApplyingVoucher}
                >
                  {isApplyingVoucher ? "Applying..." : "Apply"}
                </button>
              </div>

              {voucherMessage && (
                <p
                  className={`voucher-feedback ${
                    appliedVoucher ? "is-success" : "is-error"
                  }`}
                  role="status"
                >
                  {voucherMessage}
                </p>
              )}
            </form>

            <div className="voucher-modal-list">
              {voucherLoading ? (
                <p className="voucher-empty">
                  Loading available vouchers...
                </p>
              ) : (
                <>
                  {renderVoucherGroup(
                    "Admin vouchers",
                    adminVouchers,
                    "No available admin vouchers."
                  )}

                  {renderVoucherGroup(
                    "Organizer vouchers",
                    organizerVouchers,
                    "No available organizer vouchers."
                  )}

                  {otherVouchers.length > 0 &&
                    renderVoucherGroup(
                      "Other vouchers",
                      otherVouchers,
                      "No other vouchers."
                    )}
                </>
              )}

              {voucherError && (
                <p className="voucher-feedback is-error" role="alert">
                  {voucherError}
                </p>
              )}
            </div>

            <footer className="voucher-modal-footer">
              <span>Order total: {formatCurrency(totalAmount)}</span>
              <button
                type="button"
                onClick={() => setShowVoucherModal(false)}
              >
                Close
              </button>
            </footer>
          </section>
        </div>
      )}

      <ChangeSeatSelectionModal
        open={showSeatSelection}
        holdId={holdId}
        eventData={event}
        holdItems={items}
        attendees={attendees}
        currentSeatIds={holdData?.seatIds ?? []}
        onClose={() => setShowSeatSelection(false)}
        onSaved={handleSeatSelectionSaved}
      />

      {showCancelHoldDialog && (
        <div
          className="hold-cancel-backdrop"
          onMouseDown={(event) => {
            if (
              event.target === event.currentTarget &&
              !isCancellingHold
            ) {
              setShowCancelHoldDialog(false);
            }
          }}
        >
          <section
            className="hold-cancel-dialog"
            role="alertdialog"
            aria-modal="true"
            aria-labelledby="hold-cancel-title"
          >
            <h2 id="hold-cancel-title">Cancel your reservation?</h2>
            <p>
              Your currently held tickets will be released so you can choose
              again.
            </p>

            {cancelHoldError && (
              <p className="hold-cancel-error" role="alert">
                {cancelHoldError}
              </p>
            )}

            <div className="hold-cancel-actions">
              <button
                type="button"
                className="hold-cancel-stay"
                disabled={isCancellingHold}
                onClick={() => setShowCancelHoldDialog(false)}
              >
                Stay here
              </button>

              <button
                type="button"
                className="hold-cancel-confirm"
                disabled={isCancellingHold}
                onClick={confirmCancelHold}
              >
                {isCancellingHold
                  ? "Cancelling..."
                  : "Cancel reservation"}
              </button>
            </div>
          </section>
        </div>
      )}

      <Footer />
    </>
  );
}
