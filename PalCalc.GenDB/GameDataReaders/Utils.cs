using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.UObject;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB.GameDataReaders
{
    class FStructPropertyAttribute(string? propName = null) : Attribute
    {
        public string? PropName => propName;
    }

    static class FStructExtensions
    {
        private static ILogger logger = Log.ForContext(typeof(FStructExtensions));

        public static T ToObject<T>(this FStructFallback dt) where T : class
        {
            var result = Activator.CreateInstance<T>();

            List<string> missingProps = [];

            foreach (var prop in typeof(T).GetProperties())
            {
                var mappingAttr = prop.GetCustomAttribute<FStructPropertyAttribute>();
                if (mappingAttr == null) continue;

                string dictKey = mappingAttr.PropName ?? prop.Name;
                var structProp = dt.Properties.SingleOrDefault(p => p.Name == dictKey);

                if (structProp == null)
                {
                    missingProps.Add(dictKey);
                    continue;
                }

                switch (structProp.TagData.Type)
                {
                    case "EnumProperty":
                    case "NameProperty":
                        prop.SetValue(result, structProp.Tag.GetValue<FName>().Text);
                        break;

                    default:
                        prop.SetValue(result, structProp.Tag.GetValue(prop.PropertyType));
                        break;
                }
            }

            if (missingProps.Count > 0)
            {
                logger.Warning("Entry missing props: {Missing}", string.Join(", ", missingProps));
            }

            return result;
        }
    }

    static class EnumerableExtensions
    {
        public static V GetOneOf<K, V>(this IDictionary<K, V> dict, params K[] keys)
        {
            foreach (var k in keys.Where(dict.ContainsKey))
                return dict[k];

            throw new KeyNotFoundException();
        }

        public static Dictionary<string, V> ToCaseInsensitive<V>(this IDictionary<string, V> dict) =>
            new Dictionary<string, V>(dict, StringComparer.InvariantCultureIgnoreCase);
    }
}
