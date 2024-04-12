using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalSaveReader.FArchive
{
    interface IPropertyPathPart
    {
        string Label { get; }

        bool Matches(string literal);
    }

    class RootPart : IPropertyPathPart
    {
        public string Label { get; } = "";
        public bool Matches(string literal) => literal.Length == 0;
    }

    class SimplePathPart : IPropertyPathPart
    {
        public string Label { get; set; }
        public bool Matches(string literal) => literal == Label;
    }

    class IndexPathPart : IPropertyPathPart
    {
        public int? Index { get; set; }
        public string Label => $"[{Index?.ToString() ?? "*"}]";

        public bool Matches(string literal)
        {
            if (!literal.StartsWith('[')) return false;

            if (Index == null) return true;
            else
            {
                var literalIdx = int.Parse(literal.Substring(1, literal.Length - 2));
                return Index == literalIdx;
            }
        }
    }

    class PropertyPath
    {
        IEnumerable<IPropertyPathPart> parts;

        public PropertyPath()
        {
            parts = Enumerable.Empty<IPropertyPathPart>();
            Serialized = "";
        }

        public string Serialized { get; private set; }

        private PropertyPath(string serialized, IEnumerable<IPropertyPathPart> parts)
        {
            this.parts = parts;
            this.Serialized = serialized;
        }

        public static PropertyPath Parse(string path)
        {
            var stringParts = path.Split('.');
            return new PropertyPath(
                path,
                stringParts.Select<string, IPropertyPathPart>(p =>
                {
                    if (p == "") return new RootPart();
                    if (p.StartsWith("["))
                    {
                        var idxStr = p.Substring(1, p.Length - 2);
                        if (idxStr == "*") return new IndexPathPart() { Index = null };
                        else return new IndexPathPart() { Index = int.Parse(idxStr) };
                    }
                    else
                    {
                        return new SimplePathPart() { Label = path };
                    }
                }).ToArray()
            );
        }

        public PropertyPath WithPart(IPropertyPathPart part)
        {
            return new PropertyPath(Serialized + "." + part.Label, parts.Append(part));
        }
    }
}
