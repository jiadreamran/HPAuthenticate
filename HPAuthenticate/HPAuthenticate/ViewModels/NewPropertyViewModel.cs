using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPEntities.Entities;
using System.ComponentModel.DataAnnotations;
using HPEntities.Entities.Enums;
using HPEntities.Validators;
using System.Text.RegularExpressions;

namespace HPAuthenticate.ViewModels {

	public class NewPropertyViewModel: IValidatableObject {

		public NewPropertyViewModel() {
			this.ProductionTypes = 0;
			this.Properties = new List<Property>();
		}


		public string CurrentUserDisplayName { get; set; }

		public PropertyRole PropertyRole { get; set; }

		public bool IsOwner { get { return PropertyRole == HPEntities.Entities.Enums.PropertyRole.owner; } }
		public bool IsTenant { get { return PropertyRole == HPEntities.Entities.Enums.PropertyRole.authorized_producer; } }

		[Display(Name = "Owner Name")]
		[Required]
		public string OwnerName { get; set; }

		[Display(Name = "Owner mailing address")]
		[RequiredIf(
			ConditionalPropertyName = "RequireAddress",
			ErrorMessage = "Mailing address cannot be blank."
		)]
		public string OwnerMailingAddress { get; set; }

		[Display(Name = "Owner city")]
		[RequiredIf(
			ConditionalPropertyName = "RequireAddress",
			ErrorMessage = "City cannot be blank."
		)]
		public string OwnerCity { get; set; }

		[Display(Name = "Owner state")]
		[RequiredIf(
			ConditionalPropertyName = "RequireAddress",
			ErrorMessage = "Please select a state."
		)]
		public string OwnerState { get; set; }

		[Display(Name = "Owner postal code")]
		[CustomValidation(
			typeof(NewPropertyViewModel),
			"ValidatePostalCode",
			ErrorMessage = "Please enter a valid postal code."
		)]
		public string OwnerZip { get; set; }

		[Display(Name = "Owner phone number")]
		[RequiredIf(
			ConditionalPropertyName = "RequireAddress",
			ErrorMessage = "Phone number cannot be blank."
		)]
		public string OwnerPhone { get; set; }

		[ListNotEmpty(ErrorMessage = "You must select at least one property.")]
		public List<Property> Properties { get; set; }

		/// <summary>
		/// Installers/tenants cannot actually correct data, only say that it's wrong
		/// </summary>
		public bool IsDataWrong { get; set; }


		#region Validation Helpers


		public static ValidationResult ValidatePostalCode(string value, ValidationContext context) {
			if (!((NewPropertyViewModel)context.ObjectInstance).RequireAddress
					|| Regex.IsMatch(value, @"^(\d{5})(-\d{4})?$")) {
				return ValidationResult.Success;
			}
			return new ValidationResult("Please enter a valid postal code.");
		}
	
		// Require the address data if the role is owner and he's marked "IsDataWrong".
		public bool RequireAddress {
			get { return this.IsOwner && this.IsDataWrong; }
		}

		#endregion

		#region ProductionType properties

		// Most of these are here strictly for convenience using them in the view,
		// particularly the bool properties (for checkboxes).

		private bool GetProdType(DisclaimerDataType d) {
			return (ProductionTypes & d) == d;
		}

		private void SetProdType(bool value, DisclaimerDataType d) {
			if (value) {
				// Set flag
				ProductionTypes |= d;
			} else {
				// Unset flag
				ProductionTypes &= ~d;
			}
		}

		/// <summary>
		/// This represents the set of flags stored in the database for one or more production types.
		/// </summary>
		public DisclaimerDataType ProductionTypes { get; set; }

		public bool ProductionTypeAgriculture {
			get {
				return GetProdType(DisclaimerDataType.agriculture);
			}
			set {
				SetProdType(value, DisclaimerDataType.agriculture);
			}
		}

		public bool ProductionTypeIndustrial {
			get {
				return GetProdType(DisclaimerDataType.industrial);
			}
			set {
				SetProdType(value, DisclaimerDataType.industrial);
			}
		}

		public bool ProductionTypeMineralProduction {
			get {
				return GetProdType(DisclaimerDataType.mineral_production);
			}
			set {
				SetProdType(value, DisclaimerDataType.mineral_production);
			}
		}



		#endregion


		#region IValidatableObject Members

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {

			return new[] { ValidationResult.Success };
		}

		#endregion
	}
}