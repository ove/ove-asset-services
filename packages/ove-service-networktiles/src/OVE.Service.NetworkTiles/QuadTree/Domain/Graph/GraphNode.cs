using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.QuadTree.Domain.Graph {
    public class GraphNode : GraphObject, IEquatable<GraphNode> {
        #region equality

        public bool Equals(GraphNode other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((GraphNode) obj);
        }

        public override int GetHashCode() {
            return (Id != null ? Id.GetHashCode() : 0);
        }

        public static bool operator ==(GraphNode left, GraphNode right) {
            return Equals(left, right);
        }

        public static bool operator !=(GraphNode left, GraphNode right) {
            return !Equals(left, right);
        }

        #endregion

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public Position Pos { get; set; }

        [JsonProperty("x")]
        public float X => Pos.X;

        [JsonProperty("y")]
        public float Y => Pos.Y;

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonIgnore]
        public float SizeF { get; set; }

        [JsonProperty("size")]
        public int Size => (int) Math.Max(1, Math.Min(12, SizeF));

        [JsonIgnore]
        public int R { get; set; }

        [JsonIgnore]
        public int G { get; set; }

        [JsonIgnore]
        public int B { get; set; }

        [JsonProperty("color")]
        public string Color => "#" + R.ToString("X2") + G.ToString("X2") + B.ToString("X2");

        public Dictionary<string, object> Attrs { get; set; }

        [JsonIgnore]
        public int NumLinks { get; set; }

        [JsonIgnore]
        public List<string> Adj { get; set; }

        public override bool IsWithin<T>(QuadTreeNode<T> quadTreeNode) {
            double xCentroid = quadTreeNode.Centroid.XCentroid;
            double yCentroid = quadTreeNode.Centroid.YCentroid;
            double xWidth = quadTreeNode.Centroid.XWidth / 2.0;
            double yWidth = quadTreeNode.Centroid.YWidth / 2.0;

            double minX = xCentroid - xWidth;
            double maxX = xCentroid + xWidth;
            double minY = yCentroid - yWidth;
            double maxY = yCentroid + yWidth;
            return Pos.X >= minX && Pos.X <= maxX
                                 && Pos.Y >= minY && Pos.Y <= maxY;
        }
    }

}