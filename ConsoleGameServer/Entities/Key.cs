using ConsoleGame.Core;
using ConsoleGame.Components;

namespace ConsoleGame.Entities
{
    public class KeyItem : Item
    {
        public KeyItem(string keyId)
        {
            Set(new Key { KeyId = keyId });
        }
    }
}


