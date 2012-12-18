using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPAuthenticate.ViewModels;
using System.Text.RegularExpressions;
using libMatt.Converters;
using HPEntities.Entities;
using System.Web.Mvc;
using HPEntities;
using HPEntities.Dalcs;

namespace HPAuthenticate.Helpers {
	public class UserHelper {

		public static bool PopulatePhoneNumbers(UserViewModel uvm, HttpRequestBase request, out string validationError, out string flashErrorMessage) {
			flashErrorMessage = null;
			validationError = null;

			if (uvm == null || request == null)
				return false;

			// Find and (re)populate phone numbers.
			foreach (var phoneKey in request.Params.AllKeys.Where(x => x.StartsWith("phone_number_type"))) {
				var phoneTypeId = request[phoneKey].TryToInteger();
				var index = Regex.Match(phoneKey, @"\d+").Value;
				var phoneNumber = request[string.Format("phone_number[{0}]", index)];
				if (phoneTypeId.HasValue) {
					// TODO: If the number contains an "x", split it out into number and extension.
					var parts = phoneNumber.ToLower().Split('x');
					string extension = "";
					string number = Regex.Replace(parts[0], @"[^\d]", "");
					if (parts.Length > 1) {
						// Toss all the rest into the extension.
						extension = string.Join("", parts.Skip(1));
					}
					// If the phone number is blank, just toss the entry - each form usually gets
					// a blank spot added to it in case the user wants to add numbers, but he doesn't have to.
					if (!string.IsNullOrEmpty(phoneNumber)) {
						uvm.User.PhoneNumbers.Add(new PhoneNumber(request[string.Format("phone_number_id[{0}]", index)].TryToInteger(), phoneTypeId.Value, number, extension));
					}
				} else {
					flashErrorMessage = "Invalid phone number type - please select a valid phone type from the dropdown list.";
					validationError = "Invalid phone type.";
					return false;
				}
			}
			return true;
		}

		/// <summary>
		/// Attempts to look up an existing user based on user ID. Returns the User object if found,
		/// else null. If an existing user is found, this method overwrites its first name, middle initial,
		/// last name, 
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		public static bool TryGetExistingUser(int? userId, UserViewModel uvm, out User existingUser) {
			existingUser = null;
			if (userId.HasValue) {
				// Look up the corresponding client record and check the name against it.
				existingUser = new UserDalc().GetUser(userId.Value);
				if (existingUser != null &&
					existingUser.FirstName == uvm.User.FirstName &&
					(existingUser.MiddleInitial ?? "") == (uvm.User.MiddleInitial ?? "") &&
					existingUser.LastNameOrCompany == uvm.User.LastNameOrCompany &&
					existingUser.Addresses.Any(a => a.Equals(uvm.User.Addresses.First()))) {

					// Assume it's the same guy and associate with the existing record.
					return true;
				}
			}
			return false;
		}

	}
}