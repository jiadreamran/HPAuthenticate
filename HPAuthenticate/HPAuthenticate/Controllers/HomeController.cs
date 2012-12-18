using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPAuthenticate.Helpers;
using HPEntities.Dalcs;
using libMatt.Converters;
using HPAuthenticate.Security;

namespace HPAuthenticate.Controllers {
	public class HomeController : ApplicationController {
		[AllowAnonymous]
		public ActionResult Index() {
			if (SessionController.GetCurrentUser(HttpContext) != null) {
				return RedirectToAction("Details", "User");
			} 
			return RedirectToAction("Help", "Home");
		}

		[AllowAnonymous]
		public ActionResult About() {
			return View();
		}


		[AllowAnonymous]
		public ActionResult Help() {
			return View();
		}

	}
}
