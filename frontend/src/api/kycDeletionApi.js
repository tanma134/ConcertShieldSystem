import axiosClient from "./axiosClient"; // đổi lại đúng tên/đường dẫn axios instance đang dùng trong authApi.js

const kycDeletionApi = {
  request: () => axiosClient.post("/kyc/deletion-requests"),
  getMine: () => axiosClient.get("/kyc/deletion-requests/me"),
};

export default kycDeletionApi;