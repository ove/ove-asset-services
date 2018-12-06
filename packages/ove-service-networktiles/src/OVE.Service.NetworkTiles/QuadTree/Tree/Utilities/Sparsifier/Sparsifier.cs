using System;
using System.Collections.Generic;
using System.Linq;

namespace OVE.Service.NetworkTiles.QuadTree.Tree.Utilities.Sparsifier {
    /// <summary>
    /// todo you must run the printer first? 
    /// </summary>
    public static class Sparsifier {

        public static void Sparsify<T>(QuadTreeNode<T> quad, AConcurrentQuadTreeFactory<T> factory,
            Func<T, bool> preserver) where T : IQuadable<double> {
            //"itemsInThisTree", itemsInThisTree
            //Items = items in leaf nodes

            // recurse to leaf nodes as we need to build up the 
            if (!quad.IsLeaf()) {
                foreach (var child in quad.SubQuads) {
                    Sparsify(child, factory, preserver);
                }
            }

            if (quad.IsLeaf()) {
                return; // its already sparse
            }

            // get #items in tree per each quad 
            // load each quad tree as post processed 
            int totalObjects = quad.Counters.GetOrAdd("itemsInThisTree", 0);

            // todo this is getting raw quads and not the sparsified version 
            var allBags = quad.SubQuads.Select(q => {
                var itemsInTree = q.Counters.GetOrAdd("itemsInThisTree", 0);

                var bags = q.IsLeaf()
                    ? factory.GetBagsForQuad(q)
                    : new[] {q.SparseBag};

                var totalInBags = bags.Sum(b => b.Objects.Count);
                return new QuadProb<T> {
                    Quad = q,
                    Count = itemsInTree,
                    Prob = itemsInTree / (double) totalObjects,
                    Bags = bags.Select(b => new BagProb<T> {
                        Bag = b,
                        Count = b.Objects.Count,
                        WithinBagProp = b.Objects.Count / (double) totalInBags,
                    }).ToList()
                };
            }).ToArray();

            // set up the cumulative probabilities 
            double cumulative = 0.0;
            foreach (var subQuad in allBags) {
                foreach (var bag in subQuad.Bags) {
                    bag.OverallProb = bag.WithinBagProp * subQuad.Prob;
                    cumulative += bag.OverallProb;
                }
            }

            // sample in proportion to weighting

            QuadTreeBag<T> sparseBag = new QuadTreeBag<T>(quad.Guid, new List<T>(), quad.TreeId);
            Random r = new Random();
            
            //preserve important objects where "importance" is set to 1 
            sparseBag.Objects.AddRange(allBags.SelectMany(b => b.Bags).SelectMany(b => b.Bag.Objects.Where(preserver)));

            var sparseBiggerThanLeafs = 5;
            for (int s = sparseBag.Objects.Count; s < factory.MaxObjectsPerBag * sparseBiggerThanLeafs; s++) {

                double diceRoll = r.NextDouble();

                cumulative = 0.0;
                bool found = false;
                for (var q = 0; q < allBags.Length && !found; q++) {
                    for (var b = 0; b < allBags[q].Bags.Count && !found; b++) {
                        var bag = allBags[q].Bags[b];

                        cumulative +=
                            bag.OverallProb; // could also not keep cumulative and rely upon  bag.OverallCumulative
                        if (diceRoll < cumulative) {
                            int i = r.Next(0, bag.Count); // yes this is double sampling 
                            var elem = bag.Bag.Objects[i];
                            sparseBag.Objects.Add(elem);
                            found = true;
                        }
                    }
                }

                if (!found || cumulative - 1 > 0.001) {
                    throw new Exception("Bad statistics! " + diceRoll + " / " + cumulative);
                }
            }

            // save a reference to it someplace... 
            quad.SparseBag = sparseBag;
            quad.SparseBagId = sparseBag.BagId;

        }

    }
}