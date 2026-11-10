import axiosClient from "./axiosClient";

// Các endpoint này đi qua API Gateway: /api/kyc/* -> AuthenticationAPI /api/kyc/*
const kycApi = {
  // Nội dung thông báo + đồng ý xử lý dữ liệu (kèm version)
  getConsent: () => axiosClient.get("/kyc/consent"),


  submit: (formData) =>
    axiosClient.post("/kyc/submit", formData, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 90000,
    }),

  getStatus: (ekycId) => axiosClient.get(`/kyc/status/${ekycId}`),
};

export default kycApi;