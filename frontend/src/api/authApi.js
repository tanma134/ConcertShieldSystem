import axiosClient from "./axiosClient";

const authApi = {
  register: (data) => axiosClient.post("/auth/register", data),
  verifyEmail: (data) => axiosClient.post("/auth/verify-email", data),
  login: (data) => axiosClient.post("/auth/login", data),
  loginWithGoogle: (data) => axiosClient.post("/auth/google", data),
  refresh: (data) => axiosClient.post("/auth/refresh", data),
  logout: (data) => axiosClient.post("/auth/logout", data),
  forgotPassword: (data) => axiosClient.post("/auth/forgot-password", data),
  verifyResetOtp: (data) => axiosClient.post("/auth/verify-reset-otp", data),
  resetPassword: (data) => axiosClient.post("/auth/reset-password", data),
  getProfile: () => axiosClient.get("/auth/me"),
  getMe: () => axiosClient.get("/auth/me"),
  updateProfile: (data) => axiosClient.put("/auth/me", data),
  uploadAvatar: (file) => {
    const form = new FormData();
    form.append("file", file);
    return axiosClient.post("/auth/me/avatar", form);
  },
};

export default authApi;
