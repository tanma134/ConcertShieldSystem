import { BrowserRouter } from "react-router-dom";
import { AuthProvider } from "./context/AuthContext";
import AppRoutes from "./routes/AppRoutes";
import NotificationToast from "./components/NotificationToast";

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
        <NotificationToast />
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
