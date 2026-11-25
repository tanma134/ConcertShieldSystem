import axiosClient from "./axiosClient";

// Quan ly xu ly hoan tien va doi soat hoan tien danh cho Admin va Staff
const refundApi = {
  // Xu ly hoan tien toan phan hoac mot phan cho don hang danh cho Admin va Staff
  processRefund: (payload) => axiosClient.post("/refunds/process", payload),

  // Thu lai yeu cau hoan tien bi loi danh cho Admin va Staff
  retryRefund: (refundId, payload = { strategy: "Immediate" }) =>
    axiosClient.post(`/refunds/${refundId}/retry`, payload),

  // Xem bao cao doi soat hoan tien danh cho Admin
  getReconciliationReport: (params = {}) =>
    axiosClient.get("/refunds/reconciliation", { params }),

  // Lay thong tin tong quan ve quyen hoan tien va so du cua don hang danh cho Admin va Staff
  getOrderSummary: (orderId) => axiosClient.get(`/refunds/order/${orderId}`),

  // Xem chi tiet mot ban ghi hoan tien theo id danh cho Admin va Staff
  getRefundById: (refundId) => axiosClient.get(`/refunds/${refundId}`),
};

export default refundApi;
