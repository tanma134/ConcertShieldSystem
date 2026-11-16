import {useEffect,useState} from 'react';
import {Link,useParams} from 'react-router-dom';
import OrganizerShell from './OrganizerShell';
import AdminShell from '../admin/AdminShell';
import eventApi from '../../api/eventApi';
import GovernancePanel from '../../components/GovernancePanel';
import './OrganizerWizard.css';
export default function EventGovernancePage({admin=false}) {
  const {id}=useParams(),[event,setEvent]=useState(null),[error,setError]=useState('');
  const load=()=>{setError('');(admin?eventApi.adminGetById(id):eventApi.getMineById(id)).then(r=>setEvent(r.data.data)).catch(e=>setError(e.response?.data?.message||'Could not load concert.'));};
  useEffect(()=>{load();/* eslint-disable-next-line react-hooks/exhaustive-deps */},[id,admin]);
  const Shell=admin?AdminShell:OrganizerShell;
  return <Shell title="Concert governance"><div className="ow-wrap"><h1>Concert governance</h1><Link to={admin?'/admin/events':'/organizer/events'}>← Concerts</Link>{error&&<div className="ow-error">{error}<button onClick={load}>Retry</button></div>}{event?<GovernancePanel event={event} admin={admin} onUpdated={load}/>:!error&&<p>Loading concert...</p>}</div></Shell>;
}
