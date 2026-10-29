import { useCallback, useEffect, useMemo, useState } from "react";
import AdminShell from "./AdminShell";
import queueAdminApi from "../../api/queueAdminApi";
import eventApi from "../../api/eventApi";
import "./AdminDashboardPage.css";
import "./AdminPages.css";

const initialConfiguration = { eventId: "", preQueueStartTime: "", preQueueEndTime: "", admissionRate: 10 };

export default function QueueManagementPage() {
  const [sessions, setSessions] = useState([]);
  const [events, setEvents] = useState([]);
  const [filterEventId, setFilterEventId] = useState("");
  const [search, setSearch] = useState("");
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
      try {
        await eventApi.update(configuration.eventId, { requiresVirtualQueue: true });
      } catch (requestError) {
        const detail = requestError.response?.data?.message || requestError.response?.data?.title;
        setError(
          detail
            ? `Queue opened, but the event could not be marked as requiring the queue: ${detail}`
            : "Queue opened, but the event could not be marked as requiring the queue. Please retry opening the queue."
        );
        return;
      }
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
  const filteredSessions = useMemo(() => {
    const keyword = search.trim().toLowerCase();
    if (!keyword) return sessions;
    return sessions.filter((session) =>
      [session.userId, session.eventId, session.status, session.position]
        .some((value) => String(value ?? "").toLowerCase().includes(keyword))
    );
  }, [sessions, search]);

  return (
    <AdminShell title="Virtual Queue Management">
      <div className="queue-management-page">
        {error && <div className="auth-message error" role="alert">{error}<button type="button" onClick={() => setError("")}>Dismiss</button></div>}
        {message && <div className="auth-message success" role="status">{message}<button type="button" onClick={() => setMessage("")}>Dismiss</button></div>}

        <section className="admin-panel">
          <div className="panel-header">
            <div>
              <h2>Open Pre-Queue</h2>
              <p className="queue-filter-caption">Configure access times and admission rate for an event.</p>
            </div>
          </div>
          <form className="queue-config-form" onSubmit={configureQueue}>
            <label>
              <span>Event</span>
              <select name="eventId" required value={configuration.eventId} onChange={updateConfiguration}>
                <option value="">Select an event</option>
                {events.map((event) => <option key={event.eventId} value={event.eventId}>{event.title} (#{event.eventId})</option>)}
              </select>
            </label>
            <label>
              <span>Opens</span>
              <input name="preQueueStartTime" type="datetime-local" required value={configuration.preQueueStartTime} onChange={updateConfiguration} />
            </label>
            <label>
              <span>Closes</span>
              <input name="preQueueEndTime" type="datetime-local" required value={configuration.preQueueEndTime} onChange={updateConfiguration} />
            </label>
            <label>
              <span>Admissions per minute</span>
              <input name="admissionRate" type="number" min="1" required value={configuration.admissionRate} onChange={updateConfiguration} />
            </label>
            <button className="admin-button" type="submit" disabled={saving || !selectedEvent}>
              {saving ? "Opening..." : "Open Queue"}
            </button>
          </form>
        </section>

        <section className="summary-grid queue-management-summary" aria-label="Queue session totals">
          {["REGISTERED", "WAITING", "ADMITTED", "BLOCKED"].map((status) => (
            <div className="summary-card" key={status}>
              <span className="label">{status}</span>
              <div className="value">{loading ? "..." : count(status)}</div>
              <span className="trend">Current sessions</span>
            </div>
          ))}
        </section>

        <section className="admin-panel">
          <div className="panel-header">
            <div>
              <h2>Queue Sessions</h2>
              <p className="queue-filter-caption">
                {filterEventId
                  ? `Showing sessions for ${events.find((event) => String(event.eventId) === String(filterEventId))?.title || `Event #${filterEventId}`}`
                  : "Showing sessions for all events"}
              </p>
            </div>
            <button className="admin-button-secondary" type="button" onClick={() => load()} disabled={loading}>
              {loading ? "Loading..." : "Refresh"}
            </button>
          </div>
          <div className="toolbar-row queue-management-toolbar">
            <label className="search-box">
              <span>Search</span>
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="User ID, event ID, status..."
              />
            </label>
            <label className="queue-filter-control">
              <span>Event</span>
              <select aria-label="Filter queue sessions by event" value={filterEventId} onChange={(event) => setFilterEventId(event.target.value)}>
                <option value="">All events</option>
                {events.map((event) => <option key={event.eventId} value={event.eventId}>{event.title}</option>)}
              </select>
            </label>
          </div>
          {loading ? (
            <div className="empty-state">Loading queue sessions...</div>
          ) : filteredSessions.length === 0 ? (
            <div className="empty-state">{search ? "No queue sessions match your search." : "No queue sessions found."}</div>
          ) : (
            <div className="table-wrap">
              <table className="user-table queue-management-table">
                <thead>
                  <tr><th>User</th><th>Event</th><th>Status</th><th>Position</th><th>Updated At</th><th>Actions</th></tr>
                </thead>
                <tbody>
                  {filteredSessions.map((session) => (
                    <tr key={session.sessionId || session.id}>
                      <td>#{session.userId}</td>
                      <td>#{session.eventId}</td>
                      <td><span className={`status-pill queue-status-${String(session.status).toLowerCase()}`}>{session.status}</span></td>
                      <td>{session.position || "-"}</td>
                      <td>{session.updatedAt ? new Date(session.updatedAt).toLocaleString() : "N/A"}</td>
                      <td>
                        <div className="table-actions">
                          {session.status === "REVOKED" ? (
                            <button className="text-button" type="button" onClick={() => unrevoke(session)}>Unrevoke</button>
                          ) : (
                            <button className="danger-button" type="button" disabled={session.status === "BLOCKED"} onClick={() => revoke(session)}>Revoke</button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>
    </AdminShell>
  );
}
