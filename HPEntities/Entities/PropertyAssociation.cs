using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities.Enums;

namespace HPEntities.Entities {
	public class PropertyAssociation {

		public PropertyAssociation(int id, Property prop, PropertyRole role) {
			this.Id = id;
			this.Property = prop;
			this.Role = role;
		}

		public int Id { get; set; }
		public Property Property { get; set; }
		public PropertyRole Role { get; set; }

	}
}
