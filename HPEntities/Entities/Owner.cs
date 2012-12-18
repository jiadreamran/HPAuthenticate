using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities;

namespace HPEntities {

	public class Owner {
		
		public Owner(string name) : this(name, new List<string>()) { }

		public Owner(string name, IEnumerable<string> propertyDescriptions){
			this.Name = name;
			this.PropertyDescriptions = (propertyDescriptions ?? new List<string>()).ToList();
		}
		public string Name { get; set; }
		public string MailingAddress { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string PostalCode { get; set; }
		public string PhoneNumber { get; set; }


		public string FullAddress {
			get {
				return MailingAddress + ", " + City + ", " + State + ", " + PostalCode;
			}
		}

		public List<string> PropertyDescriptions { get; set; }

		/// <summary>
		/// Returns true if all the owner's fields match, except that this
		/// equality check ignores property descriptions.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public bool EqualsExceptPropertyDescriptions(object obj) {
			var other = obj as Owner;
			if (other == null) {
				return false;
			}
			return this.Name == other.Name
				&& this.MailingAddress == other.MailingAddress
				&& this.City == other.City
				&& this.State == other.State
				&& this.PostalCode == other.PostalCode
				&& this.PhoneNumber == other.PhoneNumber;

		}

		public override bool Equals(object obj) {
			return EqualsExceptPropertyDescriptions(obj)
				&& this.PropertyDescriptions.SequenceEqual(((Owner)obj).PropertyDescriptions);
		}

		public override int GetHashCode() {
			unchecked // Overflow is fine, just wrap
			{
				int hash = 17;
				hash = hash * 23 + Name.GetHashCode();
				hash = hash * 23 + MailingAddress.GetHashCode();
				hash = hash * 23 + City.GetHashCode();
				hash = hash * 23 + State.GetHashCode();
				hash = hash * 23 + PostalCode.GetHashCode();
				hash = hash * 23 + PhoneNumber.GetHashCode();
				hash = hash * 23 + PropertyDescriptions.GetHashCode();

				return hash;
			}			
		}

	}

}
