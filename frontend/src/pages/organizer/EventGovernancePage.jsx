import { useEffect, useState } from 'react';
import { useParams, useSearchParams, Link } from 'react-router-dom';
import OrganizerShell from './OrganizerShell';
import AdminShell from '../admin/AdminShell';
import eventApi from '../../api/eventApi';
import GovernancePanel from '../../components/GovernancePanel';
import './OrganizerWizard.css';

export default function EventGovernancePage({ admin = false }) {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const [event, setEvent] = useState(null);
  const [error, setError] = useState('');

  const load = () => {
    setError('');
    (admin ? eventApi.adminGetById(id) : eventApi.getMineById(id))
      .then((r) => setEvent(r.data?.data || null))
      .catch((e) => setError(e.response?.data?.message || 'Could not load concert.'));
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, admin]);

  const Shell = admin ? AdminShell : OrganizerShell;

  // Smart back navigation
  const stepParam = searchParams.get('step') || '3';
  const fromWizard = searchParams.get('from') === 'wizard' || Boolean(searchParams.get('step'));

  const backUrl = admin
    ? `/admin/events/${id}`
    : fromWizard
      ? `/organizer/events/${id}/edit?step=${stepParam}`
      : `/organizer/events/${id}/edit?step=3`;

  const backLabel = admin
    ? '← Back to event review'
    : fromWizard
      ? `← Back to Step ${stepParam} (Tickets & Seating)`
      : '← Back to concert setup';

  return (
    <Shell title="Concert governance">
      <div className="ow-wrap">
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px' }}>
          <h1 style={{ margin: 0 }}>Concert governance</h1>
          <Link
            to={backUrl}
            className="tb-btn tb-btn-outline"
            style={{
              textDecoration: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              fontSize: '13px'
            }}
          >
            {backLabel}
          </Link>
        </div>

        {error && (
          <div className="ow-error">
            {error}
            <button onClick={load} style={{ marginLeft: '12px' }}>Retry</button>
          </div>
        )}

        {event ? (
          <GovernancePanel event={event} admin={admin} onUpdated={load} />
        ) : (
          !error && <p>Loading concert...</p>
        )}
      </div>
    </Shell>
  );
}