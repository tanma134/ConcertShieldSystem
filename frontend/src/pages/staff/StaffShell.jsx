import { useEffect, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import PanelShell from "../../components/PanelShell";
import { useAuth } from "../../context/AuthContext";
import staffApi from "../../api/staffApi";

// Full sidebar links dedicated to event staff across all 3 Staff Roles
export const STAFF_LINKS = [
  ["/staff/gate-assignment", "My Shift & Gate"],
  ["/staff/checkin", "Gate Check-in"],
  ["/staff/incidents", "On-site Incidents"],
  ["/staff/support-cases", "Support Cases"],
  ["/admin/fraud-cases", "Fraud Cases & Risk"],
  ["/staff/returns", "Return & Refund Requests"],
  ["/staff/ticket-status-history", "Seat/Ticket Status History"],
  ["/", "← Back to site"],
];

/**
 * StaffShell
 * Standard panel layout with full left sidebar navigation for staff.
 * All navigation items are visible on the sidebar.
 * Enforces the 3 Staff Roles defined in the Use Case Diagrams:
 *   1. Check-in Staff:
 *      - View Gate Assignment, Scan Dynamic QR / Manual Check-in / Lookup Ticket / Scan History,
 *        Offline Check-in & Sync, Report / Attach Evidence / Update / Escalate On-site Incidents
 *   2. Security/Support Staff:
 *      - View / Create / Resolve Support Cases, View & Resolve Fraud Cases, View Risk Decision Details
 *   3. Ticketing/Operations Staff:
 *      - Process Full/Partial Refund, View Seat/Ticket Status History
 * If a staff member accesses a function belonging to a role they are not assigned to,
 * an English popup appears and all inputs/operations on that page are disabled.
 */
export default function StaffShell({ children, title = "Staff Dashboard" }) {
  const { user, isAdmin } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const [roles, setRoles] = useState({
    isCheckinStaff: true,
    isSecuritySupportStaff: true,
    isTicketingOpsStaff: true,
  });
  const [dutiesLoaded, setDutiesLoaded] = useState(false);
  const [showDeniedPopup, setShowDeniedPopup] = useState(false);

  useEffect(() => {
    if (isAdmin) {
      setRoles({
        isCheckinStaff: true,
        isSecuritySupportStaff: true,
        isTicketingOpsStaff: true,
      });
      setDutiesLoaded(true);
      return;
    }

    const staffId = user?.userId ?? user?.id ?? user?.UserId;
    if (!staffId) {
      setDutiesLoaded(true);
      return;
    }

    staffApi
      .getMyAssignments(staffId)
      .then((res) => {
        const assignments = res.data?.data || [];
        if (assignments.length > 0) {
          const hasCheckinStaff = assignments.some(
            (a) =>
              (a.canCheckin !== false && !!a.gateName && a.gateName !== "General Duty") ||
              !!a.canManageIncidents
          );
          const hasSecuritySupportStaff = assignments.some((a) => !!a.canManageSupportCases);
          const hasTicketingOpsStaff = assignments.some((a) => !!a.canReviewReturns);

          setRoles({
            isCheckinStaff: hasCheckinStaff,
            isSecuritySupportStaff: hasSecuritySupportStaff,
            isTicketingOpsStaff: hasTicketingOpsStaff,
          });
        } else {
          setRoles({
            isCheckinStaff: false,
            isSecuritySupportStaff: false,
            isTicketingOpsStaff: false,
          });
        }
      })
      .catch((err) => {
        console.warn("Could not load staff roles in StaffShell:", err);
      })
      .finally(() => {
        setDutiesLoaded(true);
      });
  }, [user?.userId, user?.id, user?.UserId, isAdmin]);

  // Determine if current route requires one of the 3 Staff Roles
  const path = location.pathname;
  let restrictedFeatureName = null;
  let requiredRoleName = null;
  let isAllowed = true;

  if (!isAdmin && dutiesLoaded) {
    if (path.startsWith("/staff/checkin")) {
      restrictedFeatureName = "Gate Check-in & Offline Sync";
      requiredRoleName = "Check-in Staff";
      isAllowed = roles.isCheckinStaff;
    } else if (path.startsWith("/staff/incidents")) {
      restrictedFeatureName = "On-site Incident Reporting & Resolution";
      requiredRoleName = "Check-in Staff";
      isAllowed = roles.isCheckinStaff;
    } else if (path.startsWith("/staff/support-cases") || path.startsWith("/support-cases")) {
      restrictedFeatureName = "Support Case Management";
      requiredRoleName = "Security/Support Staff";
      isAllowed = roles.isSecuritySupportStaff;
    } else if (path.startsWith("/admin/fraud-cases") || path.startsWith("/admin/risk-decisions")) {
      restrictedFeatureName = "Fraud Cases & Risk Decisions";
      requiredRoleName = "Security/Support Staff";
      isAllowed = roles.isSecuritySupportStaff;
    } else if (path.startsWith("/staff/returns")) {
      restrictedFeatureName = "Process Full/Partial Refund";
      requiredRoleName = "Ticketing/Operations Staff";
      isAllowed = roles.isTicketingOpsStaff;
    } else if (path.startsWith("/staff/ticket-status-history") || path.startsWith("/admin/ticket-status-history")) {
      restrictedFeatureName = "View Seat/Ticket Status History";
      requiredRoleName = "Ticketing/Operations Staff";
      isAllowed = roles.isTicketingOpsStaff;
    }
  }

  useEffect(() => {
    if (dutiesLoaded && !isAllowed && restrictedFeatureName) {
      setShowDeniedPopup(true);
    } else {
      setShowDeniedPopup(false);
    }
  }, [dutiesLoaded, isAllowed, restrictedFeatureName, path]);

  return (
    <PanelShell
      title={title}
      links={STAFF_LINKS}
      roleLabel="Staff"
      logoutPath="/login"
      navLabel="Staff navigation"
    >
      {!isAllowed && restrictedFeatureName ? (
        <div style={{ position: "relative" }}>
          {/* Top Warning Banner */}
          <div
            style={{
              background: "#fef2f2",
              border: "1px solid #fecaca",
              borderLeft: "4px solid #dc2626",
              borderRadius: "10px",
              padding: "16px 20px",
              marginBottom: "18px",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              gap: "12px",
              flexWrap: "wrap",
            }}
          >
            <div>
              <strong style={{ color: "#991b1b", fontSize: "15px", display: "block", marginBottom: "4px" }}>
                Permission Denied — {requiredRoleName || restrictedFeatureName} Role Not Assigned
              </strong>
              <span style={{ color: "#7f1d1d", fontSize: "13.5px" }}>
                You have not been assigned the <strong>{requiredRoleName}</strong> role ({restrictedFeatureName}) by the Event Organizer. All inputs and actions on this page are disabled.
              </span>
            </div>
            <button
              type="button"
              onClick={() => setShowDeniedPopup(true)}
              style={{
                background: "#dc2626",
                color: "#ffffff",
                border: "none",
                borderRadius: "8px",
                padding: "8px 16px",
                fontWeight: 700,
                fontSize: "13px",
                cursor: "pointer",
              }}
            >
              View Details
            </button>
          </div>

          {/* Transparent click-catcher overlay that blocks all inputs and re-opens the popup */}
          <div
            onClick={() => setShowDeniedPopup(true)}
            style={{
              position: "relative",
              cursor: "not-allowed",
            }}
          >
            <div
               inert="true"
              style={{
                pointerEvents: "none",
                userSelect: "none",
                opacity: 0.45,
                filter: "grayscale(65%)",
              }}
            >
              {children}
            </div>
          </div>
        </div>
      ) : (
        children
      )}

      {/* English Popup Modal when accessing an unassigned function */}
      {showDeniedPopup && restrictedFeatureName && (
        <div
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(15, 23, 42, 0.65)",
            backdropFilter: "blur(4px)",
            zIndex: 10000,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            padding: "20px",
          }}
          onClick={() => setShowDeniedPopup(false)}
        >
          <div
            style={{
              background: "#ffffff",
              borderRadius: "14px",
              maxWidth: "460px",
              width: "100%",
              padding: "24px",
              boxShadow: "0 20px 40px rgba(0, 0, 0, 0.35)",
              borderTop: "4px solid #dc2626",
              color: "#111827",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "12px" }}>
              <h3 style={{ margin: 0, fontSize: "18px", fontWeight: 700, color: "#dc2626" }}>
                Permission Not Assigned
              </h3>
              <button
                type="button"
                onClick={() => setShowDeniedPopup(false)}
                style={{
                  background: "none",
                  border: "none",
                  fontSize: "20px",
                  cursor: "pointer",
                  color: "#6b7280",
                  fontWeight: 700,
                }}
              >
                ×
              </button>
            </div>
            <p style={{ margin: "0 0 18px", fontSize: "14px", lineHeight: 1.6, color: "#1f2937" }}>
              You have not been assigned the <strong>{requiredRoleName}</strong> role (required for <strong>{restrictedFeatureName}</strong>). You do not have permission to enter data or perform any actions in this section. Please contact your Event Organizer if you require access.
            </p>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: "10px" }}>
              <button
                type="button"
                onClick={() => navigate("/staff/gate-assignment")}
                style={{
                  background: "#f3f4f6",
                  color: "#111827",
                  border: "1px solid #d1d5db",
                  borderRadius: "8px",
                  padding: "9px 16px",
                  fontSize: "13.5px",
                  fontWeight: 600,
                  cursor: "pointer",
                }}
              >
                Go to My Shift & Gate
              </button>
              <button
                type="button"
                onClick={() => setShowDeniedPopup(false)}
                style={{
                  background: "#dc2626",
                  color: "#ffffff",
                  border: "none",
                  borderRadius: "8px",
                  padding: "9px 18px",
                  fontSize: "13.5px",
                  fontWeight: 700,
                  cursor: "pointer",
                }}
              >
                OK, I Understand
              </button>
            </div>
          </div>
        </div>
      )}
    </PanelShell>
  );
}
