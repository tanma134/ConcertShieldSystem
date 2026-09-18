import { createContext, useContext, useState } from "react";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  // =========================
  // USER
  // =========================
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem("user");

    try {
      return saved ? JSON.parse(saved) : null;
    } catch {
      localStorage.removeItem("user");
      return null;
    }
  });

  // =========================
  // ROLES
  // =========================
  const [roles, setRoles] = useState(() => {
    const saved = localStorage.getItem("roles");

    try {
      return saved ? JSON.parse(saved) : [];
    } catch {
      localStorage.removeItem("roles");
      return [];
    }
  });

  // =========================
  // LOGIN
  // LoginResponseDTO:
  // {
  //   user,
  //   accessToken,
  //   refreshToken,
  //   roles
  // }
  // =========================
  const login = ({
    user,
    accessToken,
    refreshToken,
    roles: newRoles = [],
  }) => {
    // Access Token
    if (accessToken) {
      localStorage.setItem("accessToken", accessToken);
    }

    // Refresh Token
    if (refreshToken) {
      localStorage.setItem("refreshToken", refreshToken);
    } else {
      localStorage.removeItem("refreshToken");
    }

    // User
    if (user) {
      localStorage.setItem("user", JSON.stringify(user));
      setUser(user);
    }

    // Roles
    localStorage.setItem("roles", JSON.stringify(newRoles));
    setRoles(newRoles);
  };

  // =========================
  // LOGOUT
  // =========================
  const logout = () => {
    localStorage.removeItem("accessToken");
    localStorage.removeItem("refreshToken");
    localStorage.removeItem("user");
    localStorage.removeItem("roles");

    setUser(null);
    setRoles([]);
  };

  const updateUser = (nextUser) => {
    if (!nextUser) return;
    localStorage.setItem("user", JSON.stringify(nextUser));
    setUser(nextUser);
  };

  // =========================
  // AUTHENTICATION
  // =========================
  const isAuthenticated = !!localStorage.getItem("accessToken");

  // =========================
  // ROLE CHECK
  // =========================
  const normalizedRoles = roles.map((role) =>
    String(role).toLowerCase()
  );

  const isAdmin = normalizedRoles.includes("admin");

  const isOrganizer = normalizedRoles.includes("organizer");

  // =========================
  // PROVIDER
  // =========================
  return (
    <AuthContext.Provider
      value={{
        user,
        roles,

        login,
        logout,
        updateUser,

        isAuthenticated,
        isAdmin,
        isOrganizer,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

// =========================
// useAuth HOOK
// =========================
export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error(
      "useAuth phải được dùng bên trong AuthProvider"
    );
  }

  return context;
}
