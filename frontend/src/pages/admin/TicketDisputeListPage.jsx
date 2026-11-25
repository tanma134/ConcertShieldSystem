import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import AdminShell from "./AdminShell";
import supportCaseApi from "../../api/supportCaseApi";
import { useToast } from "../../components/ToastProvider";
import "./TicketDisputeListPage.css";
import "../../styles/supportCaseManagement.css";

const PAGE_SIZE = 20;

function formatDate(value) {
  if (!value) return "-";

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return date.toLocaleString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export default function TicketDisputeListPage() {
  const navigate = useNavigate();
  const toast = useToast();

  const [cases, setCases] = useState([]);
  const [page, setPage] = useState(1);
  const [hasNextPage, setHasNextPage] = useState(false);

  const [status, setStatus] = useState("Active");
  const [priority, setPriority] = useState("");
  const [searchTerm, setSearchTerm] = useState("");
  const [appliedSearchTerm, setAppliedSearchTerm] = useState("");

  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let isActive = true;

    async function loadDisputes() {
      setLoading(true);

      try {
        const response = await supportCaseApi.getTicketDisputes({
          page,
          pageSize: PAGE_SIZE,
          status: status && status !== "Active" ? status : undefined,
          activeOnly: status === "Active" ? true : undefined,
          priority: priority || undefined,
          searchTerm: appliedSearchTerm || undefined,
        });

        const result = Array.isArray(response.data) ? response.data : [];

        if (isActive) {
          setCases(result);
          setHasNextPage(result.length === PAGE_SIZE);
        }
      } catch (error) {
        if (isActive) {
          setCases([]);
          setHasNextPage(false);

          toast.error(
            error.response?.data?.message ||
              "Could not load ticket disputes."
          );
        }
      } finally {
        if (isActive) {
          setLoading(false);
        }
      }
    }

    loadDisputes();

    return () => {
      isActive = false;
    };
  }, [page, status, priority, appliedSearchTerm, toast]);

  function handleApplySearch(event) {
    event.preventDefault();
    setPage(1);
    setAppliedSearchTerm(searchTerm.trim());
  }

  function handleStatusChange(event) {
    setStatus(event.target.value);
    setPage(1);
  }

  function handlePriorityChange(event) {
    setPriority(event.target.value);
    setPage(1);
  }

    function handleViewDetails(caseId) {
    if (!caseId) {
        toast.error("Case ID is missing.");
        return;
    }

    navigate(`/admin/ticket-disputes/${encodeURIComponent(caseId)}`);
    }

  return (
    <AdminShell title="Ticket Dispute List">
      <div className="sc-container ticket-dispute-page">
        <header className="sc-header">
          <div className="sc-header-title">
            <span className="sc-badge-pill">Support Management</span>

            <h1>Ticket Dispute List</h1>

            <p>
              Review invalid ticket and payment dispute reports submitted by
              customers.
            </p>
          </div>
        </header>

        <form className="sc-filter-bar" onSubmit={handleApplySearch}>
          <input
            className="sc-search-input"
            type="search"
            value={searchTerm}
            onChange={(event) => setSearchTerm(event.target.value)}
            placeholder="Search case number, title, customer or email"
          />

          <select
            className="sc-select"
            value={status}
            onChange={handleStatusChange}
          >
            <option value="Active">Active reports</option>
            <option value="">All statuses</option>
            <option value="Open">Open</option>
            <option value="Closed">Closed</option>
            <option value="Dismissed">Dismissed</option>
          </select>

          <select
            className="sc-select"
            value={priority}
            onChange={handlePriorityChange}
          >
            <option value="">All priorities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>

          <button type="submit" className="sc-btn sc-btn-primary">
            Search
          </button>
        </form>

        <div className="sc-table-wrap">
          {loading ? (
            <div className="sc-empty-state"><span className="app-loading-spinner" role="status" aria-label="Loading" /></div>
          ) : cases.length === 0 ? (
            <div className="sc-empty-state">No ticket disputes found.</div>
          ) : (
            <table className="sc-table">
              <thead>
                <tr>
                  <th>Case #</th>
                  <th>Title</th>
                  <th>Customer</th>
                  <th>Category</th>
                  <th>Priority</th>
                  <th>Status</th>
                  <th>Created at</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {cases.map((caseItem) => (
                  <tr key={caseItem.caseId}>
                    <td>
                      <span className="sc-case-number">
                        {caseItem.caseNumber}
                      </span>
                    </td>

                    <td>{caseItem.title}</td>

                    <td>
                      <strong>{caseItem.customerName}</strong>
                      <br />
                      <small>{caseItem.customerEmail}</small>
                    </td>

                    <td>{caseItem.category}</td>
                    <td>{caseItem.priority}</td>
                    <td>{caseItem.status}</td>
                    <td>{formatDate(caseItem.createdAt)}</td>

                    <td>
                      <button
                        type="button"
                        className="sc-btn sc-btn-secondary sc-btn-sm"
                        onClick={() =>
                          handleViewDetails(caseItem.caseId)
                        }
                      >
                        View details
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        <div className="sc-pagination">
          <button
            type="button"
            className="sc-btn sc-btn-secondary sc-btn-sm"
            onClick={() => setPage((currentPage) => Math.max(1, currentPage - 1))}
            disabled={loading || page <= 1}
          >
            Previous
          </button>

          <span>Page {page}</span>

          <button
            type="button"
            className="sc-btn sc-btn-secondary sc-btn-sm"
            onClick={() => setPage((currentPage) => currentPage + 1)}
            disabled={loading || !hasNextPage}
          >
            Next
          </button>
        </div>
      </div>
    </AdminShell>
  );
}
