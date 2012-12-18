using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using libMatt.Converters;

namespace HPEntities.Validators {
	/// <summary>
	/// This attribute only validates if the value of the property corresponding
	/// to "ConditionalPropertyName" on the object being validated returns true.
	/// In other words, it looks to the property named ConditionalPropertyName on
	/// the object being validated, and if the value of that property returns a
	/// true value, then validation proceeds. Else, validation on the property
	/// being validated (not ConditionalPropertyName) is skipped.
	/// 
	/// To invert the comparison (i.e. validate when ConditionalPropertyName value
	/// returns false), set InvertConditional true.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class RequiredIfAttribute: RequiredAttribute, IConditionalValidator {

		private ConditionalValidator _condition = new ConditionalValidator();


		public string ConditionalPropertyName {
			get { return _condition.ConditionalPropertyName; }
			set { _condition.ConditionalPropertyName = value; }
		}


		public bool InvertConditional {
			get { return _condition.InvertConditional; }
			set { _condition.InvertConditional = value; } 
		}

		protected override ValidationResult IsValid(object value, ValidationContext validationContext) {
			if (_condition.NeedsValidation(value, validationContext)) {
				return base.IsValid(value, validationContext);
			}

			return ValidationResult.Success;
		}
		
	}

}
