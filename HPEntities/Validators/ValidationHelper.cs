using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HPEntities.Validators {
	public static class ValidationHelper {

		public static bool IsValidEmail(string email) {
			return Regex.IsMatch(email, @"^[\w+\-.]+@[a-z\d\-.]+\.[a-z]+$");
		}

		public static bool IsValidPassword(string password, string passwordConfirmation, out List<string> errors) {
			errors = new List<string>();
			if (password.Length < 8) {
				errors.Add("Password is too short. Please make it at least 8 characters long.");
			}
			if (password != passwordConfirmation) {
				errors.Add("Passwords do not match.");
			}
			return errors.Count == 0;
		}

	}
}
