import axiosClient from "./axiosClient";

const refundPolicyApi = {
  getByEvent: (eventId) => axiosClient.get(`/refundpolicies/event/${eventId}`),
  create: (eventId, dto) =>
    axiosClient.post(`/refundpolicies/event/${eventId}`, dto),
  // UpdateRefundPolicyDTO requires refundPolicyId in the body too.
  update: (refundPolicyId, dto) =>
    axiosClient.put(`/refundpolicies/${refundPolicyId}`, {
      refundPolicyId,
      ...dto,
    }),
  remove: (refundPolicyId) =>
    axiosClient.delete(`/refundpolicies/${refundPolicyId}`),
};

export default refundPolicyApi;
