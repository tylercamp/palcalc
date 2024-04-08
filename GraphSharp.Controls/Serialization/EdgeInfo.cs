using System.Xml.Serialization;

namespace GraphSharp.Serialization
{
    public abstract class EdgeInfo
    {

    }

    public abstract class EdgeInfo<TEdge> : EdgeInfo
    {
        [XmlAttribute]
        public int ID { get; set; }
        [XmlAttribute]
        public int SourceID { get; set; }
        [XmlAttribute]
        public int TargetID { get; set; }
    }
}