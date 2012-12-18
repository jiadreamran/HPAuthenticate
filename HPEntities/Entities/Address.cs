using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using HPEntities.Dalcs;

namespace HPEntities.Entities {
	public class Address {

		public int Id { get; set; }

		[Required(ErrorMessage = "Address cannot be blank.")]
		public string MailingAddress { get; set; }

		[Required(ErrorMessage = "Please provide a valid city.")]
		public int CityId { get; set; }

		private string _city, _state, _stateCode;
		private void PopulateCityState() {
			string c, s, scode;
			new SillyAbstractionDalc().GetCityState(this.CityId, out c, out s, out scode);
			if (string.IsNullOrEmpty(_city)) {
				_city = c;
			}
			if (string.IsNullOrEmpty(_state) || string.IsNullOrEmpty(_stateCode)) {
				_state = s;
				_stateCode = scode;
			}
		}

		public string City {
			get {
				if (string.IsNullOrEmpty(_city)) {
					PopulateCityState();
				}
				return _city;
			}
			set {
				_city = value;
			}
		}

		/// <summary>
		/// Attempts to set the state by looking up the name or abbreviation.
		/// </summary>
		/// <param name="state"></param>
		private void SetStateAmbiguously(string state) {
			var s = new SillyAbstractionDalc().GetState(state);
			if (s != null) {
				_state = s.Name;
				_stateCode = s.Abbreviation;
			} else {
				_state = "";
				_stateCode = "";
			}
		}

		public string State {
			get {
				if (string.IsNullOrEmpty(_state)) {
					PopulateCityState();
				}
				return _state;
			}
			set {
				SetStateAmbiguously(value);
			}
		}

		[Required(ErrorMessage = "State cannot be blank.")]
		public string StateCode {
			get {
				if (string.IsNullOrEmpty(_stateCode)) {
					PopulateCityState();
				}
				return _stateCode;
			}
			set {
				SetStateAmbiguously(value);
			}
		}

		[RegularExpression(@"^\d{5}(-?\d{4})?$", ErrorMessage = "Please provide a valid postal code.")]
		[Display(Name = "Postal Code")]
		public string Zip { get; set; }


		public override int GetHashCode() {
			int hash = 17;
			unchecked {
				hash *= 23 + Id.GetHashCode();
				hash *= 23 + MailingAddress.GetHashCode();
				hash *= 23 + CityId.GetHashCode();
				hash *= 23 + City.GetHashCode();
				hash *= 23 + State.GetHashCode();
				hash *= 23 + StateCode.GetHashCode();
				hash *= 23 + Zip.GetHashCode();
				return hash;
			}
		}

		public override bool Equals(object obj) {
			var x = obj as Address;
			if (x == null) {
				return false;
			}
			// Consider addresses equal if the city/state/mailingAddress/zip matches.
			return (this.State == x.State &&
					this.City == x.City &&
					this.MailingAddress == x.MailingAddress &&
					this.Zip == x.Zip);
		}

	}
}
