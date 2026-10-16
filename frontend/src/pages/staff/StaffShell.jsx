import PanelShell from "../../components/PanelShell";

// Sidebar links dedicated to on-site event staff
export const STAFF_LINKS = [
  ["/staff/incidents", "On-site Incidents", true],
  ["/staff/incidents/new", "+ Report New Incident"],
  ["/staff/returns", "Ticket Returns"],
  ["/", "← Back to site"],
];

/**
 * StaffShell
 * Standard panel layout with left sidebar navigation for on-site staff
 */
export default function StaffShell({ children, title = "Staff Portal - Incident Management" }) {
  return (
    <PanelShell
      title={title}
      links={STAFF_LINKS}
      roleLabel="Staff"
      logoutPath="/login"
      navLabel="Staff navigation"
    >
      {children}
    </PanelShell>
  );
}
