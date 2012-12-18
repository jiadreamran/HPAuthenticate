using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	/// <summary>
	/// As in, United.
	/// </summary>
	/// <remarks>This qualifies as a silly abstraction, forced upon the code by the database.</remarks>
	public class State {

		public State(int id, string name, string abbreviation) {
			this.Id = id;
			this.Name = name;
			this.Abbreviation = abbreviation;
		}

		public int Id { get; set; }
		public string Name { get; set; }
		public string Abbreviation { get; set; }
	}
}
