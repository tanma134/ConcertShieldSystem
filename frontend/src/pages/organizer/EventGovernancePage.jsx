import { useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import OrganizerShell from "./OrganizerShell";
import AdminShell from "../admin/AdminShell";
import eventApi from "../../api/eventApi";
import GovernancePanel from "../../components/GovernancePanel";
import "./OrganizerWizard.css";

export default function EventGovernancePage({ admin = false }) {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const fromStep = searchParams.get("fromStep");

  const [event, setEvent] = useState(null);
  const [error, setError] = useState("");

  const load = () => {
    setError("");
    (admin ? eventApi.adminGetById(id) : eventApi.getMineById(id))
      .then((r) => setEvent(r.data.data))
      .catch((e) => setError(e.response?.data?.message || "Could not load concert."));
  };

  useEffect(() => {
    load();
    /* eslint-disable-next-line react-hooks/exhaustive-deps */
  }, [id, admin]);

  const Shell = admin ? AdminShell : OrganizerShell;

  // Xác định đường dẫn quay lại chính xác:
  // Nếu là admin -> quay về chi tiết duyệt sự kiện admin
  // Nếu là organizer -> quay về trang chỉnh sửa sự kiện, đúng step đang làm dang dở!
  const backUrl = admin
    ? `/admin/events/${id}`
    : `/organizer/events/${id}/edit${fromStep ? `?step=${fromStep}` : ""}`;

  const backLabel = admin ? "← Back to Event Review" : "← Back to Event Editor";

  return (
    <Shell title="Concert governance">
      <div className="ow-wrap">
        <div className="ow-head" style={{ marginBottom: "20px" }}>
          <div>
            <h1>Concert governance</h1>
            {event && (
              <p className="ow-sub">
                {event.title} · Compliance documents and schedule change management
              </p>
            )}
          </div>
          <Link
            to={backUrl}
            className="tb-btn tb-btn-outline ow-btn-sm"
            style={{ textDecoration: "none", whiteSpace: "nowrap" }}
          >
            {backLabel}
          </Link>
        </div>

        {error && (
          <div className="ow-error">
            {error} <button onClick={load}>Retry</button>
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
