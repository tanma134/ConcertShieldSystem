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
import KycPage from "../pages/KycPage";
import MyProfilePage from "../pages/MyProfilePage";
import EditProfilePage from "../pages/EditProfilePage";
import WishlistManagementPage from "../pages/admin/WishlistManagementPage";
import EventConfigurationPage from "../pages/organizer/EventConfigurationPage";
import AdminSeatingTemplatesPage from "../pages/admin/AdminSeatingTemplatesPage";
import VoucherManagementPage from "../pages/admin/VoucherManagementPage";
import NotificationCenterPage from "../pages/NotificationCenterPage";
import SelectTicketPage from "../pages/customer/SelectTicketPage"
import QuestionFormPage from "../pages/customer/QuestionFormPage";
import PaymentInfoPage from "../pages/customer/PaymentInfoPage";
import PaymentResult from "../pages/customer/PaymentResult";
import KycConsentAdminPage from "../pages/admin/KycConsentAdminPage";
import KycDeletionAdminPage from "../pages/admin/KycDeletionAdminPage";
import KycImagesAdminPage from "../pages/admin/KycImagesAdminPage";
import KycSettingsAdminPage from "../pages/admin/KycSettingsAdminPage";
import KycAccessLogAdminPage from "../pages/admin/KycAccessLogAdminPage";
import AuditLogAdminPage from "../pages/admin/AuditLogAdminPage";
import ChangePasswordPage from "../pages/ChangePasswordPage";
import FraudAlertDashboardPage from "../pages/admin/FraudAlertDashboardPage";
import FraudAlertDetailPage from "../pages/admin/FraudAlertDetailPage";
import RiskDecisionDetailPage from "../pages/admin/RiskDecisionDetailPage";
import AppealAdminListPage from "../pages/admin/AppealAdminListPage";
import RiskBlocksPage from "../pages/admin/RiskBlocksPage";
import AppealReviewPage from "../pages/admin/AppealReviewPage";
import SubmitAppealPage from "../pages/SubmitAppealPage";
import MyTicketsPage from "../pages/customer/MyTicketsPage";
import TicketDetailsPage from "../pages/customer/TicketDetailsPage";
import MyOrdersPage from "../pages/customer/MyOrdersPage";
import OrderDetailsPage from "../pages/customer/OrderDetailsPage";

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

      <Route path="/events/:eventId/select-tickets" element={<SelectTicketPage />} />
      <Route
        path="/checkout/question-form/:holdId"
        element={
          <ProtectedRoute>
            <QuestionFormPage />
          </ProtectedRoute>
        }
      />

      <Route
        path="/checkout/payment-info/:holdId"
        element={
          <ProtectedRoute>
            <PaymentInfoPage />
          </ProtectedRoute>
        }
      />
      <Route path="/payment/result" element={<PaymentResult />} />

      <Route
        path="/my-tickets"
        element={
          <ProtectedRoute>
            <MyTicketsPage />
          </ProtectedRoute>
        }
      />

      <Route
        path="/my-tickets/:ticketId"
        element={
          <ProtectedRoute>
            <TicketDetailsPage />
          </ProtectedRoute>
        }
      />

      <Route
        path="/my-orders"
        element={
          <ProtectedRoute>
            <MyOrdersPage />
          </ProtectedRoute>
        }
      />

      <Route
        path="/my-orders/:orderId"
        element={
          <ProtectedRoute>
            <OrderDetailsPage />
          </ProtectedRoute>
        }
      />
      
<Route path="/profile/change-password" element={<ProtectedRoute><ChangePasswordPage /></ProtectedRoute>} />
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
      {/* Notifications - Available for Customer & Organizer */}
      <Route path="/notifications" element={<ProtectedRoute><NotificationCenterPage /></ProtectedRoute>} />

      {/* Voucher Management - Organizer */}
      <Route path="/organizer/vouchers" element={<ProtectedRoute organizerOnly><VoucherManagementPage /></ProtectedRoute>} />

      <Route path="/admin/wishlists" element={<AdminRoute><WishlistManagementPage /></AdminRoute>} />
      <Route path="/admin/kyc/consent" element={<AdminRoute><KycConsentAdminPage /></AdminRoute>} />
      <Route path="/admin/kyc/deletion-requests" element={<AdminRoute><KycDeletionAdminPage /></AdminRoute>} />
      <Route path="/admin/kyc-images" element={<AdminRoute><KycImagesAdminPage /></AdminRoute>} />
      <Route path="/admin/kyc/settings" element={<AdminRoute><KycSettingsAdminPage /></AdminRoute>} />
      <Route path="/admin/kyc/access-logs" element={<AdminRoute><KycAccessLogAdminPage /></AdminRoute>} />
      <Route path="/admin/audit-logs" element={<AdminRoute><AuditLogAdminPage /></AdminRoute>} />
      <Route path="/admin/fraud-alerts" element={<AdminRoute><FraudAlertDashboardPage /></AdminRoute>} />
      <Route path="/admin/fraud-alerts/:id" element={<AdminRoute><FraudAlertDetailPage /></AdminRoute>} />
      <Route path="/admin/risk/decisions/:id" element={<AdminRoute><RiskDecisionDetailPage /></AdminRoute>} />
      <Route path="/admin/risk/blocks" element={<AdminRoute><RiskBlocksPage /></AdminRoute>} />
      <Route path="/admin/risk/appeals" element={<AdminRoute><AppealAdminListPage /></AdminRoute>} />
      <Route path="/admin/risk/appeals/:id" element={<AdminRoute><AppealReviewPage /></AdminRoute>} />
      <Route path="/risk/decisions/:id/appeal" element={<ProtectedRoute><SubmitAppealPage /></ProtectedRoute>} />
      <Route path="/admin/seating-templates" element={<AdminRoute><AdminSeatingTemplatesPage /></AdminRoute>} />
      <Route path="/admin/vouchers" element={<AdminRoute><VoucherManagementPage /></AdminRoute>} />
    </Routes>
  );
}
