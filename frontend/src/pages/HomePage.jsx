import { useNavigate, Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import authApi from "../api/authApi";
import "../styles/home.css";

export default function HomePage() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const displayName = user?.fullName || user?.email || "bạn";
  const initial = displayName.charAt(0).toUpperCase();

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
    <div className="home-page">
      <div className="home-card">
        <div className="home-avatar">{initial}</div>

        <h2 className="home-greeting">Xin chào, {displayName} 👋</h2>
        <p className="home-subtitle">Bạn đã đăng nhập thành công.</p>

        <div className="home-actions">
          <Link to="/organizer/request" className="home-button home-button--primary">
            Yêu cầu trở thành Organizer
          </Link>

          <button className="home-button home-button--ghost" onClick={handleLogout}>
            Đăng xuất
          </button>
        </div>
      </div>
    </div>
  );
}
