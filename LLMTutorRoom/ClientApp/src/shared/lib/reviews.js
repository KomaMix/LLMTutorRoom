const terminalReviewStatuses = new Set(["checked", "failed"]);

export function isTerminalReview(review) {
  return terminalReviewStatuses.has(review.status);
}

export function mergeReviews(...collections) {
  const byId = new Map();
  collections.flat().forEach(review => {
    if (review?.id != null) {
      byId.set(review.id, review);
    }
  });

  return [...byId.values()].sort((left, right) => {
    const submittedDifference = new Date(right.submittedAt).getTime()
      - new Date(left.submittedAt).getTime();
    return submittedDifference || right.id - left.id;
  });
}
