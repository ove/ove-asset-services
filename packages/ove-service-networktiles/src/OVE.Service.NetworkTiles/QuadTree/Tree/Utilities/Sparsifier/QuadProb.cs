using System.Collections.Generic;

namespace OVE.Service.NetworkTiles.QuadTree.Tree.Utilities.Sparsifier {
    public class QuadProb<T> where T : IQuadable<double> {
        public QuadTreeNode<T> Quad { get; set; }
        public int Count { get; set; }
        public double Prob { get; set; }
        public List<BagProb<T>> Bags { get; set; }

        public override string ToString() {
            return $"{{ Quad = {Quad}, Count = {Count}, CumulativeProb = {Prob}, Bags = {Bags} }}";
        }

        public override bool Equals(object value) {
            return value is QuadProb<T> type && EqualityComparer<QuadTreeNode<T>>.Default.Equals(type.Quad, Quad) &&
                   EqualityComparer<int>.Default.Equals(type.Count, Count) &&
                   EqualityComparer<double>.Default.Equals(type.Prob, Prob) &&
                   EqualityComparer<IEnumerable<BagProb<T>>>.Default.Equals(type.Bags, Bags);
        }

        public override int GetHashCode() {
            int num = 0x7a2f0b42;
            num = (-1521134295 * num) + EqualityComparer<QuadTreeNode<T>>.Default.GetHashCode(Quad);
            num = (-1521134295 * num) + EqualityComparer<int>.Default.GetHashCode(Count);
            num = (-1521134295 * num) + EqualityComparer<double>.Default.GetHashCode(Prob);
            return (-1521134295 * num) + EqualityComparer<IEnumerable<BagProb<T>>>.Default.GetHashCode(Bags);
        }
    }
}