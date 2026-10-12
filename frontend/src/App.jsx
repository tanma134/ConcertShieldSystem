import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import AppRoutes from "./routes/AppRoutes";
import NotificationToast from "./components/NotificationToast";
import { ToastProvider } from "./components/ToastProvider";

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ToastProvider>
          <AppRoutes />
        </ToastProvider>
        <NotificationToast />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
