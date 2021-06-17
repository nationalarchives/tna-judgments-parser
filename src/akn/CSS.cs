
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UK.Gov.Legislation.Judgments.AkomaNtoso {

class CSS {

    public static string Serialize(Dictionary<string, Dictionary<string, string>> selectors) {
        StringBuilder builder = new StringBuilder();
        foreach (KeyValuePair<string, Dictionary<string, string>> selector in selectors) {
            builder.Append("." + selector.Key + " {");
            foreach (KeyValuePair<string, string> prop in selector.Value)
                builder.Append($" { prop.Key }: { prop.Value };");
            builder.AppendLine(" }");
        }
        return builder.ToString();
    }

    public static string SerializeInline(Dictionary<string, string> properties) {
        return string.Join(";", properties.Select(pair => pair.Key + ":" + pair.Value));
    }

}

}
