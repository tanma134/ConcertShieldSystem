import axiosClient from "./axiosClient";

const policyApi = {
  // View Policy Version History (Price/Refund/Queue/Risk)
  getPolicyHistory: (category = "PRICING") =>
    axiosClient.get(`/admin/policies/history?category=${category}`),

  // Get Active Versions for All Categories
  getActivePolicies: () =>
    axiosClient.get("/admin/policies/active"),

  // Create New Policy Version
  createPolicyVersion: (data) =>
    axiosClient.post("/admin/policies/create", data),
};

export default policyApi;
