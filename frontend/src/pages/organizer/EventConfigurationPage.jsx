import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import Header from "../../components/Header";
import eventApi from "../../api/eventApi";
import StepTicketsSeating from "./steps/StepTicketsSeating";
import "./OrganizerWizard.css";

const titles = {
  seating: "Seating Chart Management",
  pricing: "Dynamic Pricing Management",
  refunds: "Refund Policy Management",
};

export default function EventConfigurationPage({ section }) {
  const { id } = useParams();
  const [event, setEvent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const load = async () => {
    try { const res = await eventApi.getMineById(id); setEvent(res.data?.data || null); }
    catch (err) { setError(err.response?.data?.message || "Could not load this event."); }
    finally { setLoading(false); }
  };
  useEffect(() => { load(); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, [id]);

  return <div className="tb-app"><Header /><main className="tb-container ow-wrap">
    <div className="ow-head"><div><h1>{titles[section]}</h1><p className="ow-sub">{event?.title}</p></div>
      <Link className="tb-btn tb-btn-outline" to={`/organizer/events/${id}/edit`}>← Event editor</Link></div>
    {loading && <div className="tb-loading">Loading...</div>}
    {error && <div className="ow-error">{error}</div>}
    {!loading && event && <div className="ow-panel"><StepTicketsSeating eventId={Number(id)} event={event} only={section} standalone onRefresh={load} onSaved={() => {}} /></div>}
  </main></div>;
}
