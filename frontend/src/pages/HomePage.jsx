import { useNavigate } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import authApi from "../api/authApi";

export default function HomePage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const handleLogout = async () => {
    try {
      const refreshToken = localStorage.getItem("refreshToken");
      await authApi.logout({ refreshToken });
    } catch (err) {
      // Dù API lỗi vẫn cứ xóa token ở client
    } finally {
      logout();
      navigate("/login");
    }
  };

  return (
    <div style={{ padding: 40 }}>
      <h2>Xin chào, {user?.fullName || user?.email || "bạn"} 👋</h2>
      <p>Bạn đã đăng nhập thành công.</p>
      <button onClick={handleLogout}>Đăng xuất</button>
    </div>
  );
}
