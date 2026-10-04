import axiosClient from "./axiosClient";

const voucherApi = {
  getVouchers: (params = {}) => {
    return axiosClient.get("/v1/vouchers", { params });
  },

  getVoucherById: (id) => {
    return axiosClient.get(`/v1/vouchers/${id}`);
  },

  createVoucher: (data) => {
    return axiosClient.post("/v1/vouchers", data);
  },

  updateVoucherStatus: (id, isActive) => {
    return axiosClient.patch(`/v1/vouchers/${id}/status`, { is_active: isActive });
  },

  updateVoucher: (id, data) => {
    return axiosClient.put(`/v1/vouchers/${id}`, data);
  },

  deleteVoucher: (id) => {
    return axiosClient.delete(`/v1/vouchers/${id}`);
  },

  getVoucherUsages: (id) => {
    return axiosClient.get(`/v1/vouchers/${id}/usages`);
  },
};

export default voucherApi;
