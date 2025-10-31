using Aetherium.Core;

namespace Aetherium.Components
{
    public class DataStorage : Component
    {
        public string DataContent { get; set; } = string.Empty;
        public bool IsEncrypted { get; set; } = false;

        public DataStorage() : base() { }
    }
}

