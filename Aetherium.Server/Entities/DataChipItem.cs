using Aetherium.Components;
using Aetherium.Core;

namespace Aetherium.Entities
{
    public class DataChipItem : Item
    {
        public DataChipItem(string content = "", bool isEncrypted = false) : base()
        {
            var carriable = Get<Carriable>();
            carriable.Label = "Data Chip";
            carriable.Icon = "D";

            // Stores data/information
            Set(new DataStorage
            {
                DataContent = content,
                IsEncrypted = isEncrypted
            });
        }
    }
}


