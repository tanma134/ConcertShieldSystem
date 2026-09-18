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
    </Routes>
  );
}
