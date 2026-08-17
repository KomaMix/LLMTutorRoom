using TeachingService.Contracts.Enums;
using TeachingService.Contracts.Models;
using TeachingService.Models;

namespace TeachingService.Mappers
{
    public static class CourseTestMapper
    {
        public static CourseTestDto ToDto(
            CourseTest test,
            CourseTestVersion version,
            bool includeHidden)
        {
            var tasks = includeHidden
                ? version.Tasks
                : version.Tasks.Where(task => !task.IsHidden);

            return new CourseTestDto
            {
                Id = test.Id.ToString(),
                TeacherUserId = test.TeacherUserId,
                Title = version.Title,
                Subject = version.Subject,
                Status = version.Status,
                Deadline = version.Deadline,
                TimeLimitMinutes = version.TimeLimitMinutes,
                Summary = version.Summary,
                LlmModelKey = version.LlmModelKey,
                VersionNumber = version.VersionNumber,
                ContentRevision = version.ContentRevision,
                PublishedVersionNumber = test.Versions
                    .Where(item => item.Status == CourseTestStatus.Published)
                    .Select(item => (int?)item.VersionNumber)
                    .SingleOrDefault(),
                HasDraft = test.Versions.Any(
                    item => item.Status == CourseTestStatus.Draft),
                TotalPoints = GetTotalPoints(version),
                Tasks = tasks
                    .OrderBy(task => task.CreatedAt)
                    .ThenBy(task => task.Id)
                    .Select(ToDto)
                    .ToList(),
                Versions = test.Versions
                    .OrderByDescending(item => item.VersionNumber)
                    .Select(ToSummaryDto)
                    .ToList()
            };
        }

        public static CourseTestVersionSummaryDto ToSummaryDto(CourseTestVersion version)
        {
            return new CourseTestVersionSummaryDto(
                version.VersionNumber,
                version.Status,
                version.Title,
                version.CreatedAt,
                version.PublishedAt);
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

        private static decimal GetTotalPoints(CourseTestVersion version)
        {
            return version.Tasks
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
