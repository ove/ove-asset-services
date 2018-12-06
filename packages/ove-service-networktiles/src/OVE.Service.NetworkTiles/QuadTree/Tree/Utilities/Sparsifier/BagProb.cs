namespace OVE.Service.NetworkTiles.QuadTree.Tree.Utilities.Sparsifier {
    public class BagProb<T> where T : IQuadable<double> {
        public QuadTreeBag<T> Bag;
        public int Count;
        public double WithinBagProp;
        public double OverallProb;

        public override string ToString() {
            return $"{{ bag = {Bag}, count = {Count}, WithinBagProp = {WithinBagProp}, OverallProb = {OverallProb} }}";
        }
    }
}