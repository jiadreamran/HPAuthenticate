using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	/// <summary>
	/// A helper class to store results for front-end autocompleters,
	/// designed to work with jQuery UI and be serialized to JSON.
	/// </summary>
	public class AutocompleteResult {

		public AutocompleteResult(string value): this(value, value) { }

		public AutocompleteResult(string value, string label) {
			this.label = label;
			this.value = value;
		}

		public string label { get; set; }

		public string value { get; set; }

	}
}
