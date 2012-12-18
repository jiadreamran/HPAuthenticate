using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using libMatt.Converters;
using libMatt.Formatters;

namespace HPEntities.Validators {
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class RequireAtLeastOneAttribute: RequiredAttribute {

		/// <summary>
		/// This identifier is used to group properties together.
		/// Pick a number and assign it to each of the properties
		/// among which you wish to require one.
		/// </summary>
		public int GroupId { get; set; }

		/// <summary>
		/// This defines the message key any errors will be associated
		/// with, so that they can be accessed via the front end using
		/// @Html.ValidationMessage(errorMessageKey).
		/// </summary>
		public string ErrorMessageKey { get; set; }


		/// <summary>
		/// Specifies a property on the class to check before performing
		/// validation on this property - if the value of the specified
		/// property returns true, then validation will proceed. If it 
		/// returns false, validation will be skipped.
		/// 
		/// If the ValidateIfProperty is not populated, then validation
		/// will proceed.
		/// </summary>
		public string ValidateIfProperty { get; set; }

		/// <summary>
		/// If this property is set with the name of a property on the object
		/// being validated, then validation will only continue if the given
		/// object property's value returns false. If the ValidateUnlessProperty
		/// value returns true, validation will be skipped.
		/// </summary>
		public string ValidateUnlessProperty { get; set; }

		public override bool IsValid(object value) {
			return true;

			if (value == null)
				return true;


			// Find all properties on the class having a "PropertyGroupAttribute"
			// with GroupId matching the one on this attribute
			var typeInfo = value.GetType();
			var propInfo = typeInfo.GetProperties();
			foreach (var prop in propInfo) {
				foreach (PropertyGroupAttribute attr in prop.GetCustomAttributes(typeof(PropertyGroupAttribute), false)) {
					if (attr.GroupId == this.GroupId
						&& !string.IsNullOrWhiteSpace(prop.GetValue(value, null).GetString())) {
						return true;
					}
				}
			}
			return false;
		}

	}


	[AttributeUsage(AttributeTargets.Property)]
	public class PropertyGroupAttribute : Attribute {

		public PropertyGroupAttribute(int groupId) {
			this.GroupId = groupId;
		}

		public int GroupId { get; set; }

	}

}
