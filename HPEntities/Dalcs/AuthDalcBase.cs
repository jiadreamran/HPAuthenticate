using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using libMatt.Data;
using System.Configuration;
using System.Data;

namespace HPEntities.Dalcs {
	public abstract class AuthDalcBase: Dalc {

		public AuthDalcBase(): base(new SqlDataProvider(AuthDalcBase.ConnStr)) { }
		public AuthDalcBase(IDbTransaction trans) : base(trans) { }


		private static string _connStr = null;
		private static string ConnStr {
			get {
				if (string.IsNullOrWhiteSpace(_connStr)) {
					// Attempt to load it up from config.
					var env = ConfigurationManager.AppSettings["environment"];
					var connStrSettings = ConfigurationManager.ConnectionStrings[env];
					if (connStrSettings == null) {
						throw new ConfigurationException(string.Format("Missing required connection string for environment '{0}'. Please update the web.config file to add this connection string, or adjust the environment appSetting to an environment that has a connection string defined.", env));
					}
					_connStr = connStrSettings.ConnectionString;
				}
				return _connStr;
			}
		}

		

	}
}