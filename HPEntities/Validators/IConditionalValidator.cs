using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Validators {
	public interface IConditionalValidator {
		/// <summary>
		/// The name of the property on the object being validated whose
		/// value will determine whether this validation proceeds. If that
		/// property value returns true, this validation will be in effect.
		/// If the conditional property returns false, then this validation
		/// will be considered to have passed without actually being tested.
		/// </summary>
		string ConditionalPropertyName { get; set; }

		/// <summary>
		/// If true, the validation will proceed if the conditioanl property
		/// value is _false_. In essence, this attribute will become a 
		/// "RequiredUnless" attribute.
		/// </summary>
		bool InvertConditional { get; set; }
	}
}
