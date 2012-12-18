using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Collections;

namespace HPEntities.Validators {
	/// <summary>
	/// Requires the decorated IList to have at least one element.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public class ListNotEmptyAttribute: RequiredAttribute {


		protected override ValidationResult IsValid(object value, ValidationContext validationContext) {
			var list = value as IList;
			if (list == null || list.Count == 0) {
				return new ValidationResult(this.ErrorMessage);
			}
			return ValidationResult.Success;
		}

	}
}
