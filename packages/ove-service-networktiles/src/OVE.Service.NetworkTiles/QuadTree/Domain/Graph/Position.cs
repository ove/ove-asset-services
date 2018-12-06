namespace OVE.Service.NetworkTiles.QuadTree.Domain.Graph {
    public class Position {
        public float X { get; set; }
        public float Y { get; set; }

        public override string ToString() {
            return X + ":" + Y;
        }
    }
}