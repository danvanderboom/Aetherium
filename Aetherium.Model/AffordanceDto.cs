namespace Aetherium.Model
{
    public class AffordanceDto
    {
        public string Action { get; set; } = string.Empty; // pickup|drop|use|open|close
        public string ActorId { get; set; } = string.Empty;
        public string? TargetId { get; set; }
        public string? RequiresKeyId { get; set; }
    }
}



