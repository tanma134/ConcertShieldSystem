import PanelShell from "../../components/PanelShell";

// Sidebar của Organizer. Các trang theo từng concert (revenue, check-in,
// seating, pricing, refunds) được mở từ "My Concerts".
export const ORGANIZER_LINKS = [
  ["/organizer/dashboard", "Dashboard", true],
  ["/organizer/events", "My Concerts", true],
  ["/organizer/events/new", "Create Concert", true],
  ["/organizer/vouchers", "Vouchers"],
  ["/notifications", "Notifications"],
  ["/", "← Back to site", true],
];

export default function OrganizerShell({ children, title = "Organizer Panel" }) {
  return (
    <PanelShell
      title={title}
      links={ORGANIZER_LINKS}
      roleLabel="Organizer"
      logoutPath="/login"
      navLabel="Organizer navigation"
    >
      {children}
    </PanelShell>
  );
}
