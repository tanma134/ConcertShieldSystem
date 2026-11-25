import { useEffect, useState } from "react";
import PanelShell from "../../components/PanelShell";
import { useAuth } from "../../context/AuthContext";
import staffApi from "../../api/staffApi";

// Base sidebar links dedicated to on-site event staff
export const STAFF_LINKS = [
  ["/staff/gate-assignment", "My Shift & Schedule"],
  ["/staff/checkin", "Gate Check-in", true],
  ["/staff/incidents", "On-site Incidents"],
  ["/staff/support-cases", "Support Cases"],
  ["/staff/returns", "Ticket Returns"],
  ["/", "← Back to site"],
];

/**
 * StaffShell
 * Standard panel layout with left sidebar navigation for on-site staff.
 * Dynamically filters and presents navigation links according to the
 * staff member's active duty assignments (Check-in, Incidents, Support, Returns).
 */
export default function StaffShell({ children, title = "Staff Portal" }) {
  const { user, isAdmin } = useAuth();
  const [duties, setDuties] = useState({
    canCheckin: true,
    canManageIncidents: true,
    canManageSupportCases: true,
    canReviewReturns: true,
  });

  useEffect(() => {
    // Admin has access to all operational modules
    if (isAdmin) {
      setDuties({
        canCheckin: true,
        canManageIncidents: true,
        canManageSupportCases: true,
        canReviewReturns: true,
      });
      return;
    }

    const staffId = user?.userId ?? user?.id ?? user?.UserId;
    if (!staffId) return;

    staffApi.getMyAssignments(staffId)
      .then((res) => {
        const assignments = res.data?.data || [];
        if (assignments.length > 0) {
          const hasGate = assignments.some((a) => !!a.gateName);
          const hasIncidents = assignments.some((a) => !!a.canManageIncidents);
          const hasSupport = assignments.some((a) => !!a.canManageSupportCases);
          const hasReturns = assignments.some((a) => !!a.canReviewReturns);

          setDuties({
            canCheckin: hasGate,
            canManageIncidents: hasIncidents,
            canManageSupportCases: hasSupport,
            canReviewReturns: hasReturns,
          });
        }
      })
      .catch((err) => {
        console.warn("Could not load staff duties in StaffShell:", err);
      });
  }, [user?.userId, user?.id, isAdmin]);

  // Dynamically assemble visible links based on staff duties
  const dynamicLinks = [
    ["/staff/gate-assignment", "My Shift & Schedule"],
    (isAdmin || duties.canCheckin) && ["/staff/checkin", "Gate Check-in", true],
    (isAdmin || duties.canManageIncidents) && ["/staff/incidents", "On-site Incidents"],
    (isAdmin || duties.canManageSupportCases) && ["/staff/support-cases", "Support Cases"],
    (isAdmin || duties.canReviewReturns) && ["/staff/returns", "Ticket Returns"],
    ["/", "← Back to site"],
  ].filter(Boolean);

  return (
    <PanelShell
      title={title}
      links={dynamicLinks}
      roleLabel="Staff"
      logoutPath="/login"
      navLabel="Staff navigation"
    >
      {children}
    </PanelShell>
  );
}
