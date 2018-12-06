using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    /// <summary>
    /// A class for coordinating the creation of quad trees using concurrent methods
    /// </summary>
    public class ConcurrentQuadTreeFactory<T> : AConcurrentQuadTreeFactory<T> where T : IQuadable<double> {

        /// <summary>
        /// The _shed bags - index by QuadID and 
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentBag<QuadTreeBag<T>>>
            _shedBags = new ConcurrentDictionary<string, ConcurrentBag<QuadTreeBag<T>>>();

        private readonly ConcurrentBag<QuadTreeBag<T>> _bagsForRework
            = new ConcurrentBag<QuadTreeBag<T>>();

        private readonly ConcurrentBag<string> _quadIdsWhichHaveBeenReworked = new ConcurrentBag<string>();

        public ConcurrentQuadTreeFactory(QuadCentroid centroid, ILogger logger,
            int maxBags = 10, int maxObjectsPerBag = 500, int maxWorklistSize = 250, int delay = 5) : base(logger) {
            _logger.LogInformation("---- ConcurrentQuadTreeFactory ----");
            MaxBags = maxBags;
            MaxObjectsPerBag = maxObjectsPerBag;
            MaxWorklistSize = maxWorklistSize;
            this.Delay = delay;
            this.QuadTree = new QuadTree<T>(logger,centroid,
                ShedObjects,
                MarkObjectsForRework,
                RegisterQuad, MaxBags,
                MaxObjectsPerBag);
        }

        protected override void ShedObjects(QuadTreeBag<T> bag) {//todo this method could be used to reduce memory. 
            _logger.LogInformation("---- ShedObjects ----");

            this._shedBags.AddOrUpdate(bag.QuadId, new ConcurrentBag<QuadTreeBag<T>> {bag},
                (guid, objects) => {
                    objects.Add(bag);
                    return objects;
                }
            );

            if (_quadIdsWhichHaveBeenReworked.Contains(bag.QuadId)) {
                MarkObjectsForRework(bag.QuadId);
            }
        }

        protected override void RegisterQuad(string guid, QuadTreeNode<T> quad) {
            this.Quads.AddOrUpdate(guid, quad, (duplicateGuid, duplicateQuad) => throw new ArgumentException("guid collision "+ duplicateGuid+ " "+ duplicateQuad));
        }

        protected override void MarkObjectsForRework(string quadGuid) {
            _logger.LogInformation("---- MarkObjectsForRework ----");

            _quadIdsWhichHaveBeenReworked.Add(quadGuid);

            // pull all the bags for the quad tree
            ConcurrentBag<QuadTreeBag<T>> bagsToReWork =
                _shedBags.GetOrAdd(quadGuid, new ConcurrentBag<QuadTreeBag<T>>());

            // place those bags within the rework list
            while (bagsToReWork.TryTake(out var bag)) {
                this._bagsForRework.Add(bag);
            }
        }

        public override bool HasCleanState() {
            var hasCleanState = WorkList.IsEmpty && ReworkQueueEmpty();
            if (!hasCleanState) {
                _logger.LogWarning("Quad tree has Bad state");
            }

            return hasCleanState;
        }

        public override int TotalObjectsInStorage() {
            return this._shedBags.Sum(quadBags => quadBags.Value.Sum(b => b.Objects.Count));
        }

        public override string PrintState() {
            StringBuilder sb = new StringBuilder();
            sb.Append(base.PrintState());
            sb.AppendLine($"rework list = {this._bagsForRework.Count}");
            sb.AppendLine($"shed list = {this._shedBags.Count}");
            return sb.ToString();
        }

        protected override bool ReworkQueueEmpty() {
            return this._bagsForRework.IsEmpty;
        }

        public override bool GetReworkBag(out QuadTreeBag<T> bag) {// todo this is where backs are restored from the rework - i think 
            return this._bagsForRework.TryTake(out bag); // pass by reference (twice)
        }

        public override string PrintShedBags() {
            return this._shedBags.Aggregate("number of bags " + this._shedBags.Count,
                (acc, next) => acc + Environment.NewLine +
                               next.Key + "=" + next.Value.Count + "bags " +
                               next.Value.Aggregate("",
                                   (n, a) => n + "," + a.Objects.Count + (a.NeedsRework ? "!" : "")));
        }

        // todo Remove after tests. Should never be called
        public override bool GetReworkBag(out QuadTreeBag<T> bag, string treeId) {
            return this._bagsForRework.TryTake(out bag); // pass by reference (twice)
        }

        public override QuadTreeBag<T>[] GetBagsForQuad(QuadTreeNode<T> quad) {
            if (this._shedBags.TryGetValue(quad.Guid, out var res)) {
                // todo 
                return res.ToArray();
            }

            return new QuadTreeBag<T>[0];
        }

        public override Dictionary<string, QuadTreeNode<T>> SelectLeafs() {
            return this.Quads.Where(q => q.Value.IsLeaf()).ToDictionary(q => q.Key, q => q.Value);
        }
    }
}