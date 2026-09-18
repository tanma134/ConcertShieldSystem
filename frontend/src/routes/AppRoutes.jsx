import { Routes, Route } from "react-router-dom";
import RegisterPage from "../pages/RegisterPage";
import VerifyEmailPage from "../pages/VerifyEmailPage";
import LoginPage from "../pages/LoginPage";
import ForgotPasswordPage from "../pages/ForgotPasswordPage";
import VerifyResetOtpPage from "../pages/VerifyResetOtpPage";
import ResetPasswordPage from "../pages/ResetPasswordPage";
import HomePage from "../pages/HomePage";
import EventDetailPage from "../pages/EventDetailPage";
import MyConcertsPage from "../pages/organizer/MyConcertsPage";
import CreateEventWizard from "../pages/organizer/CreateEventWizard";
import AdminEventsPage from "../pages/admin/AdminEventsPage";
import AdminEventDetailPage from "../pages/admin/AdminEventDetailPage";
import ProtectedRoute from "./ProtectedRoute";
import AdminLoginPage from "../pages/AdminLoginPage";
import AdminDashboardPage from "../pages/admin/AdminDashboardPage";
import AdminRoute from "./AdminRoute";
import OrganizerRequestsAdminPage from "../pages/admin/OrganizerRequestsAdminPage";
import ReviewManagementPage from "../pages/admin/ReviewManagementPage";
import UserManagementPage from "../pages/admin/UserManagementPage";
import RoleManagementPage from "../pages/admin/RoleManagementPage";
import RequestOrganizerPage from "../pages/RequestOrganizerPage";
import MyProfilePage from "../pages/MyProfilePage";
import EditProfilePage from "../pages/EditProfilePage";
import WishlistManagementPage from "../pages/admin/WishlistManagementPage";

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/verify-email" element={<VerifyEmailPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/verify-reset-otp" element={<VerifyResetOtpPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      {/* Home page and event detail are public, just like the real Ticketbox -
          the matching EventAPI endpoints are all [AllowAnonymous]. */}
      <Route path="/" element={<HomePage />} />
      <Route path="/events/:slug" element={<EventDetailPage />} />
      <Route path="/profile" element={<ProtectedRoute><MyProfilePage /></ProtectedRoute>} />
      <Route path="/profile/edit" element={<ProtectedRoute><EditProfilePage /></ProtectedRoute>} />

      {/* Customer create/edit concert wizard - any logged-in Customer can create
          a concert without being an Organizer yet; the role is granted once an
          Admin approves it. */}
      <Route
        path="/organizer/events"
        element={
          <ProtectedRoute>
            <MyConcertsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/new"
        element={
          <ProtectedRoute>
            <CreateEventWizard />
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/:id/edit"
        element={
          <ProtectedRoute>
            <CreateEventWizard />
          </ProtectedRoute>
        }
      />

      {/* Admin moderation queue - requires the Admin role. */}
      <Route
        path="/admin/events"
        element={
          <ProtectedRoute adminOnly>
            <AdminEventsPage />
          </ProtectedRoute>
        }
      />
      <Route
        path="/admin/events/:id"
        element={
          <ProtectedRoute adminOnly>
            <AdminEventDetailPage />
          </ProtectedRoute>
        }
      />

      {/* User gui yeu cau tro thanh Organizer */}
      <Route
        path="/organizer/request"
        element={
          <ProtectedRoute>
            <RequestOrganizerPage />
          </ProtectedRoute>
        }
      />

      {/* Khu vuc Admin */}
      <Route path="/admin/login" element={<AdminLoginPage />} />
      <Route
        path="/admin"
        element={
          <AdminRoute>
            <AdminDashboardPage />
          </AdminRoute>
        }
      />
      <Route
        path="/admin/users"
        element={
          <AdminRoute>
            <UserManagementPage />
          </AdminRoute>
        }
      />
      <Route
        path="/admin/roles"
        element={
          <AdminRoute>
            <RoleManagementPage />
          </AdminRoute>
        }
      />
      <Route
        path="/admin/organizer-requests"
        element={
          <AdminRoute>
            <OrganizerRequestsAdminPage />
          </AdminRoute>
        }
      />
      <Route
        path="/admin/reviews"
        element={
          <AdminRoute>
            <ReviewManagementPage />
          </AdminRoute>
        }
      />
      <Route path="/admin/wishlists" element={<AdminRoute><WishlistManagementPage /></AdminRoute>} />
    </Routes>
  );
}
