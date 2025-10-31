using Aetherium.Core;
using Aetherium.Components;

namespace Aetherium.Entities
{
    public class KeyItem : Item
    {
        public KeyItem(string keyId)
        {
            Set(new Key { KeyId = keyId });
        }
    }
}



