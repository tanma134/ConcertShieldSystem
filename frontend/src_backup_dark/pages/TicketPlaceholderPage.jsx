import { Link, useParams } from "react-router-dom";
import Header from "../components/Header";
import Footer from "../components/Footer";
import "./VirtualQueuePage.css";

export default function TicketPlaceholderPage() {
  const { eventId } = useParams();
  return <div className="tb-app"><Header /><main className="queue-page"><section className="queue-panel queue-empty-state"><span className="queue-kicker">Ticket purchase</span><h1>Ticket API is not available yet</h1><p>Your queue admission is ready for the future ticket purchase flow. This placeholder does not create orders or accept payment.</p><Link className="tb-btn-primary" to={`/events/${eventId}/queue`}>Return to Queue</Link></section></main><Footer /></div>;
}
