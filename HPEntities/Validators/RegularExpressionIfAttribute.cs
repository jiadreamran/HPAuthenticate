using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace HPEntities.Validators {
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
	public class RegularExpressionIfAttribute: RegularExpressionAttribute, IConditionalValidator {

		private ConditionalValidator _condition = new ConditionalValidator();

		public RegularExpressionIfAttribute(string pattern)
			: base(pattern) {

		}

		public string ConditionalPropertyName {
			get { return _condition.ConditionalPropertyName; }
			set { _condition.ConditionalPropertyName = value; } 
		}

		/// <summary>
		/// If true, the validation will proceed if the conditioanl property
		/// value is _false_. In essence, this attribute will become a 
		/// "RequiredUnless" attribute.
		/// </summary>
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
