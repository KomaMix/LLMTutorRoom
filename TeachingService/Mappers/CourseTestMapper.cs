using TeachingService.Contracts.Models;
using TeachingService.Models;

namespace TeachingService.Mappers
{
    public static class CourseTestMapper
    {
        public static CourseTestDto ToDto(CourseTest test, bool includeHidden)
        {
            var tasks = includeHidden
                ? test.Tasks
                : test.Tasks.Where(task => !task.IsHidden);

            return new CourseTestDto
            {
                Id = test.Id.ToString(),
                Title = test.Title,
                Subject = test.Subject,
                Status = test.Status,
                Deadline = test.Deadline,
                TimeLimitMinutes = test.TimeLimitMinutes,
                Summary = test.Summary,
                TotalPoints = GetTotalPoints(test),
                Tasks = tasks
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id)
                    .Select(ToDto)
                    .ToList()
            };
        }

        public static TestTaskDto ToDto(TestTask task)
        {
            return new TestTaskDto
            {
                Id = task.Id.ToString(),
                Type = task.Type,
                CheckMode = task.CheckMode,
                Title = task.Title,
                Prompt = task.Prompt,
                MaxPoints = task.MaxPoints,
                WrongAnswerPenalty = task.WrongAnswerPenalty,
                IsHidden = task.IsHidden,
                CreatedAt = task.CreatedAt,
                Options = task.Options
                    .Select(option => new AnswerOptionDto
                    {
                        Id = option.Id.ToString(),
                        Text = option.Text,
                        IsCorrect = option.IsCorrect
                    })
                    .ToList(),
                CorrectOptionIds = GetCorrectOptionIds(task)
            };
        }

        private static decimal GetTotalPoints(CourseTest test)
        {
            return test.Tasks
                .Where(task => !task.IsHidden)
                .Sum(task => task.MaxPoints);
        }

        private static List<string> GetCorrectOptionIds(TestTask task)
        {
            return task.Options
                .Where(option => option.IsCorrect)
                .Select(option => option.Id.ToString())
                .ToList();
        }
    }
}
