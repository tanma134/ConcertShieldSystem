import axiosClient from "./axiosClient";

const policyApi = {
  // UC_11.1: View Policy Version History (Price/Refund/Queue/Risk)
  getPolicyHistory: (category = "PRICING") =>
    axiosClient.get(`/admin/policies/history?category=${category}`),

  // Get Active Versions for All Categories
  getActivePolicies: () =>
    axiosClient.get("/admin/policies/active"),

  // UC_11.2: Create New Policy Version
  createPolicyVersion: (data) =>
    axiosClient.post("/admin/policies/create", data),
};

export default policyApi;
