import axiosClient from "./axiosClient";

const kycApi = {
  getConsent: () => axiosClient.get("/kyc/consent"),


  submit: (formData) =>
    axiosClient.post("/kyc/submit", formData, {
      headers: { "Content-Type": "multipart/form-data" },
      timeout: 90000,
    }),

  getStatus: (ekycId) => axiosClient.get(`/kyc/status/${ekycId}`),
};

export default kycApi;