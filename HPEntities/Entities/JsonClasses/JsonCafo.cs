using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonCafo {

		/// <summary>
		/// A unique identifier assigned by the database;
		/// will be -1 for an unsaved record.
		/// </summary>
		public int id { get; set; }

		/// <summary>
		/// Average number of livestock per day.
		/// </summary>
		public int avgLivestock { get; set; }

		/// <summary>
		/// Integer CAFO type ID corresponding to the 
		/// CafoUsageLookup table in the database.
		/// </summary>
		public int cafoId { get; set; }

		/// <summary>
		/// The calculated volume for this CAFO's usage,
		/// in gallons.
		/// </summary>
		public int calculatedVolumeGallons { get; set; }

		/// <summary>
		/// Indicates whether the user accepts the
		/// calculated volume.
		/// </summary>
		public bool acceptCalculation { get; set; }

		/// <summary>
		/// The user-revised volume, in gallons, only
		/// applicable if the user does not accept the
		/// default calculated volume.
		/// </summary>
		public int? userRevisedVolume { get; set; }

		/// <summary>
		/// Units the user-revised volume is stored in.
		/// This can be either the unit ID or its string
		/// name representation (see Config.ConvertVolume).
		/// </summary>
		public int? userRevisedVolumeUnitId { get; set; }
 
	}
}
