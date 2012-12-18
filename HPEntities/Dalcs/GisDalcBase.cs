using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Data;
using System.Data;
using System.Configuration;

namespace HPEntities.Dalcs {
	public abstract class GisDalcBase: Dalc {


			public GisDalcBase() : base(new SqlDataProvider(GisDalcBase.ConnStr)) { }
			public GisDalcBase(IDbTransaction trans) : base(trans) { }


			private static string _connStr = null;
			private static string ConnStr {
				get {
					if (string.IsNullOrWhiteSpace(_connStr)) {
						// Attempt to load it up from config.
						var env = "gis_" + ConfigurationManager.AppSettings["environment"];
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
