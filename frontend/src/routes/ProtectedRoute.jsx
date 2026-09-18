import { Navigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

// Pass adminOnly to also require the Admin role (e.g. moderation pages).
// A logged-in non-admin is sent home rather than to /login, since they
// are authenticated - they just don't have the right role.
export default function ProtectedRoute({ children, adminOnly = false }) {
  const { isAuthenticated, isAdmin } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (adminOnly && !isAdmin) {
    return <Navigate to="/" replace />;
  }

  return children;
}
