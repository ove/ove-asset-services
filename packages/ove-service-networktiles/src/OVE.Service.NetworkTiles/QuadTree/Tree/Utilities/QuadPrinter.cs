using System.Linq;
using System.Xml.Linq;

namespace OVE.Service.NetworkTiles.QuadTree.Tree.Utilities {
    /// <summary>
    /// an attempt to use XMLto print off a quad tree and get a feel for its structure, 
    /// we do a depth first traversal     
    /// </summary>
    public static class QuadPrinter {
        public static string PrintQuad<T>(this QuadTree<T> quad, AConcurrentQuadTreeFactory<T> factory)
            where T : IQuadable<double> {
            XElement root = new XElement("root");
            int objectCount = PrintQuad(root, quad.Root, factory);
            root.Add(new XAttribute("count", objectCount));
            return root.ToString();
        }

        private static int PrintQuad<T>(XElement root, QuadTreeNode<T> quad, AConcurrentQuadTreeFactory<T> factory)
            where T : IQuadable<double> {
            XElement node = new XElement("node");
            node.Add(new XAttribute("guid", quad.Guid));

            int itemsInThisTree = quad.ObjectsInside.Count;
            int itemsShed = quad.Counters.GetOrAdd("ObjectsShed", 0);
            int localItems = quad.ObjectsInside.Count;


            if (quad.IsLeaf()) {
                node.Add(new XAttribute("isLeaf", true));
                itemsInThisTree += itemsShed;
            }
            else {
                foreach (var child in quad.SubQuads) {
                    itemsInThisTree += PrintQuad(node, child, factory);
                }
            }

            // save the counters
            quad.Counters.AddOrUpdate("itemsInThisTree", itemsInThisTree, (s, i) => itemsInThisTree);
            quad.ItemsInThisTree = itemsInThisTree;
            node.Add(new XAttribute("itemsInThisTree", itemsInThisTree));
            node.Add(new XAttribute("localItems", localItems));
            node.Add(new XAttribute("itemsShed", itemsShed));
            node.Add(new XAttribute("c", quad.Centroid.ToString()));
            node.Add(new XAttribute("Items",
                factory.GetBagsForQuad(quad).Aggregate(0, (acc, next) => acc + next.Objects.Count)));
            root.Add(node);

            return itemsInThisTree;
        }

    }
}