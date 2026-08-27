function requireArray(value, fieldName) {
  if (!Array.isArray(value)) {
    throw new Error(`${fieldName} must be an array.`);
  }
}

function validateReviewTaskResult(taskResult, fieldName) {
  requireArray(taskResult.findings, `${fieldName}.findings`);
  if (taskResult.answerOptions == null) {
    return;
  }

  requireArray(taskResult.answerOptions, `${fieldName}.answerOptions`);
  taskResult.answerOptions.forEach((option, optionIndex) => {
    if (!option || typeof option !== "object") {
      throw new Error(`${fieldName}.answerOptions[${optionIndex}] must be an object.`);
    }

    if (typeof option.id !== "string" || typeof option.text !== "string") {
      throw new Error(
        `${fieldName}.answerOptions[${optionIndex}] must contain string id and text.`);
    }
  });
}

export function parseOverview(data) {
  if (!data || typeof data !== "object") {
    throw new Error("Overview response must be an object.");
  }

  requireArray(data.tests, "tests");
  requireArray(data.models, "models");
  requireArray(data.reviews, "reviews");
  requireArray(data.attempts, "attempts");

  if (data.terminalReviewsNextCursor != null
    && typeof data.terminalReviewsNextCursor !== "string") {
    throw new Error("terminalReviewsNextCursor must be a string or null.");
  }

  if (!data.metrics || typeof data.metrics !== "object") {
    throw new Error("Overview response must contain metrics.");
  }

  data.tests.forEach((test, testIndex) => {
    if (!Number.isInteger(test.contentRevision) || test.contentRevision < 0) {
      throw new Error(`tests[${testIndex}].contentRevision must be a non-negative integer.`);
    }

    if (typeof test.llmModelKey !== "string") {
      throw new Error(`tests[${testIndex}].llmModelKey must be a string.`);
    }

    requireArray(test.tasks, `tests[${testIndex}].tasks`);

    test.tasks.forEach((task, taskIndex) => {
      if (typeof task.isHidden !== "boolean") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].isHidden must be a boolean.`);
      }

      if (typeof task.wrongAnswerPenalty !== "number") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].wrongAnswerPenalty must be a number.`);
      }

      if (typeof task.createdAt !== "string") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].createdAt must be a string.`);
      }

      if (typeof task.checkMode !== "string") {
        throw new Error(`tests[${testIndex}].tasks[${taskIndex}].checkMode must be a string.`);
      }

      requireArray(task.options, `tests[${testIndex}].tasks[${taskIndex}].options`);
      requireArray(task.correctOptionIds, `tests[${testIndex}].tasks[${taskIndex}].correctOptionIds`);
    });
  });

  data.reviews.forEach((review, reviewIndex) => {
    requireArray(review.taskResults, `reviews[${reviewIndex}].taskResults`);

    review.taskResults.forEach((taskResult, taskResultIndex) => {
      validateReviewTaskResult(
        taskResult,
        `reviews[${reviewIndex}].taskResults[${taskResultIndex}]`);
    });
  });

  data.attempts.forEach((attempt, attemptIndex) => {
    if (typeof attempt.id !== "string" || attempt.id.length === 0) {
      throw new Error(`attempts[${attemptIndex}].id must be a non-empty string.`);
    }

    if (typeof attempt.testId !== "string") {
      throw new Error(`attempts[${attemptIndex}].testId must be a string.`);
    }

    if (!Number.isInteger(attempt.testRevision) || attempt.testRevision < 0) {
      throw new Error(`attempts[${attemptIndex}].testRevision must be a non-negative integer.`);
    }

    if (typeof attempt.status !== "string") {
      throw new Error(`attempts[${attemptIndex}].status must be a string.`);
    }

    if (typeof attempt.startedAt !== "string") {
      throw new Error(`attempts[${attemptIndex}].startedAt must be a string.`);
    }

    if (typeof attempt.endsAt !== "string") {
      throw new Error(`attempts[${attemptIndex}].endsAt must be a string.`);
    }

    if (!attempt.answers || typeof attempt.answers !== "object" || Array.isArray(attempt.answers)) {
      throw new Error(`attempts[${attemptIndex}].answers must be an object.`);
    }
  });

  return data;
}

export function parseReviewHistoryPage(data) {
  if (!data || typeof data !== "object") {
    throw new Error("Review history response must be an object.");
  }

  requireArray(data.reviews, "reviews");
  if (data.nextCursor != null && typeof data.nextCursor !== "string") {
    throw new Error("nextCursor must be a string or null.");
  }

  data.reviews.forEach((review, reviewIndex) => {
    requireArray(review.taskResults, `reviews[${reviewIndex}].taskResults`);
    review.taskResults.forEach((taskResult, taskResultIndex) => {
      validateReviewTaskResult(
        taskResult,
        `reviews[${reviewIndex}].taskResults[${taskResultIndex}]`);
    });
  });

  return data;
}
