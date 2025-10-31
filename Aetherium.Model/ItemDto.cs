namespace Aetherium.Model
{
    public class ItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = "Item";
        public string Icon { get; set; } = "?";
        public string? KeyId { get; set; }
        public WorldLocationDto? Location { get; set; }
    }
}



