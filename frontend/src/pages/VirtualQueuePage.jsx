import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import queueApi from "../api/queueApi";
import eventApi from "../api/eventApi";
import "./VirtualQueuePage.css";

const tokenKey = (eventId) => `queue-token:${eventId}`;
const errorMessage = (error) => {
  const code = error?.response?.data?.code;
  const status = error?.response?.status;
  const messages = {
    PREQUEUE_NOT_OPEN: "Pre-queue registration has not opened yet.",
    PREQUEUE_CLOSED: "Pre-queue registration is closed.",
    ALREADY_REGISTERED: "You are already registered for this event.",
    TOKEN_INVALID: "This queue session is no longer valid.",
    TOKEN_REVOKED: "This queue token has been revoked.",
    QUEUE_NOT_AVAILABLE: "Virtual queue is not available for this event.",
    EVENT_NOT_FOUND: "Queue configuration was not found for this event.",
    UNAUTHORIZED: "Please sign in again before entering the queue.",
  };
  if (messages[code]) return messages[code];
  if (!error?.response) return "Queue service is unavailable. Make sure QueueAPI is running on port 7310.";
  if (status === 401) return "Your session is not authorized to use this queue.";
  if (status >= 500) return "Queue service encountered a server error. Please try again.";
  return "We could not update your queue status. Please try again.";
};
const formatRemaining = (date) => { if (!date) return ""; const seconds = Math.max(0, Math.ceil((new Date(date).getTime() - Date.now()) / 1000)); return `${String(Math.floor(seconds / 60)).padStart(2, "0")} : ${String(seconds % 60).padStart(2, "0")}`; };
const formatQueueDate = (date) => date ? new Intl.DateTimeFormat("en-US", { timeZone: "Asia/Ho_Chi_Minh", dateStyle: "medium", timeStyle: "short" }).format(new Date(date)) : "Not available";
const formatEventDate = (date) => date ? new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric", hour: "numeric", minute: "2-digit" }).format(new Date(date)) : "Event details loading";

