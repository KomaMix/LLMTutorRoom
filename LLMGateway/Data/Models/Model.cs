namespace LLMGateway.Data.Models
{
    public class Model
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<ModelDeployment> Deployments { get; set; } = new List<ModelDeployment>();
    }
}
