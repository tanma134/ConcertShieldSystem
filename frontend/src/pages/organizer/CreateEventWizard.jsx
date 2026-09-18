import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams, Link } from "react-router-dom";
import Header from "../../components/Header";
import eventApi from "../../api/eventApi";
import StepInfo from "./steps/StepInfo";
import StepImages from "./steps/StepImages";
import StepTicketsSeating from "./steps/StepTicketsSeating";
import StepReview from "./steps/StepReview";
import "./OrganizerWizard.css";

const STEPS = [
  { n: 1, label: "Info" },
  { n: 2, label: "Images" },
  { n: 3, label: "Tickets & Seating" },
  { n: 4, label: "Review & Submit" },
];

export default function CreateEventWizard() {
  const { id: routeId } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const [eventId, setEventId] = useState(routeId ? Number(routeId) : null);
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(!!routeId);
  const [error, setError] = useState("");
  const [step, setStep] = useState(() => {
    const fromUrl = Number(searchParams.get("step"));
    return fromUrl >= 1 && fromUrl <= 4 ? fromUrl : 1;
  });
  const [savedAt, setSavedAt] = useState(null);

  const goToStep = (n) => {
    setStep(n);
    setSearchParams({ step: String(n) }, { replace: true });
  };

  const loadEvent = useCallback(async () => {
    if (!eventId) return;
    const res = await eventApi.getMineById(eventId);
    setEvent(res.data?.data || null);
  }, [eventId]);

  useEffect(() => {
    if (!routeId) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError("");
    eventApi
      .getMineById(routeId)
      .then((res) => setEvent(res.data?.data || null))
      .catch(() =>
        setError("Could not load this event, or you are not its owner.")
      )
      .finally(() => setLoading(false));
  }, [routeId]);

  // First save of step 1 creates the draft and switches this wizard into edit
  // mode (so every step after that PUTs the same concert instead of creating
  // a new one each time).
  const handleCreated = (newEvent) => {
    setEventId(newEvent.eventId);
    setEvent(newEvent);
    navigate(`/organizer/events/${newEvent.eventId}/edit?step=1`, {
      replace: true,
    });
  };

  const handleSaved = (updatedEvent) => {
    if (updatedEvent) setEvent(updatedEvent);
    setSavedAt(new Date());
  };

  const markSaved = () => setSavedAt(new Date());

  const canLeaveStep1 = !!eventId;

  return (
    <div className="tb-app">
      <Header />

      <div className="tb-container ow-wrap">
        <div className="ow-head">
          <div>
            <h1>{routeId ? "Edit Event" : "Create New Event"}</h1>
            <p className="ow-sub">
              Events are always in the <strong>Music</strong> category. You
              can save a draft at any step and come back later.
            </p>
          </div>
          <Link to="/organizer/events" className="tb-btn tb-btn-outline">
            ← My Events
          </Link>
        </div>

        {event?.status === "Rejected" && (
          <div className="ow-banner ow-banner-rejected">
            <strong>This event was rejected.</strong>{" "}
            {event.rejectedReason
              ? `Reason: ${event.rejectedReason}`
              : "Please make changes and submit for review again."}
          </div>
        )}
        {event?.status === "Pending" && (
          <div className="ow-banner ow-banner-pending">
            This event is awaiting admin approval. You can still review the
            details below, but you cannot edit them until it is approved or
            rejected.
          </div>
        )}
        {event?.status === "Published" && (
          <div className="ow-banner ow-banner-published">
            This event has been approved and is now on public sale.
          </div>
        )}

        <ol className="ow-steps">
          {STEPS.map((s) => (
            <li
              key={s.n}
              className={
                "ow-step" +
                (s.n === step ? " active" : "") +
                (s.n < step ? " done" : "") +
                (s.n > 1 && !canLeaveStep1 ? " disabled" : "")
              }
              onClick={() => {
                if (s.n === 1 || canLeaveStep1) goToStep(s.n);
              }}
            >
              <span className="ow-step-num">{s.n < step ? "✓" : s.n}</span>
              <span className="ow-step-label">{s.label}</span>
            </li>
          ))}
        </ol>

        {savedAt && (
          <div className="ow-saved-toast">
            Draft saved at {savedAt.toLocaleTimeString("en-US")}
          </div>
        )}

        {loading && <div className="tb-loading">Loading...</div>}
        {!loading && error && <div className="tb-error">{error}</div>}

        {!loading && !error && (
          <div className="ow-panel">
            {step === 1 && (
              <StepInfo
                eventId={eventId}
                event={event}
                onCreated={handleCreated}
                onSaved={handleSaved}
                onNext={() => goToStep(2)}
              />
            )}

            {step === 2 && eventId && (
              <StepImages
                eventId={eventId}
                event={event}
                onRefresh={loadEvent}
                onSaved={markSaved}
                onBack={() => goToStep(1)}
                onNext={() => goToStep(3)}
              />
            )}

            {step === 3 && eventId && (
              <StepTicketsSeating
                eventId={eventId}
                event={event}
                onRefresh={loadEvent}
                onSaved={markSaved}
                onBack={() => goToStep(2)}
                onNext={() => goToStep(4)}
              />
            )}

            {step === 4 && eventId && (
              <StepReview
                eventId={eventId}
                event={event}
                onRefresh={loadEvent}
                onSaved={markSaved}
                onBack={() => goToStep(3)}
                onSubmitted={loadEvent}
              />
            )}
          </div>
        )}
      </div>
    </div>
  );
}