export default function VirtualQueuePage() {
  const { eventId } = useParams();
  const navigate = useNavigate();
  const [event, setEvent] = useState(null);
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [now, setNow] = useState(Date.now());
  const [token, setToken] = useState(() => localStorage.getItem(tokenKey(eventId)));
  const [eventImageState, setEventImageState] = useState({ eventId, index: 0 });
  const autoJoinKey = useRef("");

  const loadStatus = async (currentToken = token) => {
    if (!currentToken) { setLoading(false); return; }
    try { const response = await queueApi.getQueueStatus(eventId, currentToken); setStatus(response.data); setError(""); }
    catch (requestError) { setError(errorMessage(requestError)); }
    finally { setLoading(false); }
  };

  useEffect(() => { setToken(localStorage.getItem(tokenKey(eventId))); eventApi.getById ? eventApi.getById(eventId).then((response) => setEvent(response.data?.data || response.data)).catch(() => {}) : setEvent(null); loadStatus(); }, [eventId]);
  useEffect(() => { const timer = setInterval(() => setNow(Date.now()), 1000); return () => clearInterval(timer); }, []);
  useEffect(() => { if (!token) return undefined; const timer = setInterval(() => loadStatus(), 4000); return () => clearInterval(timer); }, [eventId, token]);
  useEffect(() => {
    if (status?.status !== "PREQUEUE" || !status.preQueueEndsAt || new Date(status.preQueueEndsAt).getTime() > now || !token) return;

    const key = `${eventId}:${token}`;
    if (autoJoinKey.current === key) return;
    autoJoinKey.current = key;

    setBusy(true);
    queueApi.joinQueue(eventId, token)
      .then((response) => {
        setStatus(response.data);
        if (response.data?.status === "PREQUEUE") autoJoinKey.current = "";
      })
      .catch((requestError) => {
        setError(errorMessage(requestError));
        autoJoinKey.current = "";
      })
      .finally(() => setBusy(false));
  }, [eventId, now, status?.preQueueEndsAt, status?.status, token]);

  const register = async () => { setBusy(true); setError(""); try { const response = await queueApi.registerPreQueue(eventId); const nextToken = response.data.queueToken; localStorage.setItem(tokenKey(eventId), nextToken); setToken(nextToken); setStatus(response.data); } catch (requestError) { setError(errorMessage(requestError)); } finally { setBusy(false); } };
  const join = async () => { if (!token) return; setBusy(true); try { const response = await queueApi.joinQueue(eventId, token); setStatus(response.data); } catch (requestError) { setError(errorMessage(requestError)); } finally { setBusy(false); } };
  const leave = async () => { if (!token || !window.confirm("Leave the virtual queue? Your current position will be lost.")) return; setBusy(true); try { await queueApi.leaveQueue(eventId, token); localStorage.removeItem(tokenKey(eventId)); setStatus({ status: "LEFT" }); } catch (requestError) { setError(errorMessage(requestError)); } finally { setBusy(false); } };
  const remaining = useMemo(() => formatRemaining(status?.admissionExpiresAt), [status?.admissionExpiresAt, now]);
  const preQueueRemaining = useMemo(() => formatRemaining(status?.preQueueEndsAt), [status?.preQueueEndsAt, now]);
  const positionPercent = status?.position && status?.totalWaiting ? Math.min(100, Math.round((status.position / status.totalWaiting) * 100)) : 0;
  const eventImageUrls = useMemo(() => {
    const galleryUrls = Array.isArray(event?.images)
      ? event.images
        .slice()
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
        .map((image) => image.imageUrl)
      : [];
    return [...new Set([event?.bannerUrl, event?.posterUrl, ...galleryUrls].filter(Boolean))];
  }, [event]);
  const eventImageIndex = eventImageState.eventId === eventId ? eventImageState.index : 0;

  return (
    <div className="tb-app">
      <Header />
      <main className="queue-page">
        <Link className="queue-back" to={event ? `/events/${event.slug}` : "/"}>
          <span aria-hidden="true">←</span> Back to event
        </Link>
        <section className="queue-panel">
          <div className="queue-panel-head">
            <div>
              <span className="queue-kicker">ConcertShield / Fair access</span>
              <p className="queue-subtitle">Virtual ticket queue</p>
            </div>
            <span className="queue-live"><i /> Live updates</span>
          </div>

          <div className="queue-layout">
            <aside className="queue-event-card">
              {eventImageUrls[eventImageIndex] ? (
                <img
                  src={eventImageUrls[eventImageIndex]}
                  alt={event?.title || "Event"}
                  onError={() => setEventImageState({ eventId, index: eventImageIndex + 1 })}
                />
              ) : <div className="queue-event-placeholder" role="img" aria-label="Event image unavailable">CS</div>}
              <div className="queue-event-copy">
                <span className="queue-event-label">Your event</span>
                <h1>{event?.title || "Your ticket access"}</h1>
                <p>{formatEventDate(event?.startsAt)}</p>
                {(event?.locationName || event?.city) && <p>{[event.locationName, event.city].filter(Boolean).join(", ")}</p>}
              </div>
            </aside>

            <div className="queue-content">
              {loading && <div className="queue-skeleton"><span className="queue-spinner" /> Checking your queue session...</div>}
              {error && <div className="queue-error" role="alert">{error}</div>}
              {!loading && !status && <div className="queue-empty-state"><div className="queue-step">01 / 03</div><h2>Fair access starts here</h2><p>Register during the pre-queue window. When it closes, everyone receives a randomized place.</p><button className="tb-btn-primary" onClick={register} disabled={busy}>{busy ? "Registering..." : "Register for Pre-Queue"}</button></div>}
              {status?.status === "REGISTERED" && <div className="queue-empty-state"><div className="queue-state-icon">✓</div><div className="queue-step">02 / 03</div><h2>You&apos;re registered</h2><p>Your registration is saved. Join the queue to begin the waiting countdown.</p><div className="queue-window-times"><div><span>Pre-queue opens</span><strong>{formatQueueDate(status.preQueueStartsAt)}</strong></div><div><span>Pre-queue closes</span><strong>{formatQueueDate(status.preQueueEndsAt)}</strong></div></div><button className="tb-btn-primary" onClick={join} disabled={busy}>{busy ? "Joining..." : "Join Queue"}</button></div>}
              {status?.status === "PREQUEUE" && <div className="queue-waiting"><div className="queue-step">02 / 03 · Joined</div><div className="queue-state-icon success">✓</div><h2>You&apos;re in the pre-queue</h2><p>Your registration is confirmed. When the countdown ends, your place will be randomized automatically.</p><div className="queue-countdown">{preQueueRemaining || "00 : 00"}</div><span className="queue-countdown-label">{busy ? "Assigning your queue position..." : "Time until pre-queue closes"}</span><p className="queue-help">Keep this page open; after randomization you&apos;ll see your place or be admitted.</p><button className="tb-btn-secondary" onClick={leave} disabled={busy}>{busy ? "Please wait..." : "Leave Queue"}</button></div>}
              {status?.status === "WAITING" && <div className="queue-waiting"><div className="queue-step">03 / 03 · In progress</div><p className="queue-status-label">You&apos;re in the queue</p><div className="queue-position">#{status.position || "--"}</div><p className="queue-total">of {status.totalWaiting || "--"} people waiting</p><div className="queue-progress" role="progressbar" aria-valuenow={status.position || 0} aria-valuemax={status.totalWaiting || 0}><span style={{ width: `${positionPercent}%` }} /></div><div className="queue-metrics"><div><b>{Math.max(0, (status.position || 1) - 1)}</b><span>People ahead</span></div><div><b>~{status.estimatedWaitTimeMinutes || "--"} min</b><span>Estimated wait</span></div></div><p className="queue-help">Keep this page open. Your place refreshes automatically every few seconds.</p><button className="tb-btn-secondary" onClick={leave} disabled={busy}>{busy ? "Leaving..." : "Leave Queue"}</button></div>}
              {["ADMITTED", "PURCHASE_WINDOW"].includes(status?.status) && <div className="queue-admitted"><div className="queue-state-icon success">✓</div><div className="queue-step success-text">Access granted</div><h2>You&apos;re in!</h2><p>Your purchase window is open. Choose your tickets before the timer runs out.</p><div className="queue-countdown">{remaining || "00 : 00"}</div><span>Admission access remaining</span><Link className="tb-btn-primary" to={`/events/${eventId}/select-tickets`}>Buy ticket <span aria-hidden="true">→</span></Link></div>}
              {status?.status === "COMPLETED" && <div className="queue-empty-state"><div className="queue-state-icon success">✓</div><h2>Purchase Completed</h2><p>Your ticket purchase was completed successfully.</p><button className="tb-btn-primary" disabled>View My Tickets</button></div>}
              {["LEFT", "EXPIRED", "REVOKED", "BLOCKED"].includes(status?.status) && <div className="queue-empty-state"><div className="queue-state-icon warning">!</div><h2>{status.status === "LEFT" ? "You Left the Queue" : status.status === "EXPIRED" ? "Admission Expired" : status.status === "BLOCKED" ? "Access Restricted" : "Queue Access Revoked"}</h2><p>{status.status === "LEFT" ? "You have left the virtual queue." : status.status === "EXPIRED" ? "Your ticket purchasing window has expired because no purchase was completed." : status.status === "BLOCKED" ? "Your access to this virtual queue has been restricted." : "Your queue token has been revoked."}</p><button className="tb-btn-secondary" onClick={() => navigate(event ? `/events/${event.slug}` : "/")}>Return to Event</button></div>}
            </div>
          </div>
        </section>
      </main>
      <Footer />
    </div>
  );
}
