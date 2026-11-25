import axiosClient from "./axiosClient";

const refundApi = {
  processRefund: (payload) => axiosClient.post("/refunds/process", payload),

  retryRefund: (refundId, payload = { strategy: "Immediate" }) =>
    axiosClient.post(`/refunds/${refundId}/retry`, payload),

  getReconciliationReport: (params = {}) =>
    axiosClient.get("/refunds/reconciliation", { params }),

  getOrderSummary: (orderId) => axiosClient.get(`/refunds/order/${orderId}`),

  getRefundById: (refundId) => axiosClient.get(`/refunds/${refundId}`),
};

export default refundApi;
