// Dùng chung axiosClient (BASE_URL đã có /api, tự gắn token + refresh)
import axiosClient from "./axiosClient";

const appealApi = {
  // GET api/risk/decisions/{id}  (Admin/Staff -> bản đầy đủ, Customer -> bản public của chính mình)
  getDecision: (id) => axiosClient.get(`/risk/decisions/${id}`),

  // GET api/risk/decisions/me  (Customer) - danh sách decision của chính mình. CHƯA CHẮC backend đã có: sửa URL cho khớp.
  getMyDecisions: () => axiosClient.get("/risk/decisions/me"),

  // POST api/risk/appeals  (Customer)
  submit: (body) => axiosClient.post("/risk/appeals", body),

  // GET api/admin/risk/appeals?status=Pending&page=1&pageSize=20
  search: (params) => axiosClient.get("/admin/risk/appeals", { params: clean(params) }),

  // GET api/admin/risk/appeals/{id}
  getDetail: (id) => axiosClient.get(`/admin/risk/appeals/${id}`),

  // POST api/admin/risk/appeals/{id}/resolve   body: { decision, reviewNote }
  resolve: (id, body) => axiosClient.post(`/admin/risk/appeals/${id}/resolve`, body),
};

function clean(p = {}) {
  return Object.fromEntries(Object.entries(p).filter(([, v]) => v !== "" && v != null));
}

export default appealApi;