using PalCalc.SaveReader.FArchive.Custom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader.FArchive
{
    /// <summary>
    /// A convenience wrapper for IProperty values which allows you to access data without needing to cast all the time.
    /// E.g. `myGvas.Dynamic.worldSaveData.MapObjectSaveData`.
    /// 
    /// Iterable values like `ArrayProperty` or `MapProperty` will still need an explicit cast to `IEnumerable{dynamic}` before
    /// you can iterate.
    /// 
    /// Only meant to be used for exploring data, not as part of a normal parser.
    /// </summary>
    public class DynamicProperty(IProperty value) : DynamicObject
    {
        public static object WrapValue(object value) =>
            value switch
            {
                IProperty p => new DynamicProperty(p),
                IDictionary<string, object> d => d.Aggregate(new ExpandoObject() as IDictionary<string, object>, (a, p) => { a.Add(new KeyValuePair<string, object>(p.Key, WrapValue(p.Value))); return a; }),
                _ => value
            };

        private Lazy<IDictionary<string, object>> properties = new Lazy<IDictionary<string, object>>(() =>
        {
            switch (value)
            {
                case LiteralProperty l:
                    return new Dictionary<string, object>()
                    {
                        { "Value", l.Value }
                    };

                case ByteProperty b:
                    return new Dictionary<string, object>()
                    {
                        { "Value", b.Num },
                    };

                case EnumProperty e:
                    return new Dictionary<string, object>()
                    {
                        { "Value", e.EnumValue }
                    };

                case StructProperty s:
                    if (s.Value is Dictionary<string, object>)
                    {
                        var d = s.Value as Dictionary<string, object>;
                        return d.ToDictionary(kvp => kvp.Key, kvp => WrapValue(kvp.Value));
                    }
                    else
                    {
                        return new Dictionary<string, object>()
                        {
                            { "Value", s.Value }
                        };
                    }

                case ArrayProperty a:
                    return new Dictionary<string, object>()
                    {
                        { "Values", a.Value }
                    };

                case MapProperty m:
                    return new Dictionary<string, object>()
                    {
                        { "Items", m.Value.Cast<dynamic>() }
                    };

                case CharacterContainerDataProperty p:
                    return (dynamic)p;

                case CharacterDataProperty p:
                    return (dynamic)p;

                case GroupDataProperty p:
                    return (dynamic)p;

                case WorkerDirectorDataProperty p:
                    return (dynamic)p;

                case MapModelDataProperty p:
                    return (dynamic)p;

                case BaseCampDataProperty p:
                    return (dynamic)p;

                default:
                    throw new NotImplementedException();
            }
        });

        public override IEnumerable<string> GetDynamicMemberNames() => properties.Value.Keys;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var res = properties.Value.TryGetValue(binder.Name, out result);

            return res;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            return base.TryInvokeMember(binder, args, out result);
        }

        public IEnumerable<dynamic> ToEnumerable() =>
            value switch
            {
                ArrayProperty a => a.Values<object>().Select(WrapValue),
                MapProperty m => m.Value.Select(kvp => new KeyValuePair<dynamic, dynamic>(WrapValue(kvp.Key), WrapValue(kvp.Value))).Cast<dynamic>(),
                _ => Enumerable.Empty<dynamic>()
            };

        public IPropertyMeta Meta => value.Meta;

        public int Count => value switch
        {
            ArrayProperty a => a.Values<object>().Length,
            MapProperty m => m.Value.Count,
            _ => -1
        };

        public dynamic this[int i] => value switch
        {
            ArrayProperty a => WrapValue(a.Values<object>()[i]),
            MapProperty m => m.Value.Skip(i).Select(kvp => new KeyValuePair<dynamic, dynamic>(WrapValue(kvp.Key), WrapValue(kvp.Value))).FirstOrDefault(),
            _ => null
        };

    }
}
