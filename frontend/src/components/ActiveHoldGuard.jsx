import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import ticketApi from "../api/ticketApi";
import { useAuth } from "../context/AuthContext";

function getResponseData(response) {
  return response?.data?.data ?? response?.data ?? null;
}

function isPending(order) {
  return String(order?.status || "").toLowerCase() === "pending";
}

export default function ActiveHoldGuard() {
  const { isAuthenticated, roles } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const [activeHold, setActiveHold] = useState(null);
  const [hasPendingOrder, setHasPendingOrder] = useState(false);
  const [checking, setChecking] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [secondsLeft, setSecondsLeft] = useState(0);

  const isCustomer = roles.some((role) => {
    const name = String(role?.roleName || role?.name || role).toLowerCase();
    return name === "customer";
  });

  useEffect(() => {
    let cancelled = false;

    async function checkActiveHold() {
      if (!isAuthenticated || !isCustomer) {
        setActiveHold(null);
        setHasPendingOrder(false);
        setChecking(false);
        return;
      }

      setChecking(true);
      setError("");

      try {
        const holdResponse = await ticketApi.getActiveHoldSession();
        const hold = getResponseData(holdResponse);

        if (!hold?.holdId) {
          if (!cancelled) {
            setActiveHold(null);
            setHasPendingOrder(false);
          }
          return;
        }

        const ordersResponse = await ticketApi.getMyOrders();
        const orders = getResponseData(ordersResponse) || [];

        const matchingOrder = orders.find(
            (order) =>
                String(order.holdId || "").toLowerCase() ===
                String(hold.holdId).toLowerCase()
            );

            const orderStatus = String(matchingOrder?.status || "").toLowerCase();
            const isPaidOrder = ["paid", "completed", "success", "succeeded"].includes(
            orderStatus
            );

            if (!cancelled && isPaidOrder) {
            setActiveHold(null);
            setHasPendingOrder(false);
            return;
            }

            const matchingPendingOrder =
            matchingOrder && isPending(matchingOrder)
                ? matchingOrder
                : null;

        if (!cancelled) {
          setActiveHold(hold);
          setHasPendingOrder(Boolean(matchingPendingOrder));
        }
      } catch (requestError) {
        if (!cancelled) {
          setError(
            requestError.response?.data?.message ||
              "Could not check your unfinished reservation."
          );
        }
      } finally {
        if (!cancelled) {
          setChecking(false);
        }
      }
    }

    checkActiveHold();

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, isCustomer, location.pathname]);

  useEffect(() => {
    if (!activeHold) {
      setSecondsLeft(0);
      return;
    }

    const expiresAt = Date.parse(activeHold.expiresAtUtc || "");
    const fallbackDeadline =
      Date.now() +
      Math.max(0, Number(activeHold.remainingSeconds) || 0) * 1000;

    const updateCountdown = () => {
      const deadline = Number.isFinite(expiresAt)
        ? expiresAt
        : fallbackDeadline;

      setSecondsLeft(
        Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
      );
    };

    updateCountdown();

    const timerId = window.setInterval(updateCountdown, 1000);

    return () => {
      window.clearInterval(timerId);
    };
  }, [
    activeHold?.holdId,
    activeHold?.expiresAtUtc,
    activeHold?.remainingSeconds,
  ]);

  const hasAttendees = (activeHold?.attendees || []).length > 0;

  const nextPath = hasAttendees
    ? `/checkout/payment-info/${activeHold?.holdId}`
    : `/checkout/question-form/${activeHold?.holdId}`;

  const isAlreadyAtNextStep = location.pathname === nextPath;
  const shouldBlock =
    !checking &&
    Boolean(activeHold) &&
    !isAlreadyAtNextStep;

  const minutesText = String(Math.floor(secondsLeft / 60)).padStart(2, "0");
  const secondsText = String(secondsLeft % 60).padStart(2, "0");

  function continueCheckout() {
    navigate(nextPath);
  }

  async function cancelReservation() {
    try {
      setBusy(true);
      setError("");

      await ticketApi.cancelHoldSession(activeHold.holdId);

      setActiveHold(null);
      setHasPendingOrder(false);
    } catch (requestError) {
      setError(
        requestError.response?.data?.message ||
          "Could not cancel your reservation. Please try again."
      );
    } finally {
      setBusy(false);
    }
  }

  if (checking || !shouldBlock) {
    return null;
  }

  const stageTitle = hasPendingOrder
    ? "Pending payment"
    : hasAttendees
      ? "Attendee information saved"
      : "Tickets reserved";

  const stageDescription = hasPendingOrder
    ? "You have an unpaid order. Continue to payment or cancel this reservation."
    : hasAttendees
      ? "Your attendee information is saved. Continue to payment or cancel this reservation."
      : "You have reserved tickets. Enter attendee information to continue or cancel this reservation.";

  return (
    <div className="active-hold-overlay" role="presentation">
      <section
        className="active-hold-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="active-hold-title"
      >
        <span className="active-hold-kicker">Unfinished checkout</span>

        <h2 id="active-hold-title">{stageTitle}</h2>

        <p>{stageDescription}</p>

        <p className="active-hold-timer">
          Reservation time remaining: {minutesText}:{secondsText}
        </p>

        {secondsLeft === 0 && (
          <p className="active-hold-error" role="alert">
            This reservation has expired. Please refresh the page.
          </p>
        )}

        {error && (
          <p className="active-hold-error" role="alert">
            {error}
          </p>
        )}

        <div className="active-hold-actions">
          <button
            type="button"
            className="active-hold-continue"
            onClick={continueCheckout}
            disabled={busy || secondsLeft === 0}
          >
            Continue checkout
          </button>

          <button
            type="button"
            className="active-hold-cancel"
            onClick={cancelReservation}
            disabled={busy}
          >
            Cancel reservation
          </button>
        </div>
      </section>
    </div>
  );
}
