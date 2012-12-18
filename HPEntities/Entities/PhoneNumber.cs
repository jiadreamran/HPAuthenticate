using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;


namespace HPEntities.Entities {
	public class PhoneNumber {

		public PhoneNumber(string number, string extension) : this(-1, number, extension) { }

		public PhoneNumber(int phoneTypeId, string number, string extension) :
			this(new int?(), phoneTypeId, number, extension) { }


		public PhoneNumber(int? id, int phoneTypeId, string number, string extension) {
			this.PhoneTypeId = phoneTypeId;
			this.Number = Regex.Replace(number, "[^0-9]", "");
			this.Extension = Regex.Replace(extension, "[^0-9]", "");
			this.Id = id;
		}

		public int? Id { get; set; }

		/// <summary>
		/// This corresponds to a "PhoneTypeID" read from the "PhonyType" table of the database.
		/// We need neither know nor care about what this value really is.
		/// </summary>
		public int PhoneTypeId { get; set; }

		// This can't be longer than 10 characters, according to the database.
		[StringLength(10, ErrorMessage = "Phone numbers cannot be longer than 10 numbers.")]
		[CustomValidation(typeof(PhoneNumber), "ValidatePhoneNumber")]
		public string Number { get; set; }

		// This can't be longer than 5 characters, according to the database.
		[StringLength(5, ErrorMessage = "Phone number extensions cannot be longer than 5 characters.")]
		public string Extension { get; set; }


		public static ValidationResult ValidatePhoneNumber(object value) {
			if (value == null) {
				return new ValidationResult("Phone number cannot be blank.");
			}
			if (Regex.IsMatch(Regex.Replace(value.ToString(), "[^0-9]", ""), @"^\d{10}$")) {
				return ValidationResult.Success;
			} else {
				return new ValidationResult("Please provide a valid phone number.");
			}
		}

		public override string ToString() {
			return this.Number + (string.IsNullOrEmpty(this.Extension) ? "" : " x" + this.Extension);
		}


	}
}
