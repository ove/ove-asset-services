using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace OVE.Service.NetworkTiles.QuadTree.Utilities {
    public static class XmlExtensions {
        public static IEnumerable<XElement> StreamReadXElement(string inputUrl, string elementName) {
            using (var reader = XmlReader.Create(inputUrl)) {
                reader.MoveToContent();
                while (reader.Read()) {
                    if (reader.NodeType == XmlNodeType.Element 
                        && reader.Name == elementName 
                        && XNode.ReadFrom(reader) is XElement el) {
                        yield return el;
                    }
                }
            }
        }
    }
}