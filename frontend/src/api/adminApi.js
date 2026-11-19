import axiosClient from "./axiosClient";

const adminApi = {
    getUsers: () => axiosClient.get("/auth/users"),
    // Staff directory is readable by any authenticated user (used by the fraud-case assignee picker).
    getStaff: (q) => axiosClient.get("/users/staff", { params: q ? { q } : undefined }),
    getUserById: (userId) => axiosClient.get(`/auth/users/${userId}`),
    updateUser: (userId, payload) => axiosClient.put(`/auth/users/${userId}`, payload),
    updateUserStatus: (userId, status) =>
        axiosClient.put(`/auth/users/${userId}/status`, status),
    assignRole: (userId, roleId) =>
        axiosClient.post(`/auth/users/${userId}/assign-role`, { roleId }),
    getRoles: () => axiosClient.get("/roles"),
    createRole: (payload) => axiosClient.post("/roles", payload),
    updateRole: (roleId, payload) => axiosClient.put(`/roles/${roleId}`, payload),
    deleteRole: (roleId) => axiosClient.delete(`/roles/${roleId}`),
};

export default adminApi;
