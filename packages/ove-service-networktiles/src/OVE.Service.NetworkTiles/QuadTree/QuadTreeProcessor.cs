using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using OVE.Service.NetworkTiles.QuadTree.Domain;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.QuadTree {
    public static class QuadTreeProcessor {
        /// <summary>
        /// This method kicks off the processing of a graphml file 
        /// </summary>
        /// <param name="filename">valid graphml file</param>
        /// <param name="logger">a logger to use</param>
        /// <returns>the root quad tree</returns>
        public static QuadTreeNode<GraphObject> ProcessFile(string filename, ILogger logger) {

            var rootPath = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename));
            if (!Directory.Exists(rootPath)) {
                Directory.CreateDirectory(rootPath);
            }

            logger.LogInformation($"about to process {filename}");

            // todo make this an option? 
//          logger.LogInformation("clearing unicode");
//          ReWriteFileWithoutUnicode(app.GetGraphMLPath() + "\\" + filename);

            var watch = Stopwatch.StartNew();

            //todo this is not using the streaming xml reader - why? possibly due to a need to know exact graph bounds?
            GraphmlReader reader = new GraphmlReader(logger);
            GraphInfo graph = reader.ReadGraphmlData(filename);

            logger.LogInformation("Graph read " + reader.NodesById.Count + "nodes " + reader.Edges.Count + "edges");

            var nodeSource = BatchLinq.BatchObjects<GraphNode, GraphObject>(reader.NodesById.Values);
            var edgeSource = BatchLinq.BatchObjects<GraphLink, GraphObject>(reader.Edges);

            // actually make the quadtree
            var processedGraph = SigmaGraphQuadProcessor.ProcessGraph(graph, rootPath, 5000, nodeSource, edgeSource,logger);

            watch.Stop();

            logger.LogInformation("finished in " + watch.ElapsedMilliseconds + "ms");

            return processedGraph.Root;
        }

        // todo these methods are not used and can be deleted? 
        public static IEnumerable<string> GetFilesWithin(QuadTreeNode<GraphObject> root, double x, double y,
            double xWidth, double yWidth) {
            return root.ReturnMatchingLeaves(x, y, xWidth, yWidth)
                .Select(graphNode => "/" + graphNode.Guid + ".json").ToList(); // todo fix file names returned
        }

        public static IEnumerable<string> GetLeafBoxes(QuadTreeNode<GraphObject> root, double x, double y, double xWidth, double yWidth) {
            return root.ReturnMatchingLeaves(x, y, xWidth, yWidth)
                .Select(graphNode => graphNode.Centroid.ToString()).ToList();
        }

    }
}