import axiosClient from "./axiosClient";

const paymentApi = {
  // "Check again" on the payment result page: replays VNPay's confirmed payment to TicketAPI.
  // Resolves to { success, message }; message explains the reason when success is false.
  reconcile: (orderId) => axiosClient.post(`/VnPay/reconcile/${orderId}`),
};

export default paymentApi;
