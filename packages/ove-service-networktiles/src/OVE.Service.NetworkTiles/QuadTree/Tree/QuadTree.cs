using System;
using System.Collections.Generic;
using System.Linq;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    public class QuadTree<T> where T : IQuadable<double> {
        private readonly int _maxBagsBeforeSplit;
        private readonly int _maxObjectsPerBag;

        public readonly QuadTreeNode<T> Root;
        private readonly Action<QuadTreeBag<T>> _shedObjects;
        private readonly Action<string> _markObjectsForRework;
        private readonly Action<string, QuadTreeNode<T>> _registerQuadTree;

        public string TreeId { get; } = Guid.NewGuid().ToString();

        public QuadTree(QuadCentroid centroid, Action<QuadTreeBag<T>> shedObjects, Action<string> markObjectsForRework,
            Action<string, QuadTreeNode<T>> registerQuadTree, int maxBagsBeforeSplit = 10, int maxObjectsPerBag = 500) {
        
            this.Root = new QuadTreeNode<T>(centroid, TreeId);
            this._shedObjects = shedObjects;
            this._markObjectsForRework = markObjectsForRework;
            this._registerQuadTree = registerQuadTree;
            _maxBagsBeforeSplit = maxBagsBeforeSplit;
            _maxObjectsPerBag = maxObjectsPerBag;
            registerQuadTree(this.Root.Guid, this.Root);
        }

        #region Add
        
        public void AddObject(T o) {
            var worklist = new Stack<Tuple<QuadTreeNode<T>, T>>();
            worklist.Push(new Tuple<QuadTreeNode<T>, T>(this.Root, o));
            AddObjects(worklist);
        }

        internal void AddObject(QuadTreeNode<T> quad, T o) {
            var worklist = new Stack<Tuple<QuadTreeNode<T>, T>>();
            worklist.Push(new Tuple<QuadTreeNode<T>, T>(quad, o));
            AddObjects(worklist);
        }

        private void AddObjects(Stack<Tuple<QuadTreeNode<T>, T>> workList) {

            while (workList.Any()) {
                var item = workList.Pop();
                var quad = item.Item1;
                var o = item.Item2;

                // cases    
                // we're a node - we chose the correct quadrant and recurse
                // we're a leaf and we're not full - we add
                // we're a lead and we are full - we break down and add objects...
                while (quad != null && !quad.IsLeaf()) {
                    var quads = quad.ReturnMatchingQuadrants(o); // this avoids recursion
                    if (!quads.Any()) {
                        Console.WriteLine("logic error, no quad matched an item");
                    }

                    quad = quads.First(); // recurse down the quadtree for the first object
                    foreach (var aQuad in quads.Skip(1)) {
                        // add any others to the worklist
                        workList.Push(new Tuple<QuadTreeNode<T>, T>(aQuad, o));
                    }
                }

                if (quad == null) {
                    throw new ArgumentException("null quad tree");
                }

                // add the object
                quad.ObjectsInside.Add(o);

                // Shed objects if we have more than Max inside, and erase the local objects - this avoids memory intensity 
                bool madeChildren = false;
                if (quad.ObjectsInside.Count >= _maxObjectsPerBag) {
                    ShedObjectsToExternalStore(quad);
                    // if we've pushed sufficient bags then make children and mark all of those bags for rework 
                    if (quad.Counters.GetOrAdd("BagsPushedToMongo", 0) >= _maxBagsBeforeSplit) {
                        // note that this locks on the object
                        madeChildren = quad.TryGenerateChildren(_registerQuadTree);

                        // we are now full so we should turn into a node - to do this we must retrieve objects from Mongo and add them to the worklist 
                        // If we are turning into a node, then we need to put back the objects inside the worklist, by setting the flag on the guid objects to true
                        if (madeChildren) _markObjectsForRework(quad.Guid);
                    }
                }

                // // if the leaf has been made a node then remove any last objects left which may have been added by other threads ... 

                if (madeChildren || (!quad.IsLeaf() && !quad.ObjectsInside.IsEmpty)) {
                    ShedObjectsToExternalStore(quad);
                }
            }

        }

        private void ShedObjectsToExternalStore(QuadTreeNode<T> quad) {
            Console.WriteLine("---- ShedObjectsToExternalStore ---- quad.treeId: " + quad.TreeId);
            // extract objects and shed them
            var quadTreeBag = ExtractObjectsFromQuad(quad);
            _shedObjects(quadTreeBag);
            // log global # objects shed - for debug only 
            quad.Counters.AddOrUpdate("ObjectsShed", k => quadTreeBag.Objects.Count,
                (k, v) => v + quadTreeBag.Objects.Count);
            // count the # bags this quad has shed. 
            quad.Counters.AddOrUpdate("BagsPushedToMongo", k => 1, (k, v) => v + 1);
        }

        private static QuadTreeBag<T> ExtractObjectsFromQuad(QuadTreeNode<T> quad) {
            // collect together a bag of objects to shed
            List<T> objectsToShed = new List<T>();
            while (quad.ObjectsInside.TryTake(out var t)){
                objectsToShed.Add(t);
            }

            // then shed them
            var quadTreeBag = new QuadTreeBag<T>(quad.Guid, objectsToShed, quad.TreeId);
            return quadTreeBag;
        }

        #endregion

        #region Shed to External Storage

        /// <summary>
        /// Forces all nodes to shed all of their contents to external storage
        /// </summary>
        public void ShedAllObjects() {
            ShedAllObjects(this.Root);
        }

        private void ShedAllObjects(QuadTreeNode<T> quad) {
            if (!quad.ObjectsInside.IsEmpty) {
                ShedObjectsToExternalStore(quad);
            }

            if (!quad.IsLeaf()) {
                foreach (var child in quad.SubQuads) {
                    ShedAllObjects(child);
                }
            }
        }

        #endregion

    }
}