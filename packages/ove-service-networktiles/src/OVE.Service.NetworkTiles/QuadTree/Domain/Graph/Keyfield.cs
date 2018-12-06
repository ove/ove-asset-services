using System.Collections.Generic;

namespace OVE.Service.NetworkTiles.QuadTree.Domain.Graph {
    public class KeyField {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ValueType { get; set; }
        public string AppliesTo { get; set; }

        public override string ToString() {
            return $"{{ id = {Id}, name = {Name}, type = {ValueType}, appliesTo = {AppliesTo} }}";
        }

        public override bool Equals(object value) {
            return value is KeyField other &&
                   EqualityComparer<string>.Default.Equals(other.Id, Id) &&
                   EqualityComparer<string>.Default.Equals(other.Name, Name) &&
                   EqualityComparer<string>.Default.Equals(other.ValueType, ValueType) &&
                   EqualityComparer<string>.Default.Equals(other.AppliesTo, AppliesTo);
        }

        public override int GetHashCode() {
            int num = 0x7a2f0b42;
            num = (-1521134295 * num) + EqualityComparer<string>.Default.GetHashCode(Id);
            num = (-1521134295 * num) + EqualityComparer<string>.Default.GetHashCode(Name);
            num = (-1521134295 * num) + EqualityComparer<string>.Default.GetHashCode(ValueType);
            return (-1521134295 * num) + EqualityComparer<string>.Default.GetHashCode(AppliesTo);
        }
    }
}
