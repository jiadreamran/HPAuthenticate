using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Converters;
using HPEntities.Entities;
using HPEntities.Exceptions;
using System.Data;


namespace HPEntities.Dalcs {

	/// <summary>
	/// This dalc smooths over silly forced abstractions, things
	/// like storing a user's physical city as an ID to the "Cities"
	/// table rather than just storing the city name.
	/// </summary>
	public class SillyAbstractionDalc: AuthDalcBase {


		public IEnumerable<State> GetAllStates() {
			return (from row in ExecuteDataTable("select StateID, State, StateCode from States;").AsEnumerable()
					select new State(
								row["StateID"].ToInteger(),
								row["State"].GetString(),
								row["StateCode"].GetString()
							));
		}

		/// <summary>
		/// Returns a State object matching the given string by checking the string
		/// against known state names and abbreviations.
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		public State GetState(string state) {
			var dt = ExecuteDataTable(@"
select
	StateID,
	State,
	StateCode
from States
where
	State like @state
	or StateCode like @state;", new Param("@state", state));
			if (dt.Rows.Count == 0)
				return null;
			return new State(dt.Rows[0]["StateID"].ToInteger(),
							dt.Rows[0]["State"].GetString(),
							dt.Rows[0]["StateCode"].GetString());
		}

		/// <summary>
		/// Returns a city ID, creating a new one if necessary.
		/// City name is matched to existing values case-insensitively.
		/// </summary>
		/// <param name="cityName"></param>
		/// <returns></returns>
		public int GetCityId(string cityName, string state) {
			int id = ExecuteScalar(@"
select CityID
from Cities
where
	City like @name;", new Param("@name", cityName)).ToInteger();

			if (id > 0)
				return id;

			var s = GetState(state);
			if (s == null) {
				throw new ValidationException("Invalid state: '" + state);
			}

			return ExecuteScalar(@"
insert into Cities (
	City,
	StateID
) values (
	@city,
	@stateId
);

select @@IDENTITY;", new Param("@city", cityName),
				   new Param("@stateId", s.Id)).ToInteger();

		}

		/// <summary>
		/// Looks up the city/state name corresponding to the given city ID.
		/// </summary>
		/// <param name="p"></param>
		/// <param name="_city"></param>
		/// <param name="_state"></param>
		internal void GetCityState(int cityId, out string city, out string state, out string stateCode) {
			city = "";
			state = "";
			stateCode = "";
			var dt = ExecuteDataTable(@"
select
	c.City,
	s.State,
	s.StateCode
from Cities c
inner join States s on
	s.StateID = c.StateID
where
	c.CityID = @id;", new Param("@id", cityId));
			if (dt.Rows.Count > 0) {
				city = dt.Rows[0]["City"].GetString();
				state = dt.Rows[0]["State"].GetString();
				stateCode = dt.Rows[0]["StateCode"].GetString();
			}
		}

		/// <summary>
		/// Note that this function treats ID as string for serialization purposes;
		/// the JSON serializer doesn't like to serialize dictionaries where the key 
		/// is not a string or object, for whatever reason.
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, string> GetPhoneTypes() {
			return (from row in ExecuteDataTable("select PhoneTypeID, PhoneType from PhoneTypes;").AsEnumerable()
					select new {
						id = row["PhoneTypeID"].GetString(),
						desc = row["PhoneType"].GetString()
					}).ToDictionary(
						x => x.id,
						x => x.desc
					);
		}



	}
}
