import { apiRequest } from "./httpClient.js";

const base = "/api/llm/models";
export const getModels = ({ signal } = {}) => apiRequest(base, { signal });
export const saveModel = (id, body) => apiRequest(id ? `${base}/${id}` : base, { method: id ? "PUT" : "POST", body });
export const saveDeployment = (modelId, id, body) => apiRequest(
  id ? `${base}/deployments/${id}` : `${base}/${modelId}/deployments`, { method: id ? "PUT" : "POST", body });
export const deleteDeployment = id => apiRequest(`${base}/deployments/${id}`, { method: "DELETE" });
export const checkDeployment = id => apiRequest(`${base}/deployments/${id}/check`, { method: "POST" });
export const saveRateLimit = (deploymentId, id, body) => apiRequest(
  id ? `${base}/rate-limits/${id}` : `${base}/deployments/${deploymentId}/rate-limits`, { method: id ? "PUT" : "POST", body });
export const deleteRateLimit = id => apiRequest(`${base}/rate-limits/${id}`, { method: "DELETE" });
