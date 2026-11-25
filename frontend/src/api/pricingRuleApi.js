import axiosClient from "./axiosClient";

// Quan ly quy tac gia dong ap dung cho hang ve danh cho Guest va Organizer
const pricingRuleApi = {
  // Lay danh sach quy tac gia dong cua hang ve danh cho Guest va Organizer
  getByTicketType: (ticketTypeId) =>
    axiosClient.get(`/pricingrules/tickettype/${ticketTypeId}`),

  // Tao moi quy tac gia dong cho hang ve danh cho Organizer
  create: (dto) => axiosClient.post(`/pricingrules`, dto),

  // Cap nhat quy tac gia dong danh cho Organizer
  update: (pricingRuleId, dto) =>
    axiosClient.put(`/pricingrules/${pricingRuleId}`, dto),

  // Xoa quy tac gia dong danh cho Organizer
  remove: (pricingRuleId) =>
    axiosClient.delete(`/pricingrules/${pricingRuleId}`),
};

export default pricingRuleApi;
