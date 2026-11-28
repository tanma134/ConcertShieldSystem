import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import AppRoutes from "./routes/AppRoutes";
import NotificationToast from "./components/NotificationToast";
import { ToastProvider } from "./components/ToastProvider";
import ActiveHoldGuard from "./components/ActiveHoldGuard";
import ErrorBoundary from "./components/ErrorBoundary";
import ChallengeModal from "./components/ChallengeModal";

function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        <AuthProvider>
          <ToastProvider>
            <ActiveHoldGuard />
            <AppRoutes />
          </ToastProvider>
          <NotificationToast />
          <ChallengeModal />
        </AuthProvider>
      </BrowserRouter>
    </ErrorBoundary>
  );
}

export default App;
