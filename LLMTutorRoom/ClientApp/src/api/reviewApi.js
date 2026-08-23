import { apiRequest } from "./httpClient.js";

export function saveManualReview(reviewId, taskId, payload) {
  return apiRequest(
    `/api/reviews/${reviewId}/tasks/${encodeURIComponent(taskId)}/manual`,
    {
      method: "PUT",
      body: payload
    }
  );
}
