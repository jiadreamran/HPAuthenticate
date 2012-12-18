using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using libMatt.Converters;
using SurveyApp.Models;

namespace SurveyApp.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
			ViewBag.Title = "HPWD Irrigation Duration Survey";
			ViewBag.Completed = false;
			var m = new SurveyResponse();
			return View(m);
        }

		[HttpPost]
		public ActionResult Index(SurveyResponse response) {
			ViewBag.Title = "HPWD Irrigation Duration Survey";

			// Validate entries. Required fields: firstname, lastname, avg days per season.
			// If any of these are not present (or 0), return to the form.
			if (ModelState.IsValid) {
				new MiscDalc().SaveSurveyResponse(
					Request.ServerVariables["REMOTE_ADDR"].GetString(),
					response.FirstName,
					response.LastName,
					response.County,
					response.EstimatedThickness,
					response.CropType,
					response.AvgIrrigationDays.GetValueOrDefault(),
					response.NumberYears
				);
				ViewBag.Completed = true;
			} else {
				ViewBag.Completed = false;
			}
			return View(response);
		}

    }
}
