import { Routes, Route } from "react-router-dom";
import RegisterPage from "../pages/RegisterPage";
import VerifyEmailPage from "../pages/VerifyEmailPage";
import LoginPage from "../pages/LoginPage";
import ForgotPasswordPage from "../pages/ForgotPasswordPage";
import VerifyResetOtpPage from "../pages/VerifyResetOtpPage";
import ResetPasswordPage from "../pages/ResetPasswordPage";
import HomePage from "../pages/HomePage";
import ProtectedRoute from "./ProtectedRoute";
import AdminLoginPage from "../pages/AdminLoginPage";
import AdminRoute from "./AdminRoute";
import OrganizerRequestsAdminPage from "../pages/admin/OrganizerRequestsAdminPage";
import RequestOrganizerPage from "../pages/RequestOrganizerPage";

export default function AppRoutes() {
  return (
    <Routes>
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/verify-email" element={<VerifyEmailPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/verify-reset-otp" element={<VerifyResetOtpPage />} />
      <Route path="/reset-password" element={<ResetPasswordPage />} />

      <Route
        path="/"
        element={
          <ProtectedRoute>
            <HomePage />
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
        path="/admin/organizer-requests"
        element={
          <AdminRoute>
            <OrganizerRequestsAdminPage />
          </AdminRoute>
        }
      />
    </Routes>
  );
}
