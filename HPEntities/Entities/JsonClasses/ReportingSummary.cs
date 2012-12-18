using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Dalcs;
using HPEntities.Helpers;
using System.Configuration;
using libMatt.Converters;

namespace HPEntities.Entities.JsonClasses {
	/// <summary>
	/// Represents the data object used on the Reporting Summary page.
	/// This object is designed to be serialized to JSON and stored as
	/// a snapshot in the database to save state/final submissions when
	/// the user reports usage.
	/// </summary>
	public class ReportingSummary {

		#region Constructors

		public ReportingSummary() {
			this.years = new Dictionary<int, JsonReportingYear>();
            //var uomd = new MeterDalc().GetUnitOfMeasurementDefinitions();
			this.unitDefinitions = new MeterDalc().GetUnitOfMeasurementDefinitions();
			this.loadErrors = new List<string>();
			this.cafoLookups = new Dictionary<int, JsonCafoLookup>();
		}

		#endregion

		public int version { get; set; }
		public Dictionary<string, ReportingValidationError> errorResponses = new Dictionary<string, ReportingValidationError>() {
			{
				"missing_meter", new ReportingValidationError() { 
									message = "Well #{wellNumber} is within the contiguous area but is not associated with a meter.",
									responses = new ReportingValidationErrorResponse[] {
										new ReportingValidationErrorResponse(
											"The well should be associated with a meter.", 
											"Please use the reporting tool to correct the meter/well association."
										),
										new ReportingValidationErrorResponse(
											"The well should not fall inside the contiguous acres (the area was drawn wrong or the well location is wrong).",
											"Please contact HPWD to correct the contiguous acres or well location before you complete the annual reporting."
										),
										new ReportingValidationErrorResponse(
											"This well is associated with an animal feeding operation.",
											"",
											"cafo"
										),
										new ReportingValidationErrorResponse(
											"The well is not being used.",
											"You have indicated that the well is inactive."
										),
									}
								}
			},
			{
				"well_outside_contig_acres", new ReportingValidationError() {
									message = "Well #{wellNumber} is attached to meter #{meterInstallationId} but does not fall inside the contiguous acres.",
									responses = new ReportingValidationErrorResponse[] {
										new ReportingValidationErrorResponse(
											"The well is incorrectly associated with the meter.",
											"Please use the reporting tool to correct the meter/well association."
										),
										new ReportingValidationErrorResponse(
											"The well should fall inside the contiguous acres (the area was drawn inaccurately or the well location is wrong).",
											"Please contact HPWD to correct the contiguous acres or well location before you complete the annual reporting."
										)
									}
								}
			}
		};

		/// <summary>
		/// Stores any errors encountered while loading the state
		/// </summary>
		public List<string> loadErrors { get; set; }


		public Dictionary<int, JsonMeterInstallation> meterInstallations { get; set; }

		public Dictionary<int, string> unitDefinitions { get; set; }

		public bool isReportingAllowed { get; set; }
		public int currentReportingYear { get; set; }
		
		public Dictionary<int, JsonReportingYear> years { get; set; }

		public bool adminOverride { get; set; }

		public Dictionary<int, JsonCafoLookup> cafoLookups { get; set; }

		
	}
}
