using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using HPEntities.Validators;
using libMatt.Converters;
using HPEntities.Entities;
using HPEntities.Dalcs;
using System.Web.Security;

namespace HPEntities {
	// MWinckler.20110914: Next time try separating validation into a metadata class like so:
	// http://www.paraesthesia.com/archive/2010/01/28/separating-metadata-classes-from-model-classes-in-dataannotations-using-custom.aspx

	public class User : MembershipUser, IValidatableObject {

		public User(): this (-1) {
		}

		public User(int id) {
			this.Id = id;
			this.PhoneNumbers = new List<PhoneNumber>();
			this.Addresses = new List<Address>();
			this.IsEmailConfirmed = false;
			this.IsAdmin = false;
		}


		#region Web Account Info

		/// <summary>
		/// This corresponds to Clients.ClientID.
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// If the user is an admin and is acting as someone else,
		/// this property stores the user id of the person he's acting as.
		/// </summary>
		public int? ActingAsUserId { get; set; }


		[Required(ErrorMessage = "Last name cannot be blank.")]
		[StringLength(50)] // According to database constraints.
		[Display(Name = "Last name")]
		public string LastNameOrCompany { get; set; }

		/* MWinckler.20120125: First name is now permitted to be blank.
		[Required(ErrorMessage = "First name cannot be blank.")]
		[StringLength(20)] // According to database constraints.
		 */
		[Display(Name = "First name")]
		public string FirstName { get; set; }

		[Display(Name = "Middle Initial")]
		[StringLength(1)] // According to database constraints.
		public string MiddleInitial { get; set; }

		[Display(Name = "Suffix")]
		[StringLength(5, ErrorMessage = "Suffix cannot be more than 5 characters long.")]
		public string Suffix { get; set; }

		[Display(Name = "Preferred Name")]
		[StringLength(20)] // According to database constraints.
		public string PreferredName { get; set; }

		[Display(Name = "Courtesy Title")]
		public int? CourtesyTitleId { get; set; }

		// [Required(ErrorMessage = "Please enter a mailing address.", ErrorMessageKey = "Address")]
		[ListNotEmpty(ErrorMessage = "Please provide a complete mailing address.")]
		public List<Address> Addresses { get; set; }

		[ListNotEmpty(ErrorMessage = "Please provide at least one phone number.")]
		public List<PhoneNumber> PhoneNumbers { get; set; }


		public string DisplayName {
			get {
				return (FirstName + " " + LastNameOrCompany).Trim();
			}
		}

		public bool IsAdmin { get; set; }

		#endregion

		
		/// <summary>
		/// The user's email address. This must be valid; confirmation emails
		/// are sent to this address.
		/// </summary>
		[DataMember]
		[DataType(DataType.EmailAddress)]
		[RegularExpression(@"^[\w+\-.]+@[a-z\d\-.]+\.[a-z]+$", ErrorMessage = "Please enter a valid email address.")]
		// [Required(ErrorMessage = "Email cannot be blank.")]
		public string Email { get; set; }


		// #############################
		// Auth-specific info
		// #############################

		/// <summary>
		/// The users's password, one-way hashed via bcrypt.
		/// This property should not be serialized, as it is to 
		/// be used solely within the WCF services for authentication.
		/// </summary>
		public string PasswordHashed { get; set; }


		/// <summary>
		/// The user's plaintext password.
		/// This property is only to be used for changing the password.
		/// It will never be populated from the database because we do
		/// not keep plaintext passwords.
		/// </summary>
		[DataType(DataType.Password)]
		[Required(ErrorMessage = "You must provide a password.")]
		public string Password { get; set; }

		/// <summary>
		/// This property is used to confirm that the user has entered
		/// the correct password when changing it.
		/// </summary>
		[DataType(DataType.Password)]
		[Display(Name = "Confirm password")]
		[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
		public string PasswordConfirmation { get; set; }


		private List<PropertyAssociation> _properties = null;
		public List<PropertyAssociation> Properties {
			get {
				if (_properties == null) {
					_properties = new UserDalc().GetAssociatedProperties(this.Id);
				}
				return _properties;
			}
			set {
				_properties = value;
			}
		}


		public bool IsEmailConfirmed { get; set; }


		#region IValidatableObject Members

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
			var results = new List<ValidationResult>();

			// This keeps track of whether each "RequireAtLeastOne" group has been satisfied
			var groupStatus = new Dictionary<int, bool>();
			// This stores the error messages for each group as defined
			// by the RequireAtLeastOneAttributes on the model
			var errorMessages = new Dictionary<int, ValidationResult>();

			Func<object, string, object> getPropertyValue = (parentObj, propName) => {
				if (parentObj == null)
					return null;

				var p = parentObj.GetType().GetProperty(propName);
				if (p != null) {
					return p.GetValue(parentObj, null);
				}
				return null;
			};


			// Find all "RequireAtLeastOne" property validators 
			foreach (RequireAtLeastOneAttribute attr in Attribute.GetCustomAttributes(this.GetType(), typeof(RequireAtLeastOneAttribute), true)) {
				// If the ValidateIfProperty value tells us false, skip validation.
				if (!string.IsNullOrWhiteSpace(attr.ValidateIfProperty)
					&& !getPropertyValue(this, attr.ValidateIfProperty).ToBoolean()) {
					continue;
				}

				// Likewise, if the ValidateUnlessProperty values tells us true, skip validation.
				if (!string.IsNullOrWhiteSpace(attr.ValidateUnlessProperty)
					&& getPropertyValue(this, attr.ValidateUnlessProperty).ToBoolean()) {
					continue;
				}

				groupStatus.Add(attr.GroupId, false);
				errorMessages[attr.GroupId] = new ValidationResult(attr.ErrorMessage, new string[] { attr.ErrorMessageKey });
			}

			// For each property on this class, check to see whether
			// it's got a PropertyGroup attribute, and if so, see if
			// it's been populated, and if so, mark that group as "satisfied".
			var propInfo = this.GetType().GetProperties();
			bool status;
			foreach (var prop in propInfo) {
				foreach (PropertyGroupAttribute attr in prop.GetCustomAttributes(typeof(PropertyGroupAttribute), false)) {
					if (groupStatus.TryGetValue(attr.GroupId, out status) && !status
						&& !string.IsNullOrWhiteSpace(prop.GetValue(this, null).GetString())) {
						groupStatus[attr.GroupId] = true;
					}
				}
			}

			// If any groups did not have at least one property 
			// populated, add their error messages to the
			// validation result.
			foreach (var kv in groupStatus) {
				if (!kv.Value) {
					results.Add(errorMessages[kv.Key]);
				}
			}

			return results;
		}

		#endregion
	}
}