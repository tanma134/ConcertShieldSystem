import axiosClient from "./axiosClient";

const failedTransactionApi = {
  getQueue: (page = 1, pageSize = 10) =>
    axiosClient.get("/admin/failed-transaction-queue", {
      params: { page, pageSize },
    }),
};

export default failedTransactionApi;