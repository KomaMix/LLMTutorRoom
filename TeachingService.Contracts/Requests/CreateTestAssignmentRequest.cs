namespace TeachingService.Contracts.Requests;

public class CreateTestAssignmentRequest
{
    public Guid TestId { get; set; }
    public int TestRevision { get; set; }
    public DateTimeOffset AvailableFrom { get; set; }
    public DateTimeOffset Deadline { get; set; }
}
