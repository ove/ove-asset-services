using System.Collections.Generic;

// ReSharper disable MemberCanBePrivate.Global needs to be global for serialization
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global setter needed for serialization 
namespace OVE.Service.NetworkTiles.QuadTree.Tree {

    public class QuadTreeBag<T> {
        // Critical
        public string TreeId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier - this is id of the quad tree NODE these objects belonged to 
        /// </summary>
        public string QuadId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier - this is the unique id of this bag
        /// </summary>
        public string BagId { get; set; }

        public List<T> Objects { get; set; }

        /// <summary>
        /// whether or not the bag needs some rework or not
        /// </summary>
        public bool NeedsRework { get; set; }

        // Critical
        public QuadTreeBag(string s, List<T> lso, string treeId) {
            BagId = System.Guid.NewGuid().ToString();
            QuadId = s;
            Objects = lso;
            this.TreeId = treeId;
        }

    }
}