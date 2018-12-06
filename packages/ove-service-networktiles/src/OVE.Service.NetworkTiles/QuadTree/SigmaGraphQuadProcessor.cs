using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.NetworkTiles.QuadTree.Domain;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;
using OVE.Service.NetworkTiles.QuadTree.Tree.Utilities;
using OVE.Service.NetworkTiles.QuadTree.Tree.Utilities.Sparsifier;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.QuadTree {
    public static class SigmaGraphQuadProcessor {
        /// <summary>
        /// Indexes graph into a quadtree data structure and saves the quadtree to disk to JSON.
        /// </summary>
        /// <param name="graph">the graph to index</param>
        /// <param name="outputFolder">the path to save the quadtree to</param>
        /// <param name="maxObjectsPerBag">The maximum number of objects per bag.</param>
        /// <param name="nodes">The nodes.</param>
        /// <param name="edges">The edges.</param>
        /// <param name="logger"></param>
        /// <returns>
        /// the quadtree representing the graph
        /// </returns>
        public static QuadTree<GraphObject> ProcessGraph(GraphInfo graph, string outputFolder, int maxObjectsPerBag,
            IEnumerable<List<GraphObject>> nodes, IEnumerable<List<GraphObject>> edges, ILogger logger) {
            // Create the quadtree factory
            // TODO this hard limit on max objectPerBag is questionable and may cause issues if there exists a node with valency > limit 
            //      (or nodes very close spatially with total valency > limit) 

            var factory =
                new ConcurrentQuadTreeFactory<GraphObject>(new QuadCentroid(graph.RectDim.Width, graph.RectDim.Height),
                    logger, 1, maxObjectsPerBag); // todo remove 1

            // Add the graph objects to the quadtree factory
            var addData = new List<IEnumerable<List<GraphObject>>> {
                nodes,
                edges
            };

            logger.LogInformation("About to process a graph");

            // TODO not sure if these numbers of threads is optimal - will depend upon server make configurable!
            factory.ConcurrentAdd(addData, workThreads: 3, reworkThreads: 2);
            factory.QuadTree.ShedAllObjects();

            // Write quadtree metadata to disk for debugging
            var results = factory.PrintState();
            File.WriteAllText(Path.Combine(outputFolder, "results.txt"), results);
            var tree = factory.QuadTree.PrintQuad(factory); // print so that it can be used in the sparsifier 
            File.WriteAllText(Path.Combine(outputFolder, "tree.xml"), tree);
            var bagList = factory.PrintShedBags();
            File.WriteAllText(Path.Combine(outputFolder, "bagList.csv"), bagList);

            // Get leaves and serialize their contents
            Dictionary<string, QuadTreeNode<GraphObject>> leafs = factory.SelectLeafs();
            ExportLeafNodes(factory, leafs, outputFolder);

            // now sparsify the quad tree 
            Sparsifier.Sparsify(factory.QuadTree.Root, factory, o => {
                if (!(o is GraphNode on)) return false;

                try {
                    return on.Attrs.ContainsKey("importance") && Convert.ToInt32(on.Attrs["importance"]) == 1;
                }
                catch (Exception e) {
                    Console.WriteLine(e);
                    return false;
                }
            });
            HeightAnnotator.Annotate(factory.QuadTree.Root);
            // export them 
            ExportSparseNodes(factory.QuadTree.Root, outputFolder);

            // Serialize the quadtree
            ExportQuadTree(factory.QuadTree.Root, Path.Combine(outputFolder, "quad.json"));

            return factory.QuadTree;
        }

        /// <summary>
        /// Serialize a quadtree as JSON and write it to disk.
        /// </summary>
        /// <param name="factoryQuadTree">the root node of the quadtree to serialize</param>
        /// <param name="filename">the path and name of the file to save the quadtree as</param>
        private static void ExportQuadTree(QuadTreeNode<GraphObject> factoryQuadTree, string filename) {
            string json = JsonConvert.SerializeObject(factoryQuadTree);
            File.WriteAllText(filename, json);
        }

        /// <summary>
        /// Serialize the leaves of a quadtree as JSON and write them to disk.
        /// </summary>
        /// <param name="factory">the quadtree factory</param>
        /// <param name="leafs">the leaves to serialize</param>
        /// <param name="outputFolder">the path to save the leaf JSONs to</param>
        private static void ExportLeafNodes(ConcurrentQuadTreeFactory<GraphObject> factory,
            Dictionary<string, QuadTreeNode<GraphObject>> leafs, string outputFolder) {
            Parallel.ForEach(leafs, l => ProcessLeafJson(l.Key, factory.GetBagsForQuad(l.Value), outputFolder));
        }

        private static void ExportSparseNodes(QuadTreeNode<GraphObject> quad, string outputFolder) {
            if (quad.IsLeaf()) return;
            ProcessLeafJson(quad.Guid, new[] {quad.SparseBag},
                outputFolder); // export the sparse bags with the id of the quad tree node and not the bag id.

            Parallel.ForEach(quad.SubQuads, child => ExportSparseNodes(child, outputFolder));
        }

        /// <summary>
        /// Serialize a single leaf of a quadtree to JSON.
        /// </summary>
        /// <param name="key">the name of the JSON file</param>
        /// <param name="quadTreeBag">the bag containing the graph objects associated with the leaf</param>
        /// <param name="outputFolder">the path to save the leaf JSONs to</param>
        private static void ProcessLeafJson(string key, IEnumerable<QuadTreeBag<GraphObject>> quadTreeBag, string outputFolder) {
            // Split objects into nodes & edges
            var nodes = new List<GraphNode>();
            var edges = new List<GraphLink>();
            foreach (var graphObject in quadTreeBag.SelectMany(b => b.Objects)) {
                switch (graphObject) {
                    case GraphLink _:
                        edges.Add(graphObject as GraphLink);
                        break;
                    case GraphNode _: {
                        nodes.Add(graphObject as GraphNode);
                        break;
                    }
                    default:
                        throw new ArgumentException("unknown graph object type");
                }
            }

            // Include the nodes of the edges in this bag
            //List<string> nodes = edges.Select(e => e.Source).Union(edges.Select(e => e.Target))       here removed the effects of edge nodes to avoid the use of dictionary
            //   .Union(leafNodes).ToList();
            //Debug.WriteLine("Node Count: " + leafNodes.Count 
            //              + " New Node Count: " + nodes.Count 
            //              + " Edge Count: " + edges.Count);

            // Convert the graph objects to JSON and write to disk
            string outputFile = Path.Combine(outputFolder, key + ".json");
            JsonUtilities.SaveGraph(nodes, edges, outputFile);
        }
    }
}