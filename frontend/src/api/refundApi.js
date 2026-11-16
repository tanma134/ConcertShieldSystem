import axiosClient from "./axiosClient";

const refundApi = {
  // Process Full or Partial Refund
  processRefund: (payload) => axiosClient.post("/refunds/process", payload),

  // Retry Failed Refund
  retryRefund: (refundId, payload = { strategy: "Immediate" }) =>
    axiosClient.post(`/refunds/${refundId}/retry`, payload),

  // View Refund Reconciliation Report
  getReconciliationReport: (params = {}) =>
    axiosClient.get("/refunds/reconciliation", { params }),

  // Order payment & refund eligibility summary
  getOrderSummary: (orderId) => axiosClient.get(`/refunds/order/${orderId}`),

  // Single refund details
  getRefundById: (refundId) => axiosClient.get(`/refunds/${refundId}`),
};

export default refundApi;
