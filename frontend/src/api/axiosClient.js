import axios from "axios";

// TODO: point this at your actual API Gateway address
const BASE_URL = "https://localhost:7164/api";

// Endpoints that don't need (and shouldn't get) an access token attached
const PUBLIC_ENDPOINTS = [
  "/auth/login",
  "/auth/register",
  "/auth/verify-email",
  "/auth/refresh",
  "/auth/forgot-password",
  "/auth/verify-reset-otp",
  "/auth/reset-password",
];

const isPublicEndpoint = (url = "") =>
  PUBLIC_ENDPOINTS.some((endpoint) => url.includes(endpoint));

const axiosClient = axios.create({
  baseURL: BASE_URL,
  headers: { "Content-Type": "application/json" },
});

// Attach the access token to every request, EXCEPT the public endpoints above
axiosClient.interceptors.request.use((config) => {
  if (config.data instanceof FormData) {
    delete config.headers["Content-Type"];
    delete config.headers["content-type"];
  }
  if (!isPublicEndpoint(config.url)) {
    const token = localStorage.getItem("accessToken");
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// Automatically refresh the token on 401 (only for requests that require
// auth - never for login/refresh itself, to avoid extra or infinite calls)
axiosClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    const shouldTryRefresh =
      error.response?.status === 401 &&
      !originalRequest._retry &&
      !isPublicEndpoint(originalRequest.url);

    if (shouldTryRefresh) {
      originalRequest._retry = true;
      try {
        const refreshToken = localStorage.getItem("refreshToken");
        if (!refreshToken) {
          throw new Error("No refresh token available");
        }

        const res = await axios.post(`${BASE_URL}/auth/refresh`, {
          refreshToken,
        });

        localStorage.setItem("accessToken", res.data.accessToken);
        if (res.data.refreshToken) {
          localStorage.setItem("refreshToken", res.data.refreshToken);
        }

        originalRequest.headers.Authorization = `Bearer ${res.data.accessToken}`;
        return axiosClient(originalRequest);
      } catch (refreshError) {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("refreshToken");
        window.location.href = "/login";
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);

export default axiosClient;