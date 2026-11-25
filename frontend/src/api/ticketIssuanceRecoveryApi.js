import axiosClient from "./axiosClient";

const ticketIssuanceRecoveryApi = {
  getQueue: (params) =>
    axiosClient.get("/admin/ticket-issuance-recovery-queue", { params }),
};

export default ticketIssuanceRecoveryApi;