using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using libMatt.Converters;

namespace HPEntities.Validators {
	internal class ConditionalValidator {
		/// <summary>
		/// The name of the property on the object being validated whose
		/// value will determine whether this validation proceeds. If that
		/// property value returns true, this validation will be in effect.
		/// If the conditional property returns false, then this validation
		/// will be considered to have passed without actually being tested.
		/// </summary>
		public string ConditionalPropertyName { get; set; }

		/// <summary>
		/// If true, the validation will proceed if the conditioanl property
		/// value is _false_. In essence, this attribute will become a 
		/// "RequiredUnless" attribute.
		/// </summary>
		public bool InvertConditional { get; set; }

		internal bool NeedsValidation(object value, ValidationContext validationContext) {
			if (validationContext.ObjectInstance == null)
				return false;

			if (!string.IsNullOrWhiteSpace(this.ConditionalPropertyName)) {
				// A conditional property name is present, so test for its value.
				// If the XOR of "InvertConditional" and the actual value of the 
				// conditional property returns FALSE, then we do not want to validate,
				// because this is a "RequireIf" attribute - validation only applies
				// if the given conditional (inverted as desired) evaluates true.
				if (!(this.InvertConditional ^ validationContext.ObjectInstance.GetType().GetProperty(
												this.ConditionalPropertyName
											).GetValue(validationContext.ObjectInstance, null).ToBoolean())) {
					return false;
				}
			}

			return true;
		}
	}
}
