using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.ComponentModel.DataAnnotations;

namespace SurveyApp.Models {
	public class SurveyResponse {

		static SurveyResponse() {
			SurveyResponse.AvailableCounties = new MiscDalc().GetCounties().Select(x => new SelectListItem() { Text = x });
		}

		[Required(ErrorMessage = "Please enter your first name.")]
		public string FirstName { get; set; }
		[Required(ErrorMessage = "Please enter your last name.")]
		public string LastName { get; set; }
		public string County { get; set; }
		public int? EstimatedThickness { get; set; }
		public string CropType { get; set; }
		[Required(ErrorMessage = "Please enter your average number of irrigation days per season.")]
		[Range(1, 365)]
		public int? AvgIrrigationDays { get; set; }
		public int? NumberYears { get; set; }

		public static IEnumerable<SelectListItem> AvailableCounties { get; set; }

	}
}