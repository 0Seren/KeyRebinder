using System.Collections.Generic;
using System.Text;

namespace KeyRebinder
{
    public class RebinderInfo
    {
        private const char _delimiter = '\u237C';

        public string ApplicationName;
        public List<KeyMapping> KeyMappings = new();

        public string Serialize()
        {
            StringBuilder sb = new();
            _ = sb.Append($"{ApplicationName}{_delimiter}");
            foreach (KeyMapping mapping in KeyMappings)
            {
                _ = sb.Append($"{mapping.Serialize()}{_delimiter}");
            }
            sb.Length--;
            return sb.ToString();
        }

        public static RebinderInfo DeSerialize(string rebinderInfoString)
        {
            RebinderInfo rebinderInfo = new();
            string[] splits = rebinderInfoString.Split(_delimiter);
            rebinderInfo.ApplicationName = splits[0];
            for (int i = 1; i < splits.Length; i++)
            {
                rebinderInfo.KeyMappings.Add(KeyMapping.DeSerialize(splits[i]));
            }
            return rebinderInfo;
        }
    }
}
