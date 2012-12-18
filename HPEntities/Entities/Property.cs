using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	/// <summary>
	/// Represents a property as might be stored in the AppraisalRolls table,
	/// but this class only contains fields we actually care about.
	/// </summary>
	public class Property {

		public Property(int customId, string description)
			: this(customId, null, null, description) { }

		public Property(string parcelId, string county, string description)
			: this(null, parcelId, county, description) { }

		public Property(int? customId, string parcelId, string county, string description) {
			this.CustomId = customId;
			this.ParcelId = parcelId;
			this.County = county;
			this.Description = description;
		}

		public int? CustomId { get; set; }
		public string ParcelId { get; set; }
		public string County { get; set; }
		public string Description { get; set; }

		public string Address { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string Zip { get; set; }

		/// <summary>
		/// This is an optional property, not always populated.
		/// </summary>
		public string OwnerName { get; set; }

		public bool IsNew {
			get {
				return !((this.CustomId.HasValue && this.CustomId.Value > 0) || (!string.IsNullOrEmpty(this.ParcelId)));
			}
		}

		public bool IsAppraisalRollDataModified { get; set; }

	}
}
