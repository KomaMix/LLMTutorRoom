using LLMTutorRoom.DTOs;
using LLMTutorRoom.Models;

namespace LLMTutorRoom.Services
{
    public sealed class ClassroomService
    {
        private readonly object _syncRoot = new();
        private readonly List<CourseTest> _tests;
        private readonly List<SubmissionReview> _reviews;
        private int _nextReviewId = 4;

        public ClassroomService()
        {
            _tests = SeedTests();
            _reviews = SeedReviews();
        }

        public ClassroomOverview GetTeacherOverview()
        {
            lock (_syncRoot)
            {
                return new ClassroomOverview
                {
                    Tests = _tests,
                    Models = SeedModels(),
                    Reviews = _reviews
                        .OrderByDescending(r => r.SubmittedAt)
                        .ToList(),
                    Metrics = CreateMetrics()
                };
            }
        }

        public ClassroomOverview GetStudentOverview(string studentName)
        {
            lock (_syncRoot)
            {
                var tests = _tests
                    .Where(t => t.Status == "published")
                    .ToList();
                var reviews = _reviews
                    .Where(r => string.Equals(r.StudentName, studentName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.SubmittedAt)
                    .ToList();

                return new ClassroomOverview
                {
                    Tests = tests,
                    Models = Array.Empty<LanguageModel>(),
                    Reviews = reviews,
                    Metrics = CreateStudentMetrics(tests, reviews)
                };
            }
        }

        public async Task<SubmissionReview?> CreateReviewAsync(
            ReviewRequest request,
            string studentName,
            CancellationToken cancellationToken)
        {
            await Task.Delay(750, cancellationToken);

            lock (_syncRoot)
            {
                var test = _tests.SingleOrDefault(t => t.Id == request.TestId);
                if (test is null)
                    return null;

                var taskResults = test.Tasks.Select(task =>
                {
                    request.Answers.TryGetValue(task.Id, out var answer);
                    return CreateTaskReview(task, answer ?? string.Empty);
                }).ToList();

                var totalScore = taskResults.Sum(r => r.Score);
                var review = new SubmissionReview
                {
                    Id = _nextReviewId++,
                    TestId = test.Id,
                    TestTitle = test.Title,
                    StudentName = string.IsNullOrWhiteSpace(studentName)
                        ? "Студент"
                        : studentName.Trim(),
                    Status = "checked",
                    ModelKey = test.LlmModelKey,
                    SubmittedAt = DateTimeOffset.UtcNow,
                    Score = totalScore,
                    MaxScore = test.TotalPoints,
                    Summary = CreateSummary(totalScore, test.TotalPoints),
                    TaskResults = taskResults
                };

                _reviews.Add(review);
                return review;
            }
        }

        private DashboardMetrics CreateMetrics()
        {
            return new DashboardMetrics
            {
                ActiveTests = _tests.Count(t => t.Status == "published"),
                Tasks = _tests.Sum(t => t.Tasks.Count),
                PendingReviews = _reviews.Count(r => r.Status == "queued"),
                AverageScore = _reviews.Count == 0
                    ? 0
                    : Math.Round(_reviews.Average(r => r.Score / r.MaxScore * 100), 1)
            };
        }

        private static DashboardMetrics CreateStudentMetrics(
            IReadOnlyCollection<CourseTest> tests,
            IReadOnlyCollection<SubmissionReview> reviews)
        {
            var checkedReviews = reviews.Where(r => r.Status == "checked").ToList();

            return new DashboardMetrics
            {
                ActiveTests = tests.Count,
                Tasks = tests.Sum(t => t.Tasks.Count),
                PendingReviews = reviews.Count(r => r.Status == "queued"),
                AverageScore = checkedReviews.Count == 0
                    ? 0
                    : Math.Round(checkedReviews.Average(r => r.Score / r.MaxScore * 100), 1)
            };
        }

        private static TaskReviewResult CreateTaskReview(TestTask task, string answer)
        {
            var normalizedAnswer = answer.Trim();
            var score = 0m;
            var findings = new List<string>();

            if (normalizedAnswer.Length > 120)
            {
                score += task.MaxPoints * 0.45m;
                findings.Add("Есть развернутое объяснение хода решения.");
            }
            else if (normalizedAnswer.Length > 40)
            {
                score += task.MaxPoints * 0.25m;
                findings.Add("Ответ содержит базовую аргументацию, но ее нужно раскрыть.");
            }
            else
            {
                findings.Add("Ответ слишком короткий для уверенной проверки.");
            }

            var matchedKeywords = task.Keywords.Count(keyword =>
                normalizedAnswer.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            if (matchedKeywords > 0)
            {
                score += task.MaxPoints * Math.Min(0.45m, matchedKeywords * 0.15m);
                findings.Add($"Найдены ключевые понятия: {matchedKeywords}.");
            }

            if (normalizedAnswer.Contains("ошиб", StringComparison.OrdinalIgnoreCase)
                || normalizedAnswer.Contains("провер", StringComparison.OrdinalIgnoreCase)
                || normalizedAnswer.Contains("критери", StringComparison.OrdinalIgnoreCase))
            {
                score += task.MaxPoints * 0.1m;
                findings.Add("Отмечена необходимость проверки и критериев оценивания.");
            }

            score = Math.Min(task.MaxPoints, Math.Round(score, 1));

            return new TaskReviewResult
            {
                TaskId = task.Id,
                TaskTitle = task.Title,
                Score = score,
                MaxScore = task.MaxPoints,
                Feedback = score >= task.MaxPoints * 0.75m
                    ? "Решение хорошо покрывает критерии. Осталось уточнить формулировки и привести больше конкретных деталей."
                    : "Решение требует доработки: добавь явные шаги рассуждения, критерии проверки и обоснование вывода.",
                Findings = findings
            };
        }

        private static string CreateSummary(decimal score, decimal maxScore)
        {
            var percent = maxScore == 0 ? 0 : score / maxScore;
            if (percent >= 0.8m)
                return "Работа в целом соответствует критериям. Можно использовать как сильный пример после небольшой редакции.";

            if (percent >= 0.55m)
                return "Работа частично соответствует критериям. Нужна доработка аргументации и структуры ответа.";

            return "Работа пока не закрывает ключевые критерии. Рекомендуется повторная попытка после разбора замечаний.";
        }

        private static List<LanguageModel> SeedModels()
        {
            return new List<LanguageModel>
            {
                new()
                {
                    Key = "qwen2.5:32b-instruct",
                    Provider = "Ollama",
                    Status = "available",
                    Priority = 0,
                    MaxConcurrentRequests = 1
                },
                new()
                {
                    Key = "gemma3:12b",
                    Provider = "Ollama",
                    Status = "available",
                    Priority = 1,
                    MaxConcurrentRequests = 1
                },
                new()
                {
                    Key = "mixtral:8x7b-instruct",
                    Provider = "Ollama",
                    Status = "standby",
                    Priority = 2,
                    MaxConcurrentRequests = 1
                }
            };
        }

        private static List<CourseTest> SeedTests()
        {
            return new List<CourseTest>
            {
                new()
                {
                    Id = "adaptive-assessment",
                    Title = "Адаптивное оценивание с LLM",
                    Subject = "НИР / проектирование системы",
                    Status = "published",
                    LlmModelKey = "qwen2.5:32b-instruct",
                    Deadline = DateTimeOffset.UtcNow.AddDays(6),
                    Summary = "Проверка понимания архитектуры системы: роли, критерии, эталоны и риски LLM-as-a-Judge.",
                    Criteria = new List<RubricCriterion>
                    {
                        new()
                        {
                            Id = "c1",
                            Title = "Критерии оценивания",
                            Description = "Ответ явно описывает, по каким признакам решение считается корректным.",
                            MaxPoints = 4
                        },
                        new()
                        {
                            Id = "c2",
                            Title = "Обратная связь",
                            Description = "Есть не только балл, но и объяснение ошибок, причин и рекомендаций.",
                            MaxPoints = 3
                        },
                        new()
                        {
                            Id = "c3",
                            Title = "Надежность LLM",
                            Description = "Учитываются недетерминированность, эталоны и ручная экспертная проверка.",
                            MaxPoints = 3
                        }
                    },
                    Tasks = new List<TestTask>
                    {
                        new()
                        {
                            Id = "task-pipeline",
                            Title = "Пайплайн проверки",
                            Prompt = "Опиши последовательность действий системы после отправки решения учеником.",
                            MaxPoints = 5,
                            Keywords = new List<string>
                            {
                                "решение",
                                "критерии",
                                "llm",
                                "обратная связь",
                                "сохранение"
                            }
                        },
                        new()
                        {
                            Id = "task-risk",
                            Title = "Риски автоматической оценки",
                            Prompt = "Почему недостаточно просто отправить ответ ученика в LLM и принять оценку как истину?",
                            MaxPoints = 5,
                            Keywords = new List<string>
                            {
                                "недетерминированность",
                                "ошибки",
                                "эталон",
                                "критерии",
                                "эксперт"
                            }
                        }
                    },
                    ReferenceAnswers = new List<ReferenceAnswer>
                    {
                        new()
                        {
                            Id = "ref-1",
                            TaskId = "task-pipeline",
                            StudentAlias = "Эталон A",
                            Score = 4.7m,
                            Comment = "Хорошо описан путь: задача, ответ ученика, критерии, запрос к LLM, сохранение результата."
                        },
                        new()
                        {
                            Id = "ref-2",
                            TaskId = "task-risk",
                            StudentAlias = "Эталон B",
                            Score = 4.5m,
                            Comment = "Верно отмечены нестабильность модели, необходимость критериев и роль преподавателя."
                        }
                    }
                },
                new()
                {
                    Id = "technical-feedback",
                    Title = "Техническая обратная связь",
                    Subject = "Программирование",
                    Status = "draft",
                    LlmModelKey = "gemma3:12b",
                    Deadline = DateTimeOffset.UtcNow.AddDays(14),
                    Summary = "Черновик теста по проверке решений задач с разбором хода рассуждений, а не только итогового ответа.",
                    Criteria = new List<RubricCriterion>
                    {
                        new()
                        {
                            Id = "c4",
                            Title = "Корректность",
                            Description = "Решение дает правильный результат для основных и крайних случаев.",
                            MaxPoints = 5
                        },
                        new()
                        {
                            Id = "c5",
                            Title = "Разбор",
                            Description = "Студент объясняет ход решения и ограничения выбранного подхода.",
                            MaxPoints = 5
                        }
                    },
                    Tasks = new List<TestTask>
                    {
                        new()
                        {
                            Id = "task-code-review",
                            Title = "Разбор алгоритма",
                            Prompt = "Проанализируй предложенное решение и укажи, где оно может ошибаться.",
                            MaxPoints = 10,
                            Keywords = new List<string>
                            {
                                "сложность",
                                "крайний случай",
                                "проверка",
                                "ошибка"
                            }
                        }
                    },
                    ReferenceAnswers = new List<ReferenceAnswer>()
                }
            };
        }

        private static List<SubmissionReview> SeedReviews()
        {
            return new List<SubmissionReview>
            {
                new()
                {
                    Id = 1,
                    TestId = "adaptive-assessment",
                    TestTitle = "Адаптивное оценивание с LLM",
                    StudentName = "Анна Соколова",
                    Status = "checked",
                    ModelKey = "qwen2.5:32b-instruct",
                    SubmittedAt = DateTimeOffset.UtcNow.AddHours(-5),
                    Score = 8.6m,
                    MaxScore = 10,
                    Summary = "Ответ уверенно описывает пайплайн и ограничения LLM, но критерии можно сформулировать точнее.",
                    TaskResults = new List<TaskReviewResult>
                    {
                        new()
                        {
                            TaskId = "task-pipeline",
                            TaskTitle = "Пайплайн проверки",
                            Score = 4.4m,
                            MaxScore = 5,
                            Feedback = "Последовательность проверки описана полно.",
                            Findings = new List<string> { "Есть сохранение результата.", "Есть связь с критериями." }
                        },
                        new()
                        {
                            TaskId = "task-risk",
                            TaskTitle = "Риски автоматической оценки",
                            Score = 4.2m,
                            MaxScore = 5,
                            Feedback = "Риски названы корректно, но мало примеров смещений модели.",
                            Findings = new List<string> { "Упомянуты эталоны.", "Упомянута экспертная проверка." }
                        }
                    }
                },
                new()
                {
                    Id = 2,
                    TestId = "adaptive-assessment",
                    TestTitle = "Адаптивное оценивание с LLM",
                    StudentName = "Марк Волков",
                    Status = "queued",
                    ModelKey = "qwen2.5:32b-instruct",
                    SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-18),
                    Score = 0,
                    MaxScore = 10,
                    Summary = "Ожидает автоматической проверки.",
                    TaskResults = new List<TaskReviewResult>()
                },
                new()
                {
                    Id = 3,
                    TestId = "technical-feedback",
                    TestTitle = "Техническая обратная связь",
                    StudentName = "Ирина Лебедева",
                    Status = "manual-review",
                    ModelKey = "gemma3:12b",
                    SubmittedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    Score = 6.5m,
                    MaxScore = 10,
                    Summary = "LLM нашла неоднозначность в решении. Требуется ручная проверка преподавателя.",
                    TaskResults = new List<TaskReviewResult>()
                }
            };
        }
    }
}
