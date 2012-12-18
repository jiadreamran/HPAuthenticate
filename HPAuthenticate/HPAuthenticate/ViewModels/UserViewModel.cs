using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPEntities;
using HPEntities.Entities;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using HPEntities.Validators;
using HPEntities.Dalcs;
using HPEntities.Entities.Enums;

namespace HPAuthenticate.ViewModels {

	// TODO: This is bad. UserViewModel should not inherit from User.
	// They should now be totally independent. Clean this up!
	public class UserViewModel {

		// MVC controller requires a parameterless constructor
		public UserViewModel() : this(null, UserEditMode.create) { }

		public UserViewModel(User user, UserEditMode editMode) {
			this.User = user ?? new User();
			this.EditMode = editMode;
		}

		public User User { get; set; }
		public UserEditMode EditMode { get; set; }

		public bool Editing { get { return EditMode == UserEditMode.edit; } }
		public bool EditingOrAdminCreate { get { return Editing || EditMode == UserEditMode.admin_create; } }

		private static IEnumerable<State> _availableStates = null;
		public static List<SelectListItem> AvailableStates {
			get {
				if (_availableStates == null) {
					_availableStates = new SillyAbstractionDalc().GetAllStates();
				}

				return new[] { new SelectListItem() { Text = "--Select One", Value = "", Selected = true } }.Concat(
						_availableStates.OrderBy(x => x.Name).Select(s => new SelectListItem() { Text = s.Name, Value = s.Abbreviation })
					).ToList();
			}
		}

		private Dictionary<int, string> _courtesyTitles = null;
		public List<SelectListItem> AvailableCourtesyTitles {
			get {
				if (_courtesyTitles == null) {
					_courtesyTitles = new UserDalc().GetAvailableCourtesyTitles();
				}
				return new[] { new SelectListItem() { Text = "--Select one--", Value = "" } }.Concat(
					_courtesyTitles.Select(s => new SelectListItem() { Text = s.Value, Value = s.Key.ToString() })
				).ToList();
			}
		}


		private Address FirstAddress {
			get {
				if (User.Addresses.Count == 0) {
					User.Addresses.Add(new Address());
				}
				return User.Addresses.First();
			}
		}

		public int AddressId { get { return FirstAddress.Id; } }

		[Required]
		public string Address {
			get { return FirstAddress.MailingAddress; }
			set { FirstAddress.MailingAddress = value; }
		}

		[Required]
		public string City {
			get { return FirstAddress.City; }
			set { FirstAddress.City = value; }
		}

		[Required]
		public string State {
			get { return FirstAddress.StateCode; }
			set { FirstAddress.StateCode = value; }
		}

		[RegularExpression(@"^\d{5}(-?\d{4})?$", ErrorMessage = "Please provide a valid postal code.")]
		public string PostalCode {
			get {
				// MVC sometimes calls this getter on initialization when blank
				if (FirstAddress == null || string.IsNullOrEmpty(FirstAddress.Zip)) {
					return "";
				}

				// See below for database shenanigans regarding hyphens, but
				// I've never seen a ZIP+4 without a dash in it, so I'm putting
				// the dash back in, dash it all.
				if (FirstAddress.Zip.Length == 9) {
					return FirstAddress.Zip.Substring(0, 5) + "-" + FirstAddress.Zip.Substring(5, 4);
				}
				return FirstAddress.Zip; 
			}
			set { 
				// The database is ... about storing zip code; it insists on
				// 9 characters max, meaning no dashes allowed. So if there's
				// any non-numeric digits here, strip them out when setting.
				FirstAddress.Zip = value.Replace("-", ""); 
			}
		}

		[RequiredIf(
			ConditionalPropertyName = "EditingOrAdminCreate",
			InvertConditional = true
		)]
		public string Email {
			get { return User.Email; }
			set { User.Email = value; }
		}

		// This is a hack workaround for a bug in Microsoft's jQuery unobtrusive validation script.
		// As of 20111028, attempting the compare validation on the property of a property (in this
		// case, User.Password and User.PasswordConfirmation) always fails. This workaround makes
		// it work. In the future, update the unobtrusive validation script when a fix is available
		// from Microsoft.
		[DataType(DataType.Password)]
		[RequiredIf(
			ConditionalPropertyName = "EditingOrAdminCreate",
			InvertConditional = true,
			ErrorMessage = "You must provide a password."
		)]
		[StringLength(99999999, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
		public string Password {
			get { return User.Password; }
			set { User.Password = value; }
		}

		[DataType(DataType.Password)]
		[Display(Name = "Confirm password")]
		[Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
		public string PasswordConfirmation {
			get { return User.PasswordConfirmation; }
			set { User.PasswordConfirmation = value; }
		}


		public List<PropertyAssociation> Properties { get { return User.Properties; } }


		#region View Helpers

		private Dictionary<string, string> _phoneTypes;
		public Dictionary<string, string> PhoneTypes {
			get {
				if (_phoneTypes == null) {
					_phoneTypes = new SillyAbstractionDalc().GetPhoneTypes();
				}
				return _phoneTypes;
			}
		}

		public int PhoneIndex { get; set; }

		#endregion

	}
}