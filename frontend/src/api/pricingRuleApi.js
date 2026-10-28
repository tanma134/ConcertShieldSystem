import axiosClient from "./axiosClient";

const pricingRuleApi = {
  getByTicketType: (ticketTypeId) =>
    axiosClient.get(`/pricingrules/tickettype/${ticketTypeId}`),

  create: (dto) => axiosClient.post(`/pricingrules`, dto),

  update: (pricingRuleId, dto) =>
    axiosClient.put(`/pricingrules/${pricingRuleId}`, dto),

  remove: (pricingRuleId) =>
    axiosClient.delete(`/pricingrules/${pricingRuleId}`),
};

export default pricingRuleApi;
