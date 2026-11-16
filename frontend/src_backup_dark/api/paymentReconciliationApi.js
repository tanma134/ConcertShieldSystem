import axiosClient from "./axiosClient";

const paymentReconciliationApi = {
  getExceptions: (params) =>
    axiosClient.get("/admin/payment-reconciliation/exceptions", { params }),

  resolveException: (paymentTransactionId, payload) =>
    axiosClient.post(
      `/admin/payment-reconciliation/exceptions/${encodeURIComponent(
        paymentTransactionId
      )}/resolve`,
      payload
    ),
};

export default paymentReconciliationApi;