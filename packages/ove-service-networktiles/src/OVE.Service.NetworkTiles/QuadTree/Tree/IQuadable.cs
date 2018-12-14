using System;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    /// <summary>
    /// todo The future of this class is yet to be determined
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IQuadable<T> where T : IComparable {
    // T GetDimA();
    // T GetDimB();
    bool IsWithin<TT>(QuadTreeNode<TT> q) where TT : IQuadable<double>;
        string GetId();
    }
}