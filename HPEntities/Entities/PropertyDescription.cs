using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {

	public class PropertyDescription {

		public PropertyDescription(PropertyAssociation prop): this(
			prop.Property.OwnerName,
			prop.Property.Description,
			"",
			prop.Role == Enums.PropertyRole.owner,
			prop.Role.ToString(),
			prop.Property.County,
			prop.Property.ParcelId
		) {
			_propDesc = prop;
		}

		public PropertyDescription(string ownerName, string legalDescription, string ownerUsername, bool isCurrentUserOwner, string userRole, string county, string parcelId) {
			this.OwnerName = ownerName;
			this.LegalDescription = legalDescription;
			this.OwnerUsername = ownerUsername;
			this.IsCurrentUserOwner = isCurrentUserOwner;
			this.UserRole = userRole;
			this.County = county;
			this.ParcelId = parcelId;
		}

		private PropertyAssociation _propDesc;

		public string OwnerName { get; private set; }
		public string LegalDescription { get; private set; }
		public string OwnerUsername { get; private set; }
		public bool IsCurrentUserOwner { get; private set; }
		public string UserRole { get; private set; }
		public string County { get; private set; }
		public string ParcelId { get; private set; }
	}

}
