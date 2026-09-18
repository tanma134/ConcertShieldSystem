import { createContext, useContext, useState } from "react";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem("user");
    return saved ? JSON.parse(saved) : null;
  });

  const [roles, setRoles] = useState(() => {
    const saved = localStorage.getItem("roles");
    return saved ? JSON.parse(saved) : [];
  });

  const login = ({ user, accessToken, refreshToken, roles }) => {
    localStorage.setItem("accessToken", accessToken);
    if (refreshToken) localStorage.setItem("refreshToken", refreshToken);
    if (user) {
      localStorage.setItem("user", JSON.stringify(user));
      setUser(user);
    }
    if (roles) {
      localStorage.setItem("roles", JSON.stringify(roles));
      setRoles(roles);
    }
  };

  const logout = () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("user");
    localStorage.removeItem("roles");
    setUser(null);
    setRoles([]);
  };

  const isAuthenticated = !!localStorage.getItem("accessToken");

  const isAdmin = roles.some((r) => r.toLowerCase() === "admin");

  return (
    <AuthContext.Provider value={{ user, roles, login, logout, isAuthenticated, isAdmin }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth phải được dùng bên trong AuthProvider");
  }
  return context;
}
