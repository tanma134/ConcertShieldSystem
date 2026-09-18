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

  // LoginResponseDTO carries roles alongside the user, not inside it - keep both.
  const login = ({ user, accessToken, refreshToken, roles: newRoles }) => {
    localStorage.setItem("accessToken", accessToken);
    if (refreshToken) localStorage.setItem("refreshToken", refreshToken);
    if (user) {
      localStorage.setItem("user", JSON.stringify(user));
      setUser(user);
    }
    if (newRoles) {
      localStorage.setItem("roles", JSON.stringify(newRoles));
      setRoles(newRoles);
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
  const isOrganizer = roles.includes("Organizer");
  const isAdmin = roles.includes("Admin");

  const isAdmin = roles.some((r) => r.toLowerCase() === "admin");

  return (
    <AuthContext.Provider
      value={{ user, roles, login, logout, isAuthenticated, isOrganizer, isAdmin }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
