using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using libMatt.Converters;
using HPEntities.Dalcs;

namespace HPEntities.Helpers {
	public class Config {

		private static Config _instance = new Config();

		private Config() { }

		public static Config Instance { get { return _instance; } }


		public bool RequireAccountConfirmation {
			get {
				var requireConfirmationSetting = ConfigurationManager.AppSettings["require_account_confirmation"];
				return (string.IsNullOrEmpty(requireConfirmationSetting) || requireConfirmationSetting.ToBoolean());
			}
		}

		/// <summary>
		/// Returns the length of time a password confirmation code is permitted to stay alive.
		/// Requests beyond the expiration of the code should not be honored.
		/// </summary>
		public TimeSpan PasswordConfirmationCodeDuration {
			get {
				return TimeSpan.FromDays(3);
			}
		}

		/// <summary>
		/// Returns the value of "autocompleter_result_limit" in the web.config, or 30 by default.
		/// </summary>
		public int AutocompleterResultLimit {
			get {
				int i;
				if (int.TryParse(ConfigurationManager.AppSettings["autocompleter_result_limit"], out i)) {
					return i;
				}
				return 30;
			}
		}

		/// <summary>
		/// Indicates whether CAFO reporting enhancements have been deployed.
		/// This config parameter is present due to the forked nature of CAFO
		/// development.
		/// 
		/// TODO: Remove this after both Reporting Summary and CAFO enhancements
		/// have been deployed and merged.
		/// </summary>
		public static bool IsCafoDeployed {
			get {
				return ConfigurationManager.AppSettings["is_cafo_deployed"].ToBoolean();
			}
		}

		/// <summary>
		/// Formats a database search string based on the current web.config-specified
		/// preference to either search substrings or leftmost matches only.
		/// </summary>
		/// <param name="searchTerm"></param>
		/// <returns></returns>
		public string FormatStringSearchTerm(string searchTerm) {
			if (ConfigurationManager.AppSettings["use_substring_searches"].ToBoolean()) {
				return "%" + searchTerm + "%";
			}

			return searchTerm + "%";
		}

		protected Dictionary<string, int> _unitsByName = null;
		/// <summary>
		/// Returns the unit definitions stored in the database, but keyed by name rather than 
		/// by ID. This depends on no two units having the same name, but really no two units
		/// ever should in our case. (How would we tell them apart without additional data anyway?)
		/// 
		/// All keys in this dictionary are lowercased.
		/// </summary>
		protected Dictionary<string, int> UnitsByName {
			get { 
				if (_unitsByName == null) {
					_unitsByName = new MeterDalc().GetUnitOfMeasurementDefinitions().ToDictionary(x => x.Value.ToLower().Trim(), x => x.Key);
				}
				return _unitsByName;
			}
		}

		public double ConvertVolume(double value, string fromUnit, string toUnit) {
			int fromUnitId, toUnitId;
			// Attempt to look up unit both by ID and by name
			if (!int.TryParse(fromUnit, out fromUnitId) && !UnitsByName.TryGetValue(fromUnit.ToLower(), out fromUnitId)) {
				throw new ArgumentException("No corresponding unit definition found for unit: '" + fromUnit + "'");
			}
			if (!int.TryParse(toUnit, out toUnitId) && !UnitsByName.TryGetValue(toUnit.ToLower(), out toUnitId)) {
				throw new ArgumentException("No corresponding unit definition found for unit: '" + toUnit + "'");
			}

			if (fromUnitId == toUnitId) {
				return value;
			}

			int gallonUnitId;
			if (!UnitsByName.TryGetValue("gallons", out gallonUnitId)) {
				throw new ConfigurationException("Unable to find unit ID for standard unit 'gallons'!");
			}

			if (fromUnitId != gallonUnitId) {
				// First convert to gallons, our standard unit, then convert to whatever else is desired.
				value *= new ConfigDalc().GetConversionFactor(fromUnitId, gallonUnitId);
			}

			return value * new ConfigDalc().GetConversionFactor(gallonUnitId, toUnitId);
		}


	}
}