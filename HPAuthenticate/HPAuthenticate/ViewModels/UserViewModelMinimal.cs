using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities;
using HPEntities.Entities;

namespace HPAuthenticate.ViewModels {
	/// <summary>
	/// Presents only public information, suitable for Ajax requests.
	/// This viewmodel does not persist any data back to its represented User object,
	/// but beware changing references (such as phone numbers).
	/// </summary>
	public class UserViewModelMinimal {

		public UserViewModelMinimal(User user) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			this._user = user;
		}

		private User _user;
		private Address FirstAddress { get { return _user.Addresses.DefaultIfEmpty(new Address()).First(); } }

		public int UserId { get { return _user.Id; } }
		public int? CourtesyTitleId { get { return _user.CourtesyTitleId; } }
		public string DisplayName { get { return _user.DisplayName; } }
		public string FirstName { get { return _user.FirstName; } }
		public string MiddleInitial { get { return _user.MiddleInitial; } }
		public string LastName { get { return _user.LastNameOrCompany; } }
		public string PreferredName { get { return _user.PreferredName; } }

		public string Address { get { return FirstAddress.MailingAddress; } }
		public string City { get { return FirstAddress.City; } }
		public string State { get { return FirstAddress.StateCode; } }
		public string PostalCode { get { return FirstAddress.Zip; } }

		public string Email { get { return _user.Email; } }
		public bool IsAdmin { get { return _user.IsAdmin; } }
		public bool IsAdminCandidate {
			get { return Email.EndsWith("@hpwd.com") || Email.EndsWith("@intera.com"); }
		}

		public string DisplayAddress {
			get {
				return String.Join(", ", new[] { Address, City, State }.Where(s => !string.IsNullOrWhiteSpace(s)));
			}
		}

		public List<PhoneNumber> PhoneNumbers { get { return _user.PhoneNumbers; } }
	}
}
