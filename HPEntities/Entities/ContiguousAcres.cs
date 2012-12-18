using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	public class ContiguousAcres {

		public ContiguousAcres(int objectId, int caId, string description, double areaInAcres, bool isApproved, int ownerClientId) {
			this.OBJECTID = objectId;
			this.caID = caId;
			this.Description = description;
			this.AreaInAcres = areaInAcres;
			this.IsApproved = isApproved;
			this.OwnerClientId = ownerClientId;
		}

		public int OBJECTID { get; set; }
		public int caID { get; set; }
		public double AreaInAcres { get; set; }
		public string Description { get; set; }
		public bool IsApproved { get; set; }

		public Well[] Wells { get; set; }

		public int OwnerClientId { get; set; }
	}
}
