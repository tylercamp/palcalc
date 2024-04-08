using System.Xml.Serialization;

namespace GraphSharp.Serialization
{
    public abstract  class VertexInfo
    { }

    public abstract class VertexInfo<TVertex> : VertexInfo
    {
        [XmlAttribute]
        public int ID { get; set; }

        [XmlAttribute]
        public double X { get; set; }
        
        [XmlAttribute]
        public double Y { get; set; }
        
        [XmlAttribute]
        public double Width { get; set; }
        
        [XmlAttribute]
        public double Height { get; set; }

        public TVertex Vertex { get; set; }
    }
}