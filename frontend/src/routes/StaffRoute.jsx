import { Navigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";

// Allows Admin and Staff. Staff only has access to the fraud-case review pages;
// the API enforces that they cannot block, restore, change policy or resolve appeals.
export default function StaffRoute({ children }) {
  const { isAuthenticated, isAdmin, isStaff } = useAuth();

  if (!isAuthenticated) {
    return <Navigate to="/admin/login" replace />;
  }

  if (!isAdmin && !isStaff) {
    return <Navigate to="/admin/login" replace />;
  }

  return children;
}
