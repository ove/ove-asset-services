using System;

namespace OVE.Service.NetworkTiles.QuadTree.Tree {
    public class QuadCentroid : IEquatable<QuadCentroid> {

        public readonly double XCentroid;
        public readonly double YCentroid;
        public readonly double XWidth;
        public readonly double YWidth;

        #region IEquatable<QuadCentroid>

        public bool Equals(QuadCentroid other) {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return XCentroid.Equals(other.XCentroid) && YCentroid.Equals(other.YCentroid)
                                                     && XWidth.Equals(other.XWidth) && YWidth.Equals(other.YWidth);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((QuadCentroid) obj);
        }

        public override int GetHashCode() {
            unchecked {
                var hashCode = XCentroid.GetHashCode();
                hashCode = (hashCode * 397) ^ YCentroid.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(QuadCentroid left, QuadCentroid right) {
            return Equals(left, right);
        }

        public static bool operator !=(QuadCentroid left, QuadCentroid right) {
            return !Equals(left, right);
        }

        public QuadCentroid(double xCentroid, double yCentroid, double xWidth, double yWidth) {
            this.XCentroid = xCentroid;
            this.YCentroid = yCentroid;
            this.XWidth = xWidth;
            this.YWidth = yWidth;
        }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="QuadCentroid"/> class.
        /// Assume that the data is located between 0 and width and 0 and height
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public QuadCentroid(float width, float height) {
            this.XWidth = width;
            this.YWidth = height;
            this.XCentroid = width / 2;
            this.YCentroid = height / 2;
        }

        public override string ToString() {
            return $"{nameof(XCentroid)}: {XCentroid}, {nameof(YCentroid)}: {YCentroid}, {nameof(XWidth)}: {XWidth},{nameof(YWidth)}: {YWidth}";
        }
    }
}