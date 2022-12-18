using System;
using System.Windows.Forms;

namespace KeyRebinder
{
    public class KeyMapping : IEquatable<KeyMapping>
    {
        private const char _delimiter = '\u235D';

        public Keys SourceKey = Keys.None;
        public Keys DestinationKey = Keys.None;

        public bool Equals(KeyMapping other)
        {
            return other.SourceKey == SourceKey
                && other.DestinationKey == DestinationKey;
        }

        public string Serialize()
        {
            return $"{(int)SourceKey}{_delimiter}{(int)DestinationKey}";
        }

        public static KeyMapping DeSerialize(string keyMappingString)
        {
            string[] values = keyMappingString.Split(_delimiter);
            return new KeyMapping
            {
                SourceKey = (Keys)int.Parse(values[0]),
                DestinationKey = (Keys)int.Parse(values[1]),
            };
        }
    }
}
