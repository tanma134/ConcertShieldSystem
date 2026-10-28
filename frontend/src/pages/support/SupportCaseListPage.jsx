import { useEffect, useState, useMemo } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import supportCaseApi from "../../api/supportCaseApi";
import { useAuth } from "../../context/AuthContext";
import { useToast } from "../../components/ToastProvider";
import StaffShell from "../staff/StaffShell";
import AdminShell from "../admin/AdminShell";
import "../../styles/supportCaseManagement.css";

// Vector icons
const SearchIcon = () => (
  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="11" cy="11" r="8" />
    <line x1="21" y1="21" x2="16.65" y2="16.65" />
  </svg>
);

const PlusIcon = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" y1="5" x2="12" y2="19" />
    <line x1="5" y1="12" x2="19" y2="12" />
  </svg>
);

// Format helper
const formatDate = (dateStr) => {
  if (!dateStr) return "—";
  const d = new Date(dateStr);
  return d.toLocaleString("en-US", {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

export default function SupportCaseListPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const toast = useToast();

  const isAdminPath = location.pathname.startsWith("/admin");
  const Shell = isAdminPath ? AdminShell : StaffShell;

  const [cases, setCases] = useState([]);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("");
  const [searchTerm, setSearchTerm] = useState("");

  // Load support cases
  const fetchCases = async () => {
    setLoading(true);
    try {
      const res = await supportCaseApi.getCases({
        status: statusFilter || undefined,
        priority: priorityFilter || undefined,
        searchTerm: searchTerm || undefined,
      });
      setCases(res.data || []);
    } catch (err) {
      toast.error(err.response?.data?.message || "Failed to load support cases.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCases();
  }, [statusFilter, priorityFilter]);

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => {
      fetchCases();
    }, 350);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  // Quick statistics
  const stats = useMemo(() => {
    const total = cases.length;
    const open = cases.filter((c) => c.status === "Open" || c.status === "Assigned").length;
    const critical = cases.filter((c) => c.priority === "Critical" && c.status !== "Closed").length;
    const resolved = cases.filter((c) => c.status === "Resolved" || c.status === "Closed").length;
    return { total, open, critical, resolved };
  }, [cases]);

  const getStatusBadge = (status) => {
    switch (status) {
      case "Open":
        return <span className="sc-status-badge sc-status-new">Open</span>;
      case "Assigned":
        return <span className="sc-status-badge sc-status-assigned">Assigned</span>;
      case "Resolved":
        return <span className="sc-status-badge sc-status-resolved">Resolved</span>;
      case "Closed":
        return <span className="sc-status-badge sc-status-closed">Closed</span>;
      default:
        return <span className="sc-status-badge">{status}</span>;
    }
  };

  const getPriorityBadge = (priority) => {
    const p = (priority || "").toLowerCase();
    return <span className={`sc-priority-badge sc-priority-${p}`}>{priority}</span>;
  };

  return (
    <Shell title="Customer Support Cases & Disputes">
      <div className="sc-container">
        {/* Header */}
        <div className="sc-header">
          <div className="sc-header-title">
            <span className="sc-badge-pill">Support & Dispute Center</span>
            <h1>Customer Support Cases</h1>
            <p>
              {isAdminPath
                ? "Administrator portal: Monitor all cases, assign staff, and review resolutions."
                : "Support Staff portal: Manage assigned cases and review unassigned incoming requests."}
            </p>
          </div>
          <div className="sc-header-actions">
            {!isAdminPath && (
              <Link
                to="/staff/support-cases/new"
                className="sc-btn sc-btn-primary"
              >
                <PlusIcon /> Create Support Case
              </Link>
            )}
          </div>
        </div>

        {/* Stats Ribbon */}
        <div className="sc-stats-grid">
          <div className="sc-stat-card">
            <span className="sc-stat-label">Total Cases</span>
            <span className="sc-stat-value">{stats.total}</span>
          </div>
          <div className="sc-stat-card">
            <span className="sc-stat-label">Pending / In Progress</span>
            <span className="sc-stat-value" style={{ color: "#fbbf24" }}>{stats.open}</span>
          </div>
          <div className="sc-stat-card">
            <span className="sc-stat-label">Critical Priority</span>
            <span className="sc-stat-value" style={{ color: "#f87171" }}>{stats.critical}</span>
          </div>
          <div className="sc-stat-card">
            <span className="sc-stat-label">Resolved / Closed</span>
            <span className="sc-stat-value" style={{ color: "#34d399" }}>{stats.resolved}</span>
          </div>
        </div>

        {/* Filter Bar */}
        <div className="sc-filter-bar">
          <div className="sc-search-input-wrap">
            <SearchIcon />
            <input
              type="text"
              className="sc-search-input"
              placeholder="Search by case number (SC-...), title, customer name or email..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>

          <select
            className="sc-select"
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="">All Statuses</option>
            <option value="Open">Open</option>
            <option value="Assigned">Assigned</option>
            <option value="InProgress">In Progress</option>
            <option value="Resolved">Resolved</option>
            <option value="Closed">Closed</option>
          </select>

          <select
            className="sc-select"
            value={priorityFilter}
            onChange={(e) => setPriorityFilter(e.target.value)}
          >
            <option value="">All Priorities</option>
            <option value="Critical">Critical</option>
            <option value="High">High</option>
            <option value="Medium">Medium</option>
            <option value="Low">Low</option>
          </select>
        </div>

        {/* Cases Table */}
        <div className="sc-table-wrap">
          {loading ? (
            <div style={{ padding: "40px", textAlign: "center", color: "var(--sc-text-secondary)" }}>
              Loading support cases...
            </div>
          ) : cases.length === 0 ? (
            <div style={{ padding: "40px", textAlign: "center", color: "var(--sc-text-secondary)" }}>
              No support cases found matching the criteria.
            </div>
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
                  <th>Assigned Staff</th>
                  <th>Created At</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {cases.map((c) => (
                  <tr key={c.caseId}>
                    <td>
                      <span className="sc-case-number">{c.caseNumber}</span>
                    </td>
                    <td style={{ maxWidth: "260px" }}>
                      <strong style={{ color: "#f3f4f6" }}>{c.title}</strong>
                    </td>
                    <td>
                      <div>{c.customerName}</div>
                      <small style={{ color: "var(--sc-text-muted)" }}>{c.customerEmail}</small>
                    </td>
                    <td>
                      <span style={{ fontSize: "12px", color: "var(--sc-text-secondary)" }}>
                        {c.category}
                      </span>
                    </td>
                    <td>{getPriorityBadge(c.priority)}</td>
                    <td>{getStatusBadge(c.status)}</td>
                    <td>
                      {c.assignedStaffName ? (
                        <span style={{ color: "#93c5fd", fontWeight: 500 }}>
                          {c.assignedStaffName}
                        </span>
                      ) : (
                        <span style={{ color: "var(--sc-text-muted)", fontStyle: "italic" }}>
                          Unassigned
                        </span>
                      )}
                    </td>
                    <td style={{ fontSize: "12px", color: "var(--sc-text-secondary)" }}>
                      {formatDate(c.createdAt)}
                    </td>
                    <td>
                      <button
                        onClick={() =>
                          navigate(isAdminPath ? `/admin/support-cases/${c.caseId}` : `/staff/support-cases/${c.caseId}`)
                        }
                        className="sc-btn sc-btn-secondary sc-btn-sm"
                      >
                        View Details
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      </div>
    </Shell>
  );
}
