using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Configuration;
using System.Web.Mvc;

namespace HPAuthenticate.Helpers {

	//////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////
	///// WARNING
	///// I removed the version info from this file because IIS 7 URL
	///// rewrites were not working when the app was set up as a child
	///// of data.hpwd.com. DO NOT COPY THIS FILE TO OTHER PROJECTS.
	//////////////////////////////////////////////////////////////////
	//////////////////////////////////////////////////////////////////


	/// <summary>
	/// Contains methods to extend the MVC UrlHelper class.
	/// </summary>
	public static class UrlHelperExtensions {
		private readonly static string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

		private static string GetAssetsRoot() {
			string root = ConfigurationManager.AppSettings["AssetsRoot"];
			return string.IsNullOrEmpty(root) ? "~" : root;
		}

		/// <summary>
		/// Provides an autoversioned filename for the specified image file, based on the assembly version.
		/// 
		/// Important: This requires IIS rewrite rules to work; see http://stackoverflow.com/questions/4841455/auto-versioning-in-asp-net-mvc-for-css-js-files
		/// </summary>
		/// <param name="helper"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string Image(this UrlHelper helper, string fileName) { 
			return helper.Content(string.Format("{0}/assets/images/{1}", GetAssetsRoot(), fileName, _version));
		}

		/// <summary>
		/// Provides an autoversioned filename for the specified asset, based on the assembly version.
		/// 
		/// Important: This requires IIS rewrite rules to work; see http://stackoverflow.com/questions/4841455/auto-versioning-in-asp-net-mvc-for-css-js-files
		/// </summary>
		/// <param name="helper"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string Asset(this UrlHelper helper, string fileName) {
			return helper.Content(string.Format("{0}/assets/{1}", GetAssetsRoot(), fileName, _version));
		}

		/// <summary>
		/// Provides an autoversioned filename for the specified stylesheet, based on the assembly version.
		/// 
		/// Important: This requires IIS rewrite rules to work; see http://stackoverflow.com/questions/4841455/auto-versioning-in-asp-net-mvc-for-css-js-files
		/// </summary>
		/// <param name="helper"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string Stylesheet(this UrlHelper helper, string fileName) {
			return helper.Content(string.Format("{0}/assets/css/{1}", GetAssetsRoot(), fileName, _version));
		}

		/// <summary>
		/// Provides an autoversioned filename for the specified script, based on the assembly version.
		/// 
		/// Important: This requires IIS rewrite rules to work; see http://stackoverflow.com/questions/4841455/auto-versioning-in-asp-net-mvc-for-css-js-files
		/// </summary>
		/// <param name="helper"></param>
		/// <param name="fileName"></param>
		/// <returns></returns>
		public static string Script(this UrlHelper helper, string fileName) {
			return helper.Content(string.Format("{0}/assets/js/{1}", GetAssetsRoot(), fileName, _version));
		}
	}
}