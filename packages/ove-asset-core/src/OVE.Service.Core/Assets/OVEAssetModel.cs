using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global << needed by EntityFramework
// ReSharper disable UnusedMember.Global

namespace OVE.Service.Core.Assets {
    /// <summary>
    /// Asset file model to represent OVE assets 
    /// </summary>
    [Table("OVEAssetModels")]
    public class OVEAssetModel {

        [ScaffoldColumn(false)]
        public string Id { get; set; }
        
        [Required(AllowEmptyStrings = false)]
        [MinLength(3)]
        [MaxLength(63)]
        [RegularExpression(@"^[-a-z0-9]+$", ErrorMessage = "Please keep Projects names to lowercase letters, numbers and underscores")]
        public string Project { get; set; }

        [Required(AllowEmptyStrings = false)]
        [RegularExpression(@"^[-a-zA-Z0-9_.]+$", ErrorMessage = "Please keep file names to letters, numbers, dashes and underscores")]
        [MaxLength(50, ErrorMessage = "Please keep file names short - 50 characters")]
        public string Name { get; set; }
        
        public string Description { get; set; }

        [ScaffoldColumn(false)]
        public DateTime LastModified { get; set; } = DateTime.Now;

        /// <summary>
        /// Specify which service this asset should be processed by
        /// </summary>
        [Required]
        public string Service { get; set; }

        /// <summary>
        /// This is the actual location of the file on the object store
        /// </summary>
        [ScaffoldColumn(false)]
        public string StorageLocation { get; set; }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetStorageGuid() {
            return this.StorageLocation.Split(",")?.FirstOrDefault();
        }

        /// <summary>
        /// How far processing has progressed
        /// </summary>
        [ScaffoldColumn(false)]
        public string ProcessingErrors { get; set; } = "none";

        [ConcurrencyCheck]
        [ScaffoldColumn(false)]
        public int ProcessingState { get; set; }

        /// <summary>
        /// Provides additional storage for meta data for this asset
        /// AssetMeta is an optional place to store optional meta data about the asset which can be updated through API by other services. 
        /// </summary>
        public string AssetMeta { get; set; }

        public override string ToString() {
            return$"{nameof(Id)}: {Id}, {nameof(Project)}: {Project}, {nameof(Name)}: {Name}," +
                  $" {nameof(Service)}: {Service}, {nameof(StorageLocation)}: {StorageLocation}," +
                  $" {nameof(ProcessingErrors)}: {ProcessingErrors}, {nameof(ProcessingState)}: {ProcessingState}";
        }
    }
}
