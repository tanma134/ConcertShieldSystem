import axiosClient from "./axiosClient";

/**
 * incidentApi.js
 * API Client giao tiếp với IncidentAPI thông qua API Gateway (/api/incidents)
 * Đáp ứng đầy đủ 5 Use Case:
 *   - UC_16.1: Report On-site Incident (reportIncident)
 *   - UC_16.2: Attach Evidence (uploadEvidence, getEvidences, deleteEvidence)
 *   - UC_16.3: Update Incident Report (updateIncident, updateStatus)
 *   - UC_16.4: Escalate Incident to Admin (escalateIncident)
 *   - UC_16.5: View Resolution (getResolution, resolveIncident)
 */
const incidentApi = {
  // Lấy danh sách sự cố có phân trang, lọc theo event, status, severity, category, isEscalated
  getIncidents: (params) => {
    return axiosClient.get("/incidents", { params });
  },

  // Lấy thông tin chi tiết một sự cố theo ID
  getIncidentById: (id) => {
    return axiosClient.get(`/incidents/${id}`);
  },

  // UC_16.1: Báo cáo sự cố mới tại hiện trường
  reportIncident: (data) => {
    return axiosClient.post("/incidents", data);
  },

  // UC_16.3: Cập nhật thông tin chi tiết sự cố và ghi nhận tiến độ
  updateIncident: (id, data) => {
    return axiosClient.put(`/incidents/${id}`, data);
  },

  // Cập nhật trạng thái sự cố nhanh (Reported, Investigating, InProgress, Resolved, Closed)
  updateStatus: (id, data) => {
    return axiosClient.patch(`/incidents/${id}/status`, data);
  },

  // UC_16.4: Chuyển cấp / leo thang sự cố lên Admin khi vượt thẩm quyền hoặc nghiêm trọng
  escalateIncident: (id, data) => {
    return axiosClient.post(`/incidents/${id}/escalate`, data);
  },

  // UC_16.5: Admin giải quyết sự cố và nhập kết quả giải quyết
  resolveIncident: (id, data) => {
    return axiosClient.post(`/incidents/${id}/resolve`, data);
  },

  // UC_16.5: Xem chi tiết kết quả giải quyết (Resolution)
  getResolution: (id) => {
    return axiosClient.get(`/incidents/${id}/resolution`);
  },

  // Lấy lịch sử dòng thời gian (timeline updates) của sự cố
  getHistory: (id) => {
    return axiosClient.get(`/incidents/${id}/history`);
  },

  // UC_16.2: Lấy danh sách bằng chứng đã đính kèm của sự cố
  getEvidences: (incidentId) => {
    return axiosClient.get(`/incidents/${incidentId}/evidences`);
  },

  // UC_16.2: Tải lên tệp bằng chứng (ảnh, video, audio, tài liệu) lên Cloudinary
  uploadEvidence: (incidentId, file, caption = "") => {
    const formData = new FormData();
    formData.append("file", file);
    if (caption) {
      formData.append("caption", caption);
    }
    return axiosClient.post(`/incidents/${incidentId}/evidences`, formData, {
      headers: {
        "Content-Type": "multipart/form-data",
      },
    });
  },

  // Xóa bằng chứng đính kèm
  deleteEvidence: (incidentId, evidenceId) => {
    return axiosClient.delete(`/incidents/${incidentId}/evidences/${evidenceId}`);
  },
};

export default incidentApi;
