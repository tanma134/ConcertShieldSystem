import axiosClient from "./axiosClient";

const appealApi = {
  getDecision: (id) => axiosClient.get(`/risk/decisions/${id}`),

  getMyDecisions: () => axiosClient.get("/risk/decisions/me"),

  submit: (body) => axiosClient.post("/risk/appeals", body),

  search: (params) => axiosClient.get("/admin/risk/appeals", { params: clean(params) }),

  getDetail: (id) => axiosClient.get(`/admin/risk/appeals/${id}`),

  resolve: (id, body) => axiosClient.post(`/admin/risk/appeals/${id}/resolve`, body),
};

function clean(p = {}) {
  return Object.fromEntries(Object.entries(p).filter(([, v]) => v !== "" && v != null));
}

export default appealApi;