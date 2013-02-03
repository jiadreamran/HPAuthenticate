using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPEntities;
using Newtonsoft.Json.Linq;
using HPEntities.Dalcs;
using HPEntities.Entities.JsonClasses;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using libMatt.Formatters;
using HPEntities.Entities;
using libMatt.Converters;
using System.Configuration;
using System.Web.Routing;
using HPEntities.Exceptions;
using libMatt.Linq;
using HPAuthenticate.Helpers;

namespace HPAuthenticate.Controllers {
	public class ReportingController : ApplicationController {

		public class ReportingViewModel {
			public string pageState { get; set; }
			/// <summary>
			/// Dictionary&lt;int, Dictionary&lt;int, double&gt;&gt;
			/// { fromUnitId: { toUnitId: factor, toUnitId: factor, ... } }
			/// 
			/// </summary>
			public string unitConversionFactors;

			/// <summary>
			/// { CountyID: { 'mcf':1.123456, 'kwh':0.02235 }, ... }
			/// </summary>
			public string meterUnitConversionFactors;
		}

		public class DeploymentFilter : ActionFilterAttribute {
			public override void OnActionExecuting(ActionExecutingContext filterContext) {
				if (!ConfigurationManager.AppSettings["is_reporting_deployed"].ToBoolean()) {
					filterContext.Result = new RedirectToRouteResult(
						new RouteValueDictionary { { "Controller", "User" }, { "Action", "Details" } }
					);
				}
				base.OnActionExecuting(filterContext);
			}
		}



		protected int CurrentReportingYear = DateTime.Now.Year;
		protected bool IsReportingAllowed = false;


		public ReportingController() {
			var currentDate = DateTime.Now;

			// Only allow reporting from 15 Dec - 15 Mar, and if 
			// current date is in Jan-Mar, the reporting year is "last" year.
			if ((currentDate.Month == 12 && currentDate.Day >= 15)
				|| (currentDate.Month < 3)
				|| (currentDate.Month == 3 && currentDate.Day <= 1)) {
				IsReportingAllowed = true;
				CurrentReportingYear = (currentDate.Month > 3) ? currentDate.Year : currentDate.Year - 1;
			} else {
				IsReportingAllowed = false;
			}

		}


		#region CA object creation, persistence, "cache invalidation"

		/// <summary>
		/// This method populates all properties that should be refreshed on every page
		/// load, i.e. not using existing stored JSON state.
		/// </summary>
		/// <param name="ca"></param>
		/// <returns></returns>
		protected JsonContiguousAcres PopulateNonPersistentProperties(JsonContiguousAcres ca) {
			throw new NotImplementedException();
		}

		#endregion


