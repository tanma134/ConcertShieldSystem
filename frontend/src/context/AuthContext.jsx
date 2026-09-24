import { createContext, useContext, useEffect, useRef, useState } from "react";
import authApi from "../api/authApi";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const initialRoleRefreshStarted = useRef(false);
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

  // Rotate the refresh token and receive a new JWT whose role claims are read
  // from the database now (not from the old access token).
  const refreshRoles = async () => {
    const refreshToken = localStorage.getItem("refreshToken");
    if (!refreshToken) throw new Error("No refresh token available.");

    const refreshResponse = await authApi.refresh({ refreshToken });
    const refreshed = refreshResponse.data;
    localStorage.setItem("accessToken", refreshed.accessToken);
    if (refreshed.refreshToken) {
      localStorage.setItem("refreshToken", refreshed.refreshToken);
    }

    const profileResponse = await authApi.getMe();
    const profile = profileResponse.data;
    const latestRoles = refreshed.roles || profile.roles ||
      (profile.roleName ? [profile.roleName] : []);

    localStorage.setItem("roles", JSON.stringify(latestRoles));
    setRoles(latestRoles);

    const nextUser = { ...(user || {}), ...profile };
    localStorage.setItem("user", JSON.stringify(nextUser));
    setUser(nextUser);
    return latestRoles;
  };

  useEffect(() => {
    if (initialRoleRefreshStarted.current || !localStorage.getItem("refreshToken")) return;
    initialRoleRefreshStarted.current = true;
    refreshRoles().catch(() => {
      // Keep the current session state. The normal 401 interceptor will handle
      // an actually expired/revoked session on the next protected request.
    });
    // Run once on application startup; the ref also protects React StrictMode.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // =========================
  // AUTHENTICATION
  // =========================
  const isAuthenticated = !!localStorage.getItem("accessToken");

  // =========================
  // ROLE CHECK
  // =========================
  const normalizedRoles = roles.map((role) =>
    String(role?.roleName || role?.name || role).toLowerCase()
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
        refreshRoles,

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
