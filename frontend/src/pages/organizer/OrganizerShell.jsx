import PanelShell from "../../components/PanelShell";

// Concert-scoped pages (revenue, check-in, seating, pricing, refunds) open from "My Concerts".
export const ORGANIZER_LINKS = [
  ["/organizer/dashboard", "Dashboard", true],
  ["/organizer/events", "My Concerts", true],
  ["/organizer/events/new", "Create Concert", true],
  ["/organizer/staff", "Staff Management", false],
  ["/organizer/checkin", "Check-in Staff", false],
  ["/organizer/realtime-checkin", "Check-in Live Monitor", false],
  ["/organizer/vouchers", "Vouchers", false],
  ["/notifications", "Notifications", false],
  ["/", "← Back to site", true],
];

export default function OrganizerShell({ children, title = "Organizer Panel", theme = "default" }) {
  return (
    <PanelShell
      title={title}
      links={ORGANIZER_LINKS}
      roleLabel="Organizer"
      logoutPath="/login"
      navLabel="Organizer navigation"
      theme={theme}
    >
      {children}
    </PanelShell>
  );
}
