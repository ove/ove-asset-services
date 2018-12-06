using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    /// <summary>
    /// The basic Node of the Quad Tree 
    /// </summary>
    /// <typeparam name="T">type held within the quadtree</typeparam>
    public class QuadTreeNode<T> where T : IQuadable<double> {
        private readonly ILogger _logger;
        public string Guid { get; set; } = System.Guid.NewGuid().ToString();
        public string CreationTime { get; }
        public QuadCentroid Centroid { get; }
        public QuadTreeNode<T>[] SubQuads { get; set; }

        [NonSerialized] 
        public readonly ConcurrentBag<T> ObjectsInside = new ConcurrentBag<T>();

        [NonSerialized]
        public readonly ConcurrentDictionary<string, int> Counters = new ConcurrentDictionary<string, int>();

        public int ItemsInThisTree { get; set; }

        public string TreeId { get; }
        [NonSerialized] public QuadTreeBag<T> SparseBag;
        public string SparseBagId { get; set; }
        public int Depth { get; set; }

        public QuadTreeNode(ILogger logger, QuadCentroid centroid) {
            _logger = logger;
            this.Centroid = centroid;
            CreationTime = DateTime.Now.ToString("s/m/H/dd/M/yyyy");
        }

        // Critical
        public QuadTreeNode(ILogger logger, QuadCentroid centroid, string treeId) {
            _logger = logger;
            this.Centroid = centroid;
            this.TreeId = treeId;
        }

        public QuadTreeNode(ILogger logger, double xCentroid, double yCentroid, double xWidth, double yWidth, string treeId) : this(logger,
            new QuadCentroid(xCentroid, yCentroid, xWidth, yWidth)) {
            this.TreeId = treeId;
        }

        public List<QuadTreeNode<T>> ReturnMatchingQuadrants(T o) {
            return this.SubQuads.Where(o.IsWithin).ToList();
        }

        public QuadTreeNode<T>[] ReturnMatchingQuadrants(double xCentroid, double yCentroid, double xWidth,
            double yWidth) {
            //TODO this could be made a co-recursive function 
            return this.SubQuads
                .Where(s => QuadMatchesRectangle(s, ComputeCorners(xCentroid, yCentroid, xWidth, yWidth))).ToArray();
        }

        private static List<double> ComputeCorners(double xCentroid, double yCentroid, double xWidth, double yWidth) {
            List<double> cornersCamera = new List<double> {
                xCentroid - xWidth / 2, // xMin
                xCentroid + xWidth / 2, // xMax
                yCentroid - yWidth / 2, // yMin
                yCentroid + yWidth / 2  // yMax
            };

            return cornersCamera;
        }

        /// <summary>
        /// TODO this code does need to be unit tested to ensure that the math is correct 
        /// TODO REGRESSION TEST: Quad crosses rectangle x border, and is completely between y borders
        /// </summary>
        /// <param name="quad">The quad.</param>
        /// <param name="cornersCamera">The corners camera.</param>
        /// <returns>true upon match</returns>
        private bool QuadMatchesRectangle(QuadTreeNode<T> quad, List<double> cornersCamera) {
            var pointInX = quad.Centroid.XCentroid >= cornersCamera[0] &&
                           quad.Centroid.XCentroid <= cornersCamera[1];
            var pointInY = quad.Centroid.YCentroid >= cornersCamera[2] &&
                           quad.Centroid.YCentroid <= cornersCamera[3];

            if (pointInX && pointInY) {
                return true;
            }

            var borderInBetweenX =
                    // Touching left border
                    quad.Centroid.XCentroid - quad.Centroid.XWidth / 2 <= cornersCamera[0]
                    && quad.Centroid.XCentroid + quad.Centroid.XWidth / 2 >= cornersCamera[0]
                    // Touching right border
                    || quad.Centroid.XCentroid - quad.Centroid.XWidth / 2 <= cornersCamera[1]
                    && quad.Centroid.XCentroid + quad.Centroid.XWidth / 2 >= cornersCamera[1]
                    // Between left and right border
                    || quad.Centroid.XCentroid - quad.Centroid.XWidth / 2 >= cornersCamera[0]
                    && quad.Centroid.XCentroid + quad.Centroid.XWidth / 2 <= cornersCamera[1]
                ;
            var borderInBetweenY =
                    // Touching upper border
                    quad.Centroid.YCentroid - quad.Centroid.YWidth / 2 <= cornersCamera[2]
                    && quad.Centroid.YCentroid + quad.Centroid.YWidth / 2 >= cornersCamera[2]
                    // Touching lower border
                    || quad.Centroid.YCentroid - quad.Centroid.YWidth / 2 <= cornersCamera[3]
                    && quad.Centroid.YCentroid + quad.Centroid.YWidth / 2 >= cornersCamera[3]
                    // Between upper and lower border
                    || quad.Centroid.YCentroid - quad.Centroid.YWidth / 2 >= cornersCamera[2]
                    && quad.Centroid.YCentroid + quad.Centroid.YWidth / 2 <= cornersCamera[3]
                ;
            if (borderInBetweenX && borderInBetweenY) {
                return true;
            }

            return false;
        }

        // todo check function to query through two different dimensions
        public void ReturnLeafs(double x, double y, double xWidth, double yWidth, List<QuadTreeNode<T>> listResult) {
            if (this.IsLeaf()) {
                // check if there is a matching element in the database of bags nodes containing objects? 
                listResult.Add(this);
                //yield return this;
            }
            // Else, return the quadrants that match
            else {
                QuadTreeNode<T>[] subQuads = ReturnMatchingQuadrants(x, y, xWidth, yWidth);
                foreach (var subQuad in subQuads) {
                    //foreach (var returnLeaf in subQuad.ReturnLeafs(raVal, decVal, fovVal))
                    //    { yield return returnLeaf;}
                    subQuad.ReturnLeafs(x, y, xWidth, yWidth, listResult);
                }
            }
        }

        public void ReturnLeafs(double raVal, double decVal, double fovVal, List<QuadTreeNode<T>> listResult) {
            if (this.IsLeaf()) {
                // check if there is a matching element in the database of bags nodes containing objects? 
                listResult.Add(this);
                //yield return this;
            }
            // Else, return the quadrants that match
            else {
                QuadTreeNode<T>[] subQuads = ReturnMatchingQuadrants(raVal, decVal, fovVal, fovVal);
                foreach (var subQuad in subQuads) {
                    //foreach (var returnLeaf in subQuad.ReturnLeafs(raVal, decVal, fovVal))
                    //    { yield return returnLeaf;}
                    subQuad.ReturnLeafs(raVal, decVal, fovVal, fovVal, listResult);
                }
            }
        }

        public void ReturnAllLeafs(List<QuadTreeNode<T>> listResult) {
            if (this.IsLeaf()) {
                // check if there is a matching element in the database of bags nodes containing objects? 
                listResult.Add(this);
                //yield return this;
            }
            // Else, return the quadrants that match
            else {
                foreach (var subQuad in this.SubQuads) {
                    subQuad.ReturnAllLeafs(listResult);
                }
            }
        }

        public void ReturnLeafsNames(double raVal, double decVal, double fovValX, double fovValY,
            ref string listResult) {
            if (this.IsLeaf()) {
                // check if there is a matching element in the database of bags nodes containing objects? 
                listResult += " " + (this.Guid);
                //yield return this;
            }
            // Else, return the quadrants that match
            else {
                QuadTreeNode<T>[] subQuads = ReturnMatchingQuadrants(raVal, decVal, fovValX, fovValY);
                foreach (var subQuad in subQuads) {
                    //foreach (var returnLeaf in subQuad.ReturnLeafs(raVal, decVal, fovVal))
                    //    { yield return returnLeaf;}
                    subQuad.ReturnLeafsNames(raVal, decVal, fovValX, fovValY, ref listResult);
                }
            }
        }

        // Leaf determined by either containing objects (leaf) or none (node)
        public bool IsLeaf() {
            return this.SubQuads == null;
        }

        public bool TryGenerateChildren(Action<string, QuadTreeNode<T>> registerQuadTree) {
            
            if (this.IsLeaf()) {
                // i.e. no other thread has already done this 
                // Locks
                lock (this.ObjectsInside) {
                    // avoid two threads running this method
                    if (this.IsLeaf()) {
                        // in case another thread beat us as we waited for the lock

                        _logger.LogInformation("---- Generating SubQuads ----");
                        var newQuads = new QuadTreeNode<T>[4];
                        // Create sub quadtree
                        var cptSubTree = 0;
                        for (var i = 0; i < 2; i++) {
                            for (var j = 0; j < 2; j++) {
                                double newXCentroid = this.Centroid.XCentroid +
                                                      (i == 0 ? (-this.Centroid.XWidth / 4) : this.Centroid.XWidth / 4);
                                double newYCentroid = this.Centroid.YCentroid +
                                                      (j == 0 ? (-this.Centroid.YWidth / 4) : this.Centroid.YWidth / 4);
                                double newCentroidWidthX = this.Centroid.XWidth / 2;
                                double newCentroidWidthY = this.Centroid.YWidth / 2;

                                // Critical 
                                newQuads[cptSubTree] = new QuadTreeNode<T>(_logger,newXCentroid, newYCentroid,
                                    newCentroidWidthX, newCentroidWidthY, this.TreeId);
                                
                                registerQuadTree(newQuads[cptSubTree].Guid, newQuads[cptSubTree]);
                                cptSubTree++;
                            }
                        }

                        this.SubQuads = newQuads;
                        return true;
                    }
                }
            }

            return false;
        }

        public int GetMaxDepth() => this.IsLeaf() ? 0 : 1 + this.SubQuads.Max(q => q.GetMaxDepth());

        public int GetNumberElements() => this.IsLeaf()
            ? this.ObjectsInside.Count
              + this.Counters.GetOrAdd("Quad" + this.Guid + "_objectsPushedToMongo", 0)
            : SubQuads?.Sum(sq => sq.GetNumberElements()) ?? 0;

        /// <summary>
        /// work our way through the quadtree finding leaves which overlap with the defined rectangle 
        /// </summary>
        /// <param name="xCenter">The x center.</param>
        /// <param name="yCenter">The y center.</param>
        /// <param name="xWidth">Width of the x.</param>
        /// <param name="yWidth">Width of the y.</param>
        /// <returns></returns>
        public List<QuadTreeNode<T>>
            ReturnMatchingLeaves(double xCenter, double yCenter, double xWidth, double yWidth) {

            var matchingLeaves = new List<QuadTreeNode<T>>();
            var workList = new Stack<QuadTreeNode<T>>(); // holds matching quadtree bits

            if (QuadMatchesRectangle(this, ComputeCorners(xCenter, yCenter, xWidth, yWidth))) {
                workList.Push(this);
            }
            
            while (workList.Any()) {
                var q = workList.Pop();

                if (q.IsLeaf()) {
                    matchingLeaves.Add(q);
                } else {
                    foreach (var sq in q.ReturnMatchingQuadrants(xCenter, yCenter, xWidth, yWidth)) {
                        workList.Push(sq); // only add things which match
                    }
                }
            }

            return matchingLeaves;
        }
    }
}