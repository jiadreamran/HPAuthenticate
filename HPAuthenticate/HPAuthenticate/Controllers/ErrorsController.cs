using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPAuthenticate.Helpers;

namespace HPAuthenticate.Controllers
{
    public class ErrorsController : ApplicationController
    {
		public ErrorsController() {
			ViewBag.ExceptionContent = "";
		}

        public ActionResult General(Exception exception) {
#if DEBUG
			ViewBag.ExceptionContent = Logger.FormatExceptionHtml(exception);
#endif
            return View();
        }

		public ActionResult Http404() {
			return Content("Not found", "text/plain");
		}

		public ActionResult Http403() {
			return Content("Forbidden", "text/plain");
		}

    }
}
