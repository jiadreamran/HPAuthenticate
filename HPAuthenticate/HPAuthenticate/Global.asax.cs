using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using HPAuthenticate.Security;
using HPAuthenticate.Controllers;
using HPAuthenticate.Helpers;

namespace HPAuthenticate {
	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication {
		public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
			filters.Add(new HandleErrorAttribute());
			filters.Add(new LoginAuthorizeAttribute());
		}

		public static void RegisterRoutes(RouteCollection routes) {
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			routes.MapRoute(
				"Login",
				"Login",
				new { controller = "Session", action = "Login" }
			);

			routes.MapRoute(
				"Help",
				"Help",
				new { controller = "Home", action = "Help" }
			);

			routes.MapRoute(
				"Default", // Route name
				"{controller}/{action}/{id}", // URL with parameters
				new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
			);



		}

		protected void Application_Start() {
			AreaRegistration.RegisterAllAreas();

			RegisterGlobalFilters(GlobalFilters.Filters);
			RegisterRoutes(RouteTable.Routes);
		}


		protected void Application_Error() {
			var exception = Server.GetLastError();
			var httpException = exception as HttpException;
			Response.Clear();
			Server.ClearError();
			var routeData = new RouteData();
			routeData.Values["controller"] = "Errors";
			routeData.Values["action"] = "General";
			routeData.Values["exception"] = exception;
			Response.StatusCode = 500;
			bool logException = true;
			if (httpException != null) {
				Response.StatusCode = httpException.GetHttpCode();
				switch (Response.StatusCode) {
					case 403:
						routeData.Values["action"] = "Http403";
						logException = false;
						break;
					case 404:
						routeData.Values["action"] = "Http404";
						logException = false;
						break;
				}
			}
			if (logException) {
				Logger.LogError(exception);
			}

			IController errorsController = new ErrorsController();
			var rc = new RequestContext(new HttpContextWrapper(Context), routeData);
			errorsController.Execute(rc);
		}
		
	}
}