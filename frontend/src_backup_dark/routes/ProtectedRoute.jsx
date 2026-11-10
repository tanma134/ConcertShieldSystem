import { Navigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

// Pass adminOnly to also require the Admin role (e.g. moderation pages).
// Pass organizerOnly to require the Organizer role; unauthenticated users go
// to /login, authenticated non-organizers go to /organizer/request so they
// can apply for the role.
// Pass staffOnly to require Staff/Validator role (or Admin/Organizer).
export default function ProtectedRoute({
  children,
  adminOnly = false,
  organizerOnly = false,
  staffOnly = false,
}) {
  const { isAuthenticated, isAdmin, isOrganizer, isStaff } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (adminOnly && !isAdmin) {
    return <Navigate to="/" replace />;
  }

  // Organizer-only pages: redirect customers to the request page
  if (organizerOnly && !isOrganizer && !isAdmin) {
    return <Navigate to="/organizer/request" replace />;
  }

  // Staff-only check-in portal: redirect non-staff to home
  if (staffOnly && !isStaff && !isAdmin && !isOrganizer) {
    return <Navigate to="/" replace />;
  }

  return children;
}
