import axiosClient from "./axiosClient";

const queueApi = {
  configure: (eventId, data) => axiosClient.post(`/queue/${eventId}/prequeue/configure`, data),
  registerPreQueue: (eventId) => axiosClient.post(`/queue/${eventId}/prequeue/register`),
  joinQueue: (eventId, token) => axiosClient.post(`/queue/${eventId}/join`, null, { headers: { "X-Queue-Token": token } }),
  getQueueStatus: (eventId, token) => axiosClient.get(`/queue/${eventId}/status`, { headers: { "X-Queue-Token": token } }),
  leaveQueue: (eventId, token) => axiosClient.delete(`/queue/${eventId}/leave`, { headers: { "X-Queue-Token": token } }),
};

export default queueApi;
