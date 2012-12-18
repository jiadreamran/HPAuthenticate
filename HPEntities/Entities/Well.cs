using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	public class Well {

		public int WellId { get; set; }
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public int? PermitNumber { get; set; }
		public int CountyId { get; set; }
		public string County { get; set; }
		
		/// <summary>
		/// This represents a user response to a detected error condition on the well.
		/// </summary>
		public string ErrorResponse { get; set; }

		private int[] _miids;
		public int[] MeterInstallationIds {
			get {
				if (_miids == null) {
					_miids = new int[] { };
				}
				return _miids;
			}
			set {
				_miids = value;
			}
		}

	}
}