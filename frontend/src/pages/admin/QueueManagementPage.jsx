import { useCallback, useEffect, useState } from "react";
import AdminShell from "./AdminShell";
import queueAdminApi from "../../api/queueAdminApi";
import eventApi from "../../api/eventApi";
import "./AdminPages.css";

const initialConfiguration = { eventId: "", preQueueStartTime: "", preQueueEndTime: "", admissionRate: 10 };

export default function QueueManagementPage() {
  const [sessions, setSessions] = useState([]);
  const [events, setEvents] = useState([]);
  const [filterEventId, setFilterEventId] = useState("");
  const [configuration, setConfiguration] = useState(initialConfiguration);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  const load = useCallback(async (eventId = filterEventId) => {
    setLoading(true);
    try {
      const [sessionResponse, eventResponse] = await Promise.all([
        queueAdminApi.getSessions(eventId),
        events.length ? Promise.resolve(null) : eventApi.getAllAdmin({ page: 1, pageSize: 100, sortBy: "Title", sortOrder: "asc" }),
      ]);
      setSessions(sessionResponse.data || []);
      if (eventResponse) {
        const items = eventResponse.data?.data?.items || eventResponse.data?.data?.Items || [];
        setEvents(items);
      }
      setError("");
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Could not load queue operations.");
    } finally {
      setLoading(false);
    }
  }, [events.length, filterEventId]);

  useEffect(() => {
    const timer = window.setTimeout(() => load(), 0);
    return () => window.clearTimeout(timer);
  }, [load]);

  const updateConfiguration = (event) => {
    const { name, value } = event.target;
    setConfiguration((current) => ({ ...current, [name]: value }));
  };

  const selectedEvent = events.find((event) => String(event.eventId) === String(configuration.eventId));

  const configureQueue = async (event) => {
    event.preventDefault();
    setSaving(true);
    setMessage("");
    setError("");
    try {
      await queueAdminApi.configure(configuration.eventId, {
        preQueueStartTime: configuration.preQueueStartTime,
        preQueueEndTime: configuration.preQueueEndTime,
        admissionRate: Number(configuration.admissionRate),
        queueEnabled: true,
        fairnessPolicy: "RANDOMIZED",
      });
      setMessage("Pre-queue opened successfully.");
    } catch (requestError) {
      setError(requestError.response?.data?.code || "Could not configure the pre-queue.");
    } finally {
      setSaving(false);
    }
  };

  const revoke = async (session) => {
    if (!window.confirm("Revoke this queue token?")) return;
    try {
      await queueAdminApi.revokeSession(session.sessionId, "Manual admin revocation");
      await load();
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Could not revoke the queue token.");
    }
  };

  const unrevoke = async (session) => {
    if (!window.confirm("Restore this session so the user can register again?")) return;
    try {
      await queueAdminApi.unrevokeSession(session.sessionId);
      await load();
    } catch (requestError) {
      setError(requestError.response?.data?.message || "Could not restore the queue session.");
    }
  };

  const count = (status) => sessions.filter((session) => session.status === status).length;

  return (
    <AdminShell title="Virtual Queue Management">
      

      {error && <div className="auth-message error">{error}</div>}
      {message && <div className="auth-message success">{message}</div>}

      <section className="admin-panel queue-config-panel">
        <div className="admin-panel-head"><h2>Open Pre-Queue</h2></div>
        <form className="admin-filter-bar" onSubmit={configureQueue}>
          <label className="queue-event-select">Event<select name="eventId" required value={configuration.eventId} onChange={updateConfiguration}>
            <option value="">Select an event</option>
            {events.map((event) => <option key={event.eventId} value={event.eventId}>{event.title} (#{event.eventId})</option>)}
          </select></label>
          <label>Opens <input name="preQueueStartTime" type="datetime-local" required value={configuration.preQueueStartTime} onChange={updateConfiguration} /></label>
          <label>Closes <input name="preQueueEndTime" type="datetime-local" required value={configuration.preQueueEndTime} onChange={updateConfiguration} /></label>
          <label>Admissions/min <input name="admissionRate" type="number" min="1" required value={configuration.admissionRate} onChange={updateConfiguration} /></label>
          <button className="admin-button" type="submit" disabled={saving || !selectedEvent}>{saving ? "Opening..." : "Open Queue"}</button>
        </form>
      </section>

      <section className="summary-grid">
        {["REGISTERED", "WAITING", "ADMITTED", "BLOCKED"].map((status) => (
          <div className="summary-card" key={status}><span className="label">{status}</span><div className="value">{loading ? "..." : count(status)}</div><span className="trend">Current sessions</span></div>
        ))}
      </section>

      <section className="admin-panel">
        <div className="admin-panel-head"><div><h2>Queue sessions</h2><p className="queue-filter-caption">{filterEventId ? `Showing sessions for ${events.find((event) => String(event.eventId) === String(filterEventId))?.title || `Event #${filterEventId}`}` : "Showing sessions for all events"}</p></div><div className="queue-session-actions"><select aria-label="Filter queue sessions by event" value={filterEventId} onChange={(event) => setFilterEventId(event.target.value)}><option value="">All events</option>{events.map((event) => <option key={event.eventId} value={event.eventId}>{event.title}</option>)}</select><button className="admin-button" type="button" onClick={() => load()}>Refresh</button></div></div>
        <div className="admin-table-wrap"><table className="ow-table"><thead><tr><th>User</th><th>Event</th><th>Status</th><th>Position</th><th>Updated</th><th>Action</th></tr></thead><tbody>
          {!loading && sessions.length === 0 && <tr><td colSpan="6">No queue sessions found.</td></tr>}
          {sessions.map((session) => <tr key={session.id}><td>{session.userId}</td><td>{session.eventId}</td><td><span className="admin-status-pill">{session.status}</span></td><td>{session.position || "-"}</td><td>{new Date(session.updatedAt).toLocaleString()}</td><td>{session.status === "REVOKED" ? <button className="admin-button" type="button" onClick={() => unrevoke(session)}>Unrevoke</button> : <button className="admin-button admin-btn-danger" type="button" disabled={session.status === "BLOCKED"} onClick={() => revoke(session)}>Revoke</button>}</td></tr>)}
        </tbody></table></div>
      </section>
    </AdminShell>
  );
}
