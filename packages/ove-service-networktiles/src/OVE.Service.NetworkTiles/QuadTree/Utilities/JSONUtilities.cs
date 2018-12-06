using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;

namespace OVE.Service.NetworkTiles.QuadTree.Utilities {
    /// <summary>
    /// Help to serialize the graph to match the right format
    /// </summary>
    public static class JsonUtilities {
        /// <summary>
        /// Serializes a graph as JSON
        /// </summary>
        /// <param name="nodesList">the actual nodes</param>
        /// <param name="edges">The arcs. id > id </param>
        /// <param name="fileName">Name of the file  to generate</param>
        public static void SaveGraph(List<GraphNode> nodesList, List<GraphLink> edges, string fileName) {
            Dictionary<string, IEnumerable<GraphObject>> graphObjects =
                new Dictionary<string, IEnumerable<GraphObject>> {
                    ["nodes"] = nodesList,
                    ["edges"] = edges
                };

            JsonSerializerSettings settings = new JsonSerializerSettings {
                ContractResolver = CustomDataContractResolver.Instance
            };
            string json = JsonConvert.SerializeObject(graphObjects, settings);
            File.WriteAllText(fileName, json);
        }

        private static string HexConverter(System.Drawing.Color c) {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }
        
        /// <summary>
        /// Custom serializer that serializes all attributes names in lowercase.
        /// </summary>
        private class CustomDataContractResolver : DefaultContractResolver {
            public static readonly CustomDataContractResolver Instance = new CustomDataContractResolver();

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization) {
                var property = base.CreateProperty(member, memberSerialization);
                property.PropertyName = property.PropertyName.ToLower();
                return property;
            }
        }
    }
}