		/// <summary>
		/// Creates a dynamic object representing the page state.
		/// This method will attempt to load and deserialize JSON
		/// from the database if it exists for this user (updating
		/// relevant properties appropriately), and if no existing
		/// JSON exists will construct a new object with which the
		/// user can report water usage.
		/// </summary>
		/// <returns></returns>
		protected dynamic GetPageStateJson(User user) {
			// Only update the _current_ reporting year or unsubmitted 
			var pageStateJson = new ReportingDalc().GetReportingSummary(user.IsAdmin ? (user.ActingAsUserId ?? user.Id) : user.Id);
			

			ReportingSummary pageState = null;
			try {
				pageState = JsonConvert.DeserializeObject<ReportingSummary>(pageStateJson);
			} catch (Exception ex) {
				// There was a problem deserializing.
				// TODO: Notify user, possibly admins!
				throw;
			}
			if (pageState == null) {
				pageState = new ReportingSummary();
			}

			/// Returns the volume (Item1) and a bool indicating whether rollover occurred (Item2)
			/// Args: meterInstallationId, meterreadings
			Func<int, IEnumerable<JsonMeterReading>, Tuple<int, bool>> calculateVolume = (meterInstallationId, readings) => {
				double runningTotal = 0d;
				if (readings.Count() == 0) {
					return new Tuple<int, bool>((int)runningTotal, false);
				}
				bool rolledOver = false;
				var prevReading = readings.First();
				foreach (var currentReading in readings.Skip(1)) {
					double delta = 0;
					if (currentReading.reading.GetValueOrDefault() < prevReading.reading.GetValueOrDefault()) {
						// Rollover occurred
						rolledOver = true;
						// Retrieve the rollover value for this meter installation
						var rollover = new MeterDalc().GetMeterInstallations(meterInstallationId).First().RolloverValue;
						delta = (rollover - prevReading.reading.Value) + currentReading.reading.Value;
					} else {
						delta = currentReading.reading.GetValueOrDefault() - prevReading.reading.GetValueOrDefault();
					}

					runningTotal += delta * (prevReading.rate.HasValue ? prevReading.rate.Value : 1);

					prevReading = currentReading;
				}

				return new Tuple<int, bool>((int)runningTotal, rolledOver);
			};

			// Only show Approved CAs unless the user is an admin
			var contigAcres = new GisDalc().GetContiguousAcres(user, true).Where(ca => user.IsAdmin || ca.IsApproved);


			// Key: CA ID, Value: well ids
			var wellIds = new Dictionary<int, HashSet<int>>();
			Dictionary<int, ContiguousAcres> validAcres = new Dictionary<int, ContiguousAcres>();

			// There may be load errors that got persisted from a previous page load, but we
			// only want to show current ones so clear the list.
			pageState.loadErrors.Clear();

			if (contigAcres.Count() > 0) { // Only create the service if contigAcres has stuff, because SOAP takes time
				var service = new GisServiceSoapClient();
				foreach (var ca in contigAcres) {
                    string result = "";
                    // This call is prone to failure whenever the GIS service is down.
                    try {
                        result = service.GetWellIDsByCA(ca.OBJECTID);
                    } catch (Exception ex) {
                        pageState.loadErrors.Add("Unable to load well associations for any CAs: the GIS service appears to be down!");
                    }
					try {
						int[] ids = JsonConvert.DeserializeObject<int[]>(result.Trim('{', '}'));
						wellIds[ca.caID] = new HashSet<int>(ids);
						validAcres[ca.caID] = ca;
					} catch (JsonException ex) {
						pageState.loadErrors.Add("Error loading CA ID " + ca.caID + ": " + ex.Message + " Service response: " + result);
					}

				}
			}

			var mdalc = new MeterDalc();
			var rptgDalc = new ReportingDalc();

			var wellDalc = new WellDalc();
			var wells = wellDalc.GetWells(wellIds.SelectMany(x => x.Value).ToArray());
			var meterInstallations = mdalc.GetMeterInstallations(wells.SelectMany(x => x.MeterInstallationIds).ToArray()).GroupBy(x => x.Id).Select(x => x.First()).ToDictionary(x => x.Id, x => x);
			// In addition to the wells retrieved by the spatial query, we also need to
			// retrieve wells attached to the meters on that initial set.
			wells = wells.Concat(wellDalc.GetWellsByMeterInstallationIds(meterInstallations.Select(x => x.Key).ToArray()))
							.Distinct((a,b) => a.WellId == b.WellId);
						

			Func<int, Dictionary<int, JsonBankedWaterRecord>> getBankedWaterTable = caId => {
				return rptgDalc.GetHistoricalBankedWater(caId).ToDictionary(x => x.year, x => x);
			};

			// func to populate a JsonCA in the current reporting year
			Func<ContiguousAcres, JsonContiguousAcres> createNewJsonCA = ca => {
				ca.Wells = wellDalc.GetWells(wellIds[ca.caID].ToArray()).ToArray();
				var reportedVolumes = mdalc.GetReportedVolumeGallons(ca.Wells.SelectMany(w => w.MeterInstallationIds), ca.caID, CurrentReportingYear);
				JsonContiguousAcres ret = new JsonContiguousAcres(ca, CurrentReportingYear);
				Tuple<int, int> rvol;
				ret.meterReadings = new Dictionary<int, JsonMeterReadingContainer>();
				MeterInstallation meter = null;

				foreach (var miid in ret.wells.SelectMany(x => x.meterInstallationIds).Distinct()) {
					meterInstallations.TryGetValue(miid, out meter);
					var container = new JsonMeterReadingContainer() {
						meterInstallationId = miid,
						calculatedVolume = reportedVolumes.TryGetValue(miid, out rvol) ? rvol.Item1 : 0,
						acceptCalculation = rvol == null || rvol.Item2 == 0,
						userRevisedVolume = rvol != null ? rvol.Item2 : 0,
						totalVolumeAcreInches = 0,
						isNozzlePackage = meter != null ? meter.MeterType.Description().ToLower() == "nozzle package" : false,
                        isElectric = meter != null ? meter.MeterType.Description().ToLower() == "electric" : false,
                        isNaturalGas =  meter != null ? meter.MeterType.Description().ToLower() == "natural gas" : false,
                        isThirdParty = meter != null ? meter.MeterType.Description().ToLower() == "bizarro-meter" : false,
                        depthToRedbed = -99999,
                        //squeezeValue = 0,
						unitId = meter != null ? meter.UnitId : null,
						county = meter != null ? meter.County : "",
						rolloverValue = meter != null ? meter.RolloverValue : 0,
						countyId = meter != null ? meter.CountyId : -1,
						meterType = meter != null ? meter.MeterType.Description() : "",
						nonStandardUnits = meter != null && meter.MeterType != HPEntities.Entities.Enums.MeterType.standard ? new MeterDalc().GetUnits(meter.MeterType).Description() : "",
						meterMultiplier = meter != null ? meter.Multiplier : 1.0d
					};

                    // Add depth to redbed if the meter is of 1) natural gas or 2) electric
                    if (container.isNaturalGas || container.isElectric)
                    {
                        /* //ned skip this lookup until we really need to implement it
                        var service1 = new GisServiceSoapClient();
                        string depthInString = service1.GetDepthToRedbedByMeter(miid);
                        string depthInString = "Implement this later"
                        double depth = -99999;


                        if (!Double.TryParse(depthInString, out depth))
                            depth = -99999;
                        */
                        double depth = 100;  //take this out when the above works
                        container.depthToRedbed = depth;
                    }

					container.readings = (from mr in mdalc.GetReadings(miid, CurrentReportingYear).Reverse() // By default, they're ordered by reading date desc
										  select new JsonMeterReading() {
											  reading = mr.Reading,
											  rate = mr.Rate,
											  isValidBeginReading = ReportingDalc.IsMeterReadingValidBeginReading(mr, CurrentReportingYear),
											  isValidEndReading = ReportingDalc.IsMeterReadingValidEndReading(mr, CurrentReportingYear)
										  }).ToArray();
					ret.meterReadings[miid] = container;
				}



				ret.meterInstallationErrors = GetMeterInstallationErrors(ca, wellIds[ca.caID]);



				ret.bankedWaterHistory = getBankedWaterTable(ca.caID);
				JsonBankedWaterRecord bankedWaterLastYear;
				ret.annualUsageSummary = new JsonAnnualUsageSummary() {
					contiguousArea = ca.AreaInAcres,
					annualVolume = 0, // this will only be populated after submittal
					allowableApplicationRate = rptgDalc.GetAllowableProductionRate(CurrentReportingYear),
					bankedWaterFromPreviousYear = ret.bankedWaterHistory.TryGetValue(CurrentReportingYear - 1, out bankedWaterLastYear) ? bankedWaterLastYear.bankedInches : 0 
				};

				ret.isSubmitted = rptgDalc.IsSubmitted(ca.caID, CurrentReportingYear);
				ret.ownerClientId = ca.OwnerClientId;
				ret.isCurrentUserOwner = (this.ActualUser.ActingAsUserId ?? this.ActualUser.Id) == ca.OwnerClientId;
				return ret;
			};

			JsonReportingYear year;
			if (pageState.years.TryGetValue(CurrentReportingYear, out year)) {
				// Check the existing year object and update as necessary
				// For now, wholly reload any CAs that have not yet been submitted.

				List<JsonContiguousAcres> removals = year.contiguousAcres.Where(ca => !ca.isSubmitted).ToList();
				HashSet<int> submittedCaIds = new HashSet<int>(year.contiguousAcres.Where(ca => ca.isSubmitted).Select(x => x.number));
				

				// Ensure that all the CAs are still applicable to this account - the CA IDs need
				// to match what's presently associated with the user account, else they're removed.
				foreach (var ca in removals) {
					year.contiguousAcres.Remove(ca);
				}

				// Add any CAs that were missing from the original state
				foreach (var kv in validAcres) {
					if (!submittedCaIds.Contains(kv.Value.caID)) {
						year.contiguousAcres.Add(createNewJsonCA(kv.Value));
					}
					for (int i = 0; i < year.contiguousAcres.Count; i++) {
						if (year.contiguousAcres[i].number == kv.Value.caID) {
							year.contiguousAcres[i].meterInstallationErrors = GetMeterInstallationErrors(kv.Value, wellIds[kv.Value.caID]);
							year.contiguousAcres[i].wells = kv.Value.Wells.Select(w => new JsonWell(w)).ToArray();
						}
					}
					//year.contiguousAcres.Where(jca => jca.number == kv.Value.caID).First().meterInstallationErrors = GetMeterInstallationErrors(kv.Value, wellIds[kv.Value.caID]);
				}

				foreach (var ca in year.contiguousAcres) {
					// This is used on the frontend to decide whether to show the user an editable form.
					// Only CA owners get the editable form.
					ca.isCurrentUserOwner = (this.ActualUser.ActingAsUserId ?? this.ActualUser.Id) == ca.ownerClientId;

					

				}


			} else { // No existing year record exists; create a new one
				year = new JsonReportingYear();
				year.contiguousAcres = validAcres.Values.Select(ca => {
					return createNewJsonCA(ca);
				}).ToList();

				pageState.years[CurrentReportingYear] = year;

			}


			// Populate meterInstallations by retrieving metadata for all used meter installations
			HashSet<int> meterInstallationIds = new HashSet<int>((from y in pageState.years
																  from ca in y.Value.contiguousAcres
																  from w in ca.wells
																  select w.meterInstallationIds).SelectMany(x => x));

			// Converting this directly to a dictionary will fail in the event that a meter
			// is associated with wells in multiple counties. However, that case doesn't really
			// make any sense and would break all sorts of other things (such as meter unit conversion factors
			// that are specific to counties), so we're going to assume that there's always
			// a single county.
			pageState.meterInstallations = mdalc.GetMeterInstallations(meterInstallationIds.ToArray())
												.GroupBy(x => x.Id)
												.Select(mi => new JsonMeterInstallation(mi.First()))
												.ToDictionary(
													x => x.id,
													x => x
												);

			pageState.currentReportingYear = CurrentReportingYear;

			pageState.isReportingAllowed = IsReportingAllowed;
			pageState.adminOverride = ((!IsReportingAllowed) && this.ActualUser.IsAdmin);

			pageState.cafoLookups = rptgDalc.GetCafoLookups();

			// Go through and check all the submittal states of the CAs
			foreach (var ca in pageState.years[CurrentReportingYear].contiguousAcres) {
				ca.isSubmitted = rptgDalc.IsSubmitted(ca.number, CurrentReportingYear);
                if (ca.isSubmitted) {
                    // If it was a submitted CA, the banked water history table
                    // still needs to be loaded because it wasn't set above.
                    ca.bankedWaterHistory = getBankedWaterTable(ca.number);
                }

			}

			return pageState;

		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="ca"></param>
		/// <param name="wellIdsInsideCA">The well IDs for the CA as returned by a spatial query.</param>
		/// <returns></returns>
		private Dictionary<int, JsonErrorCondition> GetMeterInstallationErrors(ContiguousAcres ca, IEnumerable<int> wellIdsInsideCA) {
			var ret = new Dictionary<int, JsonErrorCondition>();
			var mdalc = new MeterDalc();
			var rptgDalc = new ReportingDalc();

			if (ca.Wells == null) {
				ca.Wells = new WellDalc().GetWells(wellIdsInsideCA.ToArray()).ToArray();
			}

			// Check for irregularities in meter installations.
			// 1. If a meter has a well associated with it that's outside the CA, note it.
			foreach (var well in ca.Wells) {
				try {
					foreach (var miid in well.MeterInstallationIds) {
						var wids = mdalc.GetAssociatedWells(miid).Select(x => x.WellId).Except(wellIdsInsideCA);
						if (wids.Count() > 0) {
							// There's a well associated that's not in the CA.
							// Check to see if there's a user response.
							ret[miid] = new JsonErrorCondition() {
								wellIds = wids.ToArray(),
								errorCondition = "Associted well IDs " + string.Join(", ", wids.Select(x => "#" + x.ToString())) + " are outside the contiguous area.",
								userResponse = rptgDalc.GetMeterInstallationErrorResponse(miid, ActualUser.ActingAsUserId ?? ActualUser.Id)
							};
						}
					}
				} catch (MeterNotFoundException ex) {
					// Suggests that the meter installation was deleted, 
					// but is still associated with the well for some reason.

					// TODO: Not sure how to handle this.
				}
			}

			return ret;
		}


		//
		// GET: /Reporting/
		[DeploymentFilter]
		public ActionResult Index() {
            ViewBag.IsCafoDeployed = ConfigurationManager.AppSettings["is_cafo_deployed"].ToBoolean();
            ViewBag.IsEcfDeployed = ConfigurationManager.AppSettings["is_ecf_deployed"].ToBoolean();
			ViewBag.CurrentUserEmailAddress = ActingUser.Email;
			if (new ReportingDalc().CanUserOverrideReportingDates(ActualUser.ActingAsUserId ?? ActualUser.Id)) {
				IsReportingAllowed = true;
			}
			var cdalc = new ConfigDalc();
			return View(new ReportingViewModel() {
				meterUnitConversionFactors = JObject.FromObject(cdalc.GetAllMeterUnitConversionFactors()).ToString(),
				unitConversionFactors = JObject.FromObject(cdalc.GetAllUnitConversionFactors()).ToString(),
				pageState = JObject.FromObject(GetPageStateJson(ActingUser)).ToString()
			});
		}


		public ActionResult AlternateIndex() {
			if (new ReportingDalc().CanUserOverrideReportingDates(ActualUser.ActingAsUserId ?? ActualUser.Id)) {
				IsReportingAllowed = true;
			}
			// For this alternate bizarro-world page, require a special code
			int code = DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day;
			if (Request["code"] != code.ToString()) {
				return RedirectToAction("Index");
			}
			var cdalc = new ConfigDalc();
			return View(new ReportingViewModel() {
				meterUnitConversionFactors = JObject.FromObject(cdalc.GetAllMeterUnitConversionFactors()).ToString(),
				unitConversionFactors = JObject.FromObject(cdalc.GetAllUnitConversionFactors()).ToString(),
				pageState = JObject.FromObject(GetPageStateJson(ActingUser)).ToString()
			});
		}

		#region Validation support

        /// <summary>
        /// Validates CA for usage submittal. Assumes current reporting year.
        /// </summary>
        /// <param name="ca"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
		protected bool ValidateContiguousAcres(JsonContiguousAcres ca, out List<string> errors) {
			errors = new List<string>();
            /* MWinckler.20130202: This set of validation is written as CA-specific, but it is
             * attempting to validate meter-specific conditions. This validation needs to be
             * rewritten to validate meter readings, and also not rely on calculated boolean
             * values sent from the (untrustworthy) client. Rely on the actual submitted meter
             * readings and user inputs - we have them all here.

            if (ca.isFakingValidReadings)
            {
                if(!ca.userRevisedVolume)
                    errors.Add("The end reading for one of the meters is not in the valid reporting range of December 15 to January 15.");
            }
            if (ca.notEnoughReadingsForAllMeters) // This also allows user override
            {
                if (!ca.userRevisedVolume)
                    errors.Add("One of the meters does not have enough valid readings for volume calculation.");
            }
            if (!ca.hasValidBeginReadings) // This also allows user override
            {
                if (!ca.userRevisedVolume)
                    errors.Add("One of the meters does not have valid begin readings for volume calculation.");
            }
             */
			// Ensure the CA actually exists in the database.
			if (!new GisDalc().ContiguousAcresExists(ca.number)) {
				errors.Add("The specified contiguous area (ID: " + ca.number + ") does not exist.");
			} else {
				// (the remainder of validation is only useful if the CA actually exists)

				// Verify that the user is actually associated with the CA
				if (!new GisDalc().OwnsContiguousAcres(ActualUser, ca.number)) {
					errors.Add("Records show that you do not own the specified contiguous acres. You may not record banked water for this area.");
					return false;
				}

				// Ensure the CA is for the current reporting year.
				if (ca.year != CurrentReportingYear) {
					errors.Add("You cannot submit usage reports for previous operating years.");
				}

				// Check to see if the CA has already been submitted.
				if (new ReportingDalc().IsSubmitted(ca.number, CurrentReportingYear)) {
					errors.Add("The usage report has already been submitted for this contiguous area in this reporting year.");
				}

				if (ca.annualUsageSummary.contiguousArea <= 0) {
					errors.Add("The contiguous area has no acreage defined!");
				}

				foreach (var mr in ca.meterReadings) {
					if (mr.Value.acceptCalculation) {
						if (mr.Value.calculatedVolume < 0) {
							errors.Add("Meter ID " + mr.Key.ToString() + " has an invalid volume.");
						}
					} else {
						if (mr.Value.userRevisedVolume <= 0) {
							errors.Add("Meter ID " + mr.Key.ToString() + " has an invalid user-revised volume.");
						}
					}
				}

				if (ca.annualUsageSummary.desiredBankInches < 0) {
					errors.Add("Desired water bank value cannot be negative.");
				}

                // Retrieve allowable application rate for this year
                // It's also on the submitted CA JSON, but we cannot 
                // trust the client here!
				var rdalc = new ReportingDalc();
                var allowableRate = rdalc.GetAllowableProductionRate(CurrentReportingYear);
                if (ca.annualUsageSummary.desiredBankInches > allowableRate) {
                    errors.Add(string.Format("You cannot bank more than {0} inches for this reporting year.", allowableRate));
                }

				// Check all associated wells
				foreach (var well in ca.wells) {
					if (well.meterInstallationIds.Length == 0) {
						// Check to see if there's already been a user error response
						if (!rdalc.IsWellErrorResponseRecorded(well.id, ActualUser.ActingAsUserId ?? ActualUser.Id)) {
							errors.Add("Well #" + well.id + " has no associated meters.");
						}
					}
				}

				// Retrieve previous banked water values for the CA.
				// (These are on the ca object, but we can't trust the client.)
				var bankHistory = new ReportingDalc().GetHistoricalBankedWater(ca.number);
				int prevBank = bankHistory.Where(x => x.year == CurrentReportingYear - 1).Select(x => x.bankedInches).DefaultIfEmpty(0).First();
				int avgAppRate = (int)(ca.annualUsageSummary.annualVolume / ca.annualUsageSummary.contiguousArea);
				int surplusSubtotal = ca.annualUsageSummary.allowableApplicationRate - avgAppRate;
				var surplusTotal = prevBank + surplusSubtotal;
				if (ca.annualUsageSummary.desiredBankInches > 0 && ca.annualUsageSummary.desiredBankInches > surplusTotal) {
					errors.Add("You cannot bank more water than your cumulative available total (previous year's banked inches plus this year's surplus/deficit).");
				}

			}

			return errors.Count == 0;
		}


		#endregion


		#region Ajax methods

		// There are two ways the application saves state: automatically, and user-initiated.
		// The autosave routine only keeps some most recent snapshots (deleting older ones);
		// user-initiated saves are never discarded. (A reporting usage submittal would constitute
		// a user-initiated save.) The retrieval routines look at whichever save is most recent
		// when loading page state, and additionally certain updated data (such as new years,
		// changed meter data, etc.) may be injected into the loaded state.

		/// <summary>
		/// Autosaves a snapshot of the user's page state.
		/// </summary>
		/// <param name="jsonState"></param>
		/// <returns></returns>
		public JsonResult AutosaveState(string jsonState) {
			try {
				new ReportingDalc().SaveReportingSummary(this.ActualUser, jsonState, false);
			} catch (Exception ex) {
				return Json(new { success = false, error = ex.Message });
			}
			return Json(new { success = true });
		}

		/// <summary>
		/// Stores a user-initiated save.
		/// </summary>
		/// <param name="jsonState"></param>
		/// <returns></returns>
		public JsonResult SaveState(string jsonState) {
			try {
				new ReportingDalc().SaveReportingSummary(this.ActualUser, jsonState, true);
			} catch (Exception ex) {
				return Json(new { success = false, error = ex.Message });
			}
			return Json(new { success = true });
		}

		/// <summary>
		/// Saves an error response to the database.
		/// </summary>
		/// <param name="wellId"></param>
		/// <param name="response"></param>
		/// <returns></returns>
		public JsonResult SaveErrorResponse(int? wellId, int? meterInstallationId, string response) {
			try {
				new ReportingDalc().SaveErrorResponse(this.ActualUser, wellId, meterInstallationId, response);
			} catch (Exception ex) {
				return Json(new { success = false, error = ex.Message });
			}
			return Json(new { success = true });
		}



		public JsonResult SubmitUsageReport(string state, string ca) {
			var contigAcres = JsonConvert.DeserializeObject<JsonContiguousAcres>(ca);

			AutosaveState(state);
			List<string> validationErrors;
			if (!ValidateContiguousAcres(contigAcres, out validationErrors)) {
				return Json(new JsonResponse(false, validationErrors.ToArray()));
			}

			
			// Save the reporting summary values independently of the json.
			// Important stuff: 
			// - Contiguous Area
			// - Annual Volume
			// - Banked water from previous year
			// - Desired bank inches
			// For each meter:
			// - meter installation id
			// - calculated volume (gallons)
			// - user revised volume (gallons)
			bool success = true;
			string error = "";
			try {
				success = new ReportingDalc().SaveAnnualUsageReport(ActualUser, contigAcres, out error);
			} catch (Exception ex) {
				success = false;
				error = "Server error: " + ex.Message;
			}

			if (success) {
				// Update the state
				try {
					var pageState = JsonConvert.DeserializeObject<ReportingSummary>(state);
					// Find the index of the CA we care about
					foreach (var acres in pageState.years[contigAcres.year].contiguousAcres) {
						if (acres.number == contigAcres.number) {
							acres.isSubmitted = true;
							break;
						}
					}
					SaveState(JsonConvert.SerializeObject(pageState));
				} catch (Exception ex) {
					// TODO: Not sure how we should handle an exception here.
					// The report was submitted but the "isSubmitted" flag was
					// not set to true on the state object...which will only ever
					// matter if the CA is deleted in this same year (after being submitted)
					// and in that case it wouldn't show up on the page due to the
					// way CAs are loaded during the page state load.
					//
					// For now I'll assume this case is so rare that it does not
					// warrant extra effort to handle this error in a special way.

				}
			}

			return Json(new JsonResponse(success, error));
		}

		/// <summary>
		/// Sends a message to the owner of the CA via email,
		/// using the current user's email address as the "From"
		/// address.
		/// </summary>
		/// <param name="caId"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		[HttpPost]
		public JsonResult EmailCAOwner(int caId, string message) {
			if (ActualUser == null) {
				return Json(new JsonResponse(false, "You are not authorized to perform that action."));
			}

			if (string.IsNullOrWhiteSpace(message)) {
				return Json(new JsonResponse(false, "You must specify a message to send."));
			}

			var ca = new GisDalc().GetContiguousAcres(caId);
			if (ca == null) {
				return Json(new JsonResponse(false, "No CA corresponding to CA ID " + caId + " was found."));
			}

			var owner = new UserDalc().GetUser(ca.OwnerClientId);
			if (owner == null) {
				return Json(new JsonResponse(false, "No owner information found for CA ID " + caId + " (owner ID " + ca.OwnerClientId + ")."));
			}

			// Prepend some boilerplate to each message explaining to the owner
			// what in the world this thing is
			message = @"This message has been sent from the High Plains Water District website on behalf of " 
						+ ActingUser.DisplayName.Trim() + " (" + ActingUser.Email + @") regarding the contiguous acres described as '" + ca.Description 
						+ @"', which our records indicate you own.

To respond, you can reply directly to this email. " + ActingUser.DisplayName.Trim() + @"'s original message follows.
--------

" + message;

			MailHelper.Send(owner.Email, ActingUser.Email, "HPWD: Message for owner of " + ca.Description, message);
			return Json(new JsonResponse(true));
		}


		#endregion


	}
}
