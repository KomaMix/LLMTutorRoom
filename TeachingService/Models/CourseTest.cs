namespace TeachingService.Models
{
    public sealed class CourseTest
    {
        public Guid Id { get; set; }
        public string TeacherUserId { get; set; } = string.Empty;
        public int NextVersionNumber { get; set; } = 1;
        public List<CourseTestVersion> Versions { get; set; } = new();
    }
}
