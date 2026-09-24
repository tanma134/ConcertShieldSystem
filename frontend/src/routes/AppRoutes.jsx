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
import OrganizerDashboardPage from "../pages/organizer/OrganizerDashboardPage";
import CreateEventWizard from "../pages/organizer/CreateEventWizard";
import AdminEventsPage from "../pages/admin/AdminEventsPage";
import AdminAllEventsPage from "../pages/admin/AdminAllEventsPage";
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
import EventConfigurationPage from "../pages/organizer/EventConfigurationPage";
import AdminSeatingTemplatesPage from "../pages/admin/AdminSeatingTemplatesPage";

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

      {/* Any logged-in Customer may create and configure a Draft concert. */}
      <Route
        path="/organizer/dashboard"
        element={
          <ProtectedRoute>
            <OrganizerDashboardPage />
          </ProtectedRoute>
        }
      />
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
          <ProtectedRoute organizerOnly>
            <CreateEventWizard />
          </ProtectedRoute>
        }
      />
      <Route
        path="/organizer/events/:id/edit"
        element={
          <ProtectedRoute organizerOnly>
            <CreateEventWizard />
          </ProtectedRoute>
        }
      />
      {[
        ["seating", "seating"], ["pricing", "pricing"], ["refunds", "refunds"],
      ].map(([path, section]) => <Route key={path} path={`/organizer/events/:id/${path}`} element={<ProtectedRoute organizerOnly><EventConfigurationPage section={section} /></ProtectedRoute>} />)}

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
        path="/admin/events/all"
        element={
          <ProtectedRoute adminOnly>
            <AdminAllEventsPage />
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

      {/* Xac thuc danh tinh (eKYC) bang camera */}
      <Route
        path="/kyc"
        element={
          <ProtectedRoute>
            <KycPage />
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
      <Route path="/admin/seating-templates" element={<AdminRoute><AdminSeatingTemplatesPage /></AdminRoute>} />
    </Routes>
  );
}
