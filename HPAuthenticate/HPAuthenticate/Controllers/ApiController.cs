using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPEntities.Dalcs;
using System.Web.Security;
using HPEntities.Entities;
using HPEntities;
using HPEntities.Entities.Enums;
using libMatt.Formatters;
using libMatt.Converters;
using System.Text;

namespace HPAuthenticate.Controllers {
	public class ApiController : ApplicationController {
		//
		// GET: /Api/

		public ActionResult Index() {
			return View();
		}

		/// <summary>
		/// Gets all owners whose names contain the specified term.
		/// This function returns an array of AutocompleteResult objects,
		/// with labels and values.
		/// </summary>
		/// <param name="term"></param>
		/// <returns></returns>
		public JsonResult FindOwners(string term) {
			var ar = (from owner in new OwnerDalc().GetOwnersByName(term, 15)
					  select owner).ToArray();
			return Json(ar);
		}

		/// <summary>
		/// Looks up the username stored in the given authentication ticket
		/// and returns it as a string.
		/// </summary>
		/// <param name="authToken">(string) The forms authentication ticket to decrypt.</param>
		/// <returns>(string) The username associated with the given ticket, or a blank string if it is not valid.</returns>
		public JsonResult GetUsername(string ticket) {
			try {
				var faTicket = FormsAuthentication.Decrypt(ticket);
				if (faTicket != null && !faTicket.Expired) {
					return Json(faTicket.Name, JsonRequestBehavior.AllowGet);
				}
			} catch (Exception ex) {
				return Json("Exception occurred: " + ex.Message, JsonRequestBehavior.AllowGet);
			}
			return Json("", JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// Looks up the username (email address) of the given user, 
		/// and also finds any associated property descriptions (via
		/// either ownership or authorized producership) and returns
		/// the entire result as a JSON array.
		/// 
		/// In the event of an "act as" situation, the data returned
		/// will be for the _acted as_ user, with the sole exception
		/// of "data.actualClientId", which will be the client ID of
		/// the true (admin) user.
		/// </summary>
		/// <param name="ticket">(string) The FormsAuthentication ticket to decrypt.</param>
		/// <returns>(JSON) A JSON object containing username and any associated property descriptions.</returns>
		public JsonResult GetUserData(string ticket) {
			/* Sample result format
	{
		status:{success:true, errors:[]},
		data: {
			ActualClientId:1234,
			ActingAsClientId:5678,
			EmailAddress:"mwinckler@intera.com",
			property_descriptions: [
				{ OwnerName:"Matt", PropertyDesc:"123 Fifth Ave.", PropId:123, IsCurrentUserOwner:true },
				{ OwnerName:"Meng", PropertyDsec:"555 wherever st.", PropId:555, IsCurrentUserOwner:false }
			],
			VisibleContiguousAcresIds:[4,2,1,662,124]
		}
	}
			 */
			bool success = true;
			var errors = new List<string>();
			List<PropertyDescription> propDescs = null;
			User actualUser = null, actingUser = null;
			int[] caIds = new int[] { };
			try {
				actualUser = actingUser = GetUserFromAuthTicket(ticket);
				if (actualUser != null) {
					// If this user is an admin and is acting for someone else,
					// pull that someone else's info instead.
					if (actualUser.IsAdmin && actualUser.ActingAsUserId.HasValue) {
						actingUser = new UserDalc().GetUser(actualUser.ActingAsUserId.Value);
					}
					// Pull property descriptions for this user, including owned and
					// authorized properties
					propDescs = (from pd in new UserDalc().GetAssociatedProperties(actingUser.Id)
								 select new PropertyDescription(pd)).ToList();

					// Retrieve the contiguous acres IDs that this user is permitted to see,
					// namely, the ones associated with properties that are associated with
					// the user's account, PLUS the contiguous acres definitions the user
					// created (regardless of association).
					caIds = new PropertyDalc().GetContiguousAcresIds(actingUser.Id, (from pd in propDescs select new Tuple<string, string>(pd.ParcelId, pd.County)));
				}

			} catch (Exception ex) {
				success = false;
				errors.Add("Exception occurred: " + ex.Message);
			}

			object ret;

			if (actualUser == null || actingUser == null) {
				ret = new JsonResponse(false, "No such user exists.");
			} else {
				var d = new {
					ActualUserId = actualUser.Id,
					ActingUserId = actingUser.Id,
					DisplayName = actingUser.DisplayName,
					EmailAddress = actingUser.Email,
					PropertyDescriptions = propDescs.OrderBy(x => x.OwnerName).ToArray() ?? new PropertyDescription[] {},
					VisibleContiguousAcresIds = caIds,
					PhoneNumber = string.Join("; ", actingUser.PhoneNumbers)
				};
				ret = new JsonResponse(success, (object)d, errors.ToArray());
			}

			return Json(ret, JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// Validates the meter given by object ID. Returns JSON in the following format:
		/// {
		///		isValid: (bool),
		///		errors: (string[])
		///	}
		/// </summary>
		/// <param name="meterInstId"></param>
		/// <returns></returns>
		public JsonResult IsMeterValid(int objectId) {

			var errors = new List<string>();
			var gdalc = new GisDalc();
			var meter = gdalc.GetMeterByObjectId(objectId);
			if (meter == null) {
				errors.Add("Specified meter not found; OBJECTID: " + objectId);
			} else {
				// Begin validation.
				// 1. Inst date must be in the past.
				// inst_date should be stored in local server time zone, where this app also resides,
				// so time zones should not be an issue.
				if (!meter.inst_date.HasValue || meter.inst_date.Value > DateTime.Now) {
					errors.Add("The installation date must be equal to the current day or before.");
				}

				if (!meter.read_date.HasValue || meter.read_date.Value > DateTime.Now) {
					errors.Add("The initial reading date must be equal to the current day or before.");
				}


				// 20111130: Grandfathered manufacturers/models are not validated for associations.
				if (!string.IsNullOrEmpty(meter.Model) && !string.IsNullOrEmpty(meter.Manufacturer)) {
					if (!new MeterManufacturerDalc().IsMeterAssociatedWithManufacturer(meter.Model, meter.Manufacturer)) {
						errors.Add("The model " + HttpUtility.HtmlEncode(meter.Model) + " is not available for manufacturer " + HttpUtility.HtmlEncode(meter.Manufacturer) + ".");
					}
				}

				Action<string, List<string>> validateSerial = (serial, errorList) => {
					if (string.IsNullOrWhiteSpace(serial)) {
						errorList.Add("The serial number must be entered.");
					}
				};

				Action<string, string, List<string>> validateSize = (size, errorLabel, errorList) => {
					if (string.IsNullOrWhiteSpace(size)) {
						errorList.Add(string.Format("The {0} must be entered.", errorLabel));
					}
				};

				Action<double?, List<string>> validateInitialReading = (initialReading, errorList) => {
					if (!initialReading.HasValue) {
						errorList.Add("The meter installation must have an initial reading.");
					}
				};


				// 20111130: Special case "Alternate Reporting" manufacturer does not
				// require serial/size validation.
				if (!meter.Manufacturer.ToLower().StartsWith("alternate reporting")) {
					validateSerial(meter.Serial, errors);
					validateSize(meter.Size, "size", errors);

					// Require at least one meter installation reading.
					validateInitialReading(meter.InitialReading, errors);
				} else {
					// Alternate reporting meters require special kinds of validation
					// depending on the meter name.
					// 20111209: This is hardcoded, because we don't expect them to change
					// and this should only be valid/useful for about a year after deployment.
					// If these end up changing, consider moving this to a config table in the database.
					switch (meter.Model.ToLower()) {
						case "alternate: electric":
						case "alternate: natural gas":
							// electric and nat gas validates serial and initial reading.
							validateSerial(meter.Serial, errors);
							validateInitialReading(meter.InitialReading, errors);
							break;
						case "alternate: nozzle package":
							validateSize(meter.Size, "rate", errors);
							validateInitialReading(meter.InitialReading, errors);
							break;
						case "alternate: third party":
							// Third party validates none of this.
							break;
					}
				}

				// Require: (manu && model) || (gf_manu && gf_model)
				if ((string.IsNullOrWhiteSpace(meter.Manufacturer) || string.IsNullOrWhiteSpace(meter.Model))
						&& (string.IsNullOrWhiteSpace(meter.Gf_Manu) || string.IsNullOrWhiteSpace(meter.Gf_Model))) {
					errors.Add("You must choose a Manufacturer/Model or enter a Grandfathered Manufacturer/Model.");
				}

				if (string.IsNullOrEmpty(meter.Application)) {
					errors.Add("The meter application field cannot be blank.");
				}

			}

			return Json(new { isValid = errors.Count == 0, errors = errors }, JsonRequestBehavior.AllowGet);
		}


		/// <summary>
		/// Returns the past [count] meter readings for the specified meter installation ID (meterInstId).
		/// JSON result format is a <see cref="HPEntities.Entities.JsonResponse"/> object, with data
		/// format of:
		///		{ 
		///			MeterInstallationId: 0,
		///			Readings:[
		///				{ 
		///					Date:"1/1/2011",
		///					Reading: (null or int),
		///					Volume: (null or int),
		///					UnitOfMeasureId: 0,
		///					ActingId: 0,
		///					ActualId: 0
		///				},
		///				{ ... }
		///			]
		///		}	
		/// </summary>
		/// <param name="meterInstId"></param>
		/// <returns></returns>
		public JsonResult GetRecentMeterReadings(int meterInstId, int count) {
			bool success = true;
			List<string> errors = new List<string>();
			object data = null;
			try {
				data = new MeterDalc().GetRecentReadings(meterInstId, null, count);
			} catch (Exception ex) {
				success = false;
				errors.Add(ex.Message);
			}


			if (data != null) {
				return Json(new JsonResponse(success, data, errors.ToArray()), JsonRequestBehavior.AllowGet);
			} else {
				return Json(new JsonResponse(success, errors.ToArray()), JsonRequestBehavior.AllowGet);
			}
		}

		/// <summary>
		/// Returns a hash of UnitOfMeasure records; Key:id, Value:name
		/// </summary>
		/// <returns></returns>
		public JsonResult GetUnitOfMeasurementDefinitions() {
			// This is brain damaged, but .NET refuses to serialize a dictionary to JSON
			// when the key is anything other than a string or an object. They say the rationale
			// is that dictionaries in Javascript are always keyed with strings, which I
			// believe to be false, but in any case this is a workaround to convert the
			// integer key to a string. /drainbramage
			var dict = new MeterDalc().GetUnitOfMeasurementDefinitions().ToDictionary(
							x => x.Key.ToString(),
							x => x.Value
						);
			return Json(dict, JsonRequestBehavior.AllowGet);
		}

		public JsonResult SaveMeterReading(string authTicket, int meterInstId, DateTime? date, double? reading, int actingUserId, int actualUserId, double? rate) {
			// First, verify that the logged-in user has the necessary permissions to 
			// save a meter reading (including permission to use the specified userIds.)
			var user = GetUserFromAuthTicket(authTicket);
			if (user == null) {
				// No user found based on auth ticket.
				return Json(new JsonResponse(false, "Invalid authentication ticket - no user found."));
			}

			if (user.Id != actualUserId) {
				// Bizarre - the auth ticket is not for the specified user id.
				return Json(new JsonResponse(false, "Unauthorized action: The specified authentication ticket does not match the provided actual user ID."));
			}

			if (actingUserId != actualUserId && !user.IsAdmin) {
				// Acting as is only allowed for admins.
				return Json(new JsonResponse(false, "Unauthorized action: You do not have permission to act for that user."));
			}

			if (!date.HasValue || date.Value > DateTime.Now) {
				return Json(new JsonResponse(false, "Reading date must be equal to or earlier than the current date."));
			}

			string error;
			var success = new MeterDalc().SaveMeterReading(new MeterReading() {
				MeterInstallationId = meterInstId,
				DateTime = date.Value,
				Reading = reading,
				ActingUserId = actingUserId,
				ActualUserId = actualUserId,
				Rate = rate
			}, out error);


			return Json(new JsonResponse(success, error));
		}


		/// <summary>
		/// Returns the meter whose meterInstId corresponds to the given meterId.
		/// </summary>
		/// <param name="meterInstId"></param>
		/// <returns></returns>
		public JsonResult GetMeter(int meterInstId) {
			var m = new GisDalc().GetMeter(meterInstId);
			if (m == null) {
				return Json(new JsonResponse(false, "The specified meter ID (" + meterInstId.ToString() + ") does not exist."));
			}

			return Json(new JsonResponse(true, m), JsonRequestBehavior.AllowGet);
		}


		/// <summary>
		/// Associates a property with the user account specified by actingUserId.
		/// </summary>
		/// <param name="actualUserId">(int) The actual logged-in user.</param>
		/// <param name="actingUserId">
		///		(int) The "act-as" user (may be the same as the logged-in user; 
		///		actualUser must have privileges to act-as if this ID is different).
		///		This is the user account the property will be associated with.
		///	</param>
		/// <param name="role">(PropertyRole) The acting user's role in relation to the property.</param>
		/// <param name="parcelId">(string) The parcel ID (PropertyNumber) corresponding to the property in appraisal roll records.</param>
		/// <param name="county">(string) The county of the </param>
		/// <param name="productionTypes"></param>
		/// <returns></returns>
		public JsonResult AddProperty(string authTicket, int actualUserId, int actingUserId, PropertyRole role, string parcelId, string county, DisclaimerDataType productionTypes) {
			var errors = new List<string>();
			Func<JsonResult> jsonStatus = () => {
				return Json(new JsonResponse(errors.Count == 0, errors.ToArray()));
			};

			// 0. Verify that all required data is present.
			if (actualUserId < 1) {
				errors.Add("Parameter 'actualUserId' is required and must be greater than 0.");
			}
			if (actingUserId < 1) {
				errors.Add("Parameter 'actingUserId' is required and must be greater than 0.");
			}
			if (string.IsNullOrWhiteSpace(parcelId)) {
				errors.Add("Parameter 'parcelId' is required and cannot be blank.");
			}
			if (string.IsNullOrWhiteSpace(county)) {
				errors.Add("Parameter 'county' is required and cannot be blank.");
			}
			if (!Enum.IsDefined(typeof(PropertyRole), role)) {
				errors.Add("Invalid property role: " + role.ToString());
			}
			// Check for validity of production types - this is a flags enum,
			// so valid values are anything > 0 and < (sum of all values)
			var maxVal = Enum.GetValues(typeof(DisclaimerDataType)).Cast<int>().Sum();
			if (productionTypes < 0 || (int)productionTypes > maxVal || (productionTypes == 0 && role != PropertyRole.installer)) {
				errors.Add("Invalid production type: " + productionTypes.ToString());
			}



			if (errors.Count > 0) {
				return jsonStatus();
			}

			var udalc = new UserDalc();
			User actualUser = GetUserFromAuthTicket(authTicket);
			User actingUser = udalc.GetUser(actingUserId);
			if (actualUser == null) {
				errors.Add("Unable to find user account based on provided auth ticket.");
			}
			if (actingUser == null) {
				errors.Add("Unable to find user account corresponding to actingUserId == " + actingUserId.ToString());
			}
			if (errors.Count > 0) {
				return jsonStatus();
			}

			if (actualUser.Id != actualUserId) {
				// Bizarre - the auth ticket is not for the specified user id.
				errors.Add("Unauthorized action: The specified authentication ticket does not match the provided actual user ID.");
				return jsonStatus();
			}

			// 1. Ensure actual user has permission to pose as acting user
			if (actualUserId != actingUserId && !actualUser.IsAdmin) {
				errors.Add("Unauthorized action: You do not have permission to act for that user.");
				return jsonStatus();
			}

			var propDalc = new PropertyDalc();

			// 2. Verify that the property matches values in AppraisalRolls.
			//    Also check to ensure there is only one property matching this parcel id
			//		and county. (This is in response to a bug in production where there
			//		where many parcelIds of 0 in Cochran county; without this check
			//		some hundreds of records would be associated with the user account.)
			int propertyCount = propDalc.GetPropertyCount(parcelId, county);

			if (propertyCount == 0) {
				errors.Add(string.Format("Unable to find a matching appraisal roll record for parcel ID '{0}', county '{1}'", parcelId, county));
				return jsonStatus();
			} else if (propertyCount > 1) {
				errors.Add(string.Format("Multiple ({0}) records found for parcel ID '{1}', county '{2}'. Cannot add property when duplicates exist.", propertyCount, parcelId, county));
				return jsonStatus();
			}

			// 3. If the property has already been associated with the user account,
			//		return an error message to that effect.
			if (propDalc.IsPropertyAssociated(actingUserId, parcelId, county)) {
				errors.Add("The property is already associated with your account. If you wish to change roles, please delete the existing property from your account and add it again with the different role.");
				return jsonStatus();
			}

			// 4. Create the association.
			propDalc.AssociateProperty(actingUser, new Property(parcelId, county, ""), role, true, productionTypes, false);

			return jsonStatus();
		}


		public JsonResult GetPropertyRoles() {
			var ret = new Dictionary<string, string>();
			foreach (PropertyRole role in Enum.GetValues(typeof(PropertyRole))) {
				ret[((int)role).ToString()] = role.Description();
			}
			return Json(ret, JsonRequestBehavior.AllowGet);
		}

		public JsonResult GetProductionTypes() {
			var ret = new Dictionary<string, string>();
			foreach (DisclaimerDataType d in Enum.GetValues(typeof(DisclaimerDataType))) {
				ret[((int)d).ToString()] = d.Description();
			}
			return Json(ret, JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// Returns the wells associated with the given meter. meterId corresponds
		/// to the meterInstId of the desired meter.
		/// </summary>
		/// <param name="meterInstId"></param>
		/// <returns></returns>
		public JsonResult GetAssociatedWells(FormCollection form) {
			int meterInstId;
			if (!int.TryParse(form["meterInstId"].GetString(), out meterInstId)) {
				return Json(new JsonResponse(false, "Unable to parse an integer meterInstId from provided value: '" + form["meterInstId"].GetString() + "'."));
			}
			return GetAssociatedWells(meterInstId);
		}

		private JsonResult GetAssociatedWells(int meterInstId) {
			try {
				var wells = new MeterDalc().GetAssociatedWells(meterInstId);
				return Json(new JsonResponse(true, new { Wells = wells.ToArray(), MeterInstId = meterInstId }), JsonRequestBehavior.AllowGet);
			} catch (Exception ex) {
				return Json(new JsonResponse(false, ex.Message));
			}
		}


		//public JsonResult AssociateWellsWithMeter(string authTicket, int actualUserId, int actingUserId, int meterInstId, int[] wellIds) {
		public JsonResult AssociateWellsWithMeter(FormCollection form) {
			

			string  authTicket;
			int actualUserId, actingUserId, meterInstId;
			int[] wellIds = new int[0];
			try {
				authTicket = form["authTicket"];
				actualUserId = form["actualUserId"].ToInteger();
				actingUserId = form["actingUserId"].ToInteger();
				meterInstId = form["meterInstId"].ToInteger();
				var str = form["wellIds[]"].GetString();
				if (str.Length == 0) {
					str = form["wellIds"].GetString();
				}
				if (str.Length > 0) {
					wellIds = str.Split(',').Select(x => x.ToInteger()).ToArray();
				}
			} catch (Exception ex) {
				return Json(new JsonResponse(false, ex.Message));
			}

			var user = GetUserFromAuthTicket(authTicket);
			if (user == null) {
				return Json(new JsonResponse(false, "Invalid authentication ticket."));
			}

			if (user.Id != actualUserId) {
				return Json(new JsonResponse(false, "Provided actual user ID does not match ID from authentication ticket."));
			}

			if (!user.IsAdmin && user.Id != actingUserId) {
				return Json(new JsonResponse(false, "Actual logged-in user is not authorized to act for other users."));
			}

			try {
				new MeterDalc().SaveWellAssociation(actualUserId, actingUserId, meterInstId, wellIds);
			} catch (Exception ex) {
				return Json(new JsonResponse(false, ex.Message));
			}

			return GetAssociatedWells(meterInstId);

		}


		/// <summary>
		/// Associates contiguous acres with parcel/county combo specified in form.
		/// This method deletes any existing associations, so only those parcels/counties
		/// specified here will be associated after the function returns.
		/// </summary>
		/// <param name="form"></param>
		/// <returns></returns>
		public JsonResult AssociateContiguousAcresWithParcels(FormCollection form) {
			try {
				int OBJECTID = form["contiguousAcresObjectId"].ToInteger();
				int contiguousAcresId = new GisDalc().GetContiguousAcresIdFromOBJECTID(OBJECTID);
				string authTicket = form["authTicket"].GetString();
				var user = GetUserFromAuthTicket(authTicket);

				if (user == null) {
					return Json(new JsonResponse(false, "Invalid auth ticket - unable to find user."));
				}
				if (contiguousAcresId <= 0) {
					return Json(new JsonResponse(false, "Unable to find a contiguous acres definition corresponding to OBJECTID " + OBJECTID));
				}


				string[] parcelIds = GetArrayFromFormParameter(form, "parcelIds");
				string[] counties = GetArrayFromFormParameter(form, "counties");

				if (parcelIds.Length == 0 || counties.Length == 0 || parcelIds.Length != counties.Length) {
					return Json(new JsonResponse(false, string.Format(
															"You must specify at least one parcel and county, and the length of the parcelIds and counties arrays must be the same (parcelIds.Length == {0}, counties.Length == {1})",
															parcelIds.Length,
															counties.Length
													)));
				}

				var propDalc = new PropertyDalc();
				var parcelCounties = new List<Tuple<string, string>>();
				for (int i = 0; i < parcelIds.Length; i++) {
					parcelCounties.Add(new Tuple<string, string>(parcelIds[i], counties[i]));
				}
				propDalc.AssociateContiguousAcres(contiguousAcresId, parcelCounties, user.Id, user.ActingAsUserId ?? user.Id);

				return Json(new JsonResponse(true));
			} catch (Exception ex) {
				return Json(new JsonResponse(false, ex.Message));
			}
		}

		/// <summary>
		/// Returns all property descriptions associated with the given contiguous acres id.
		/// </summary>
		/// <param name="CAId"></param>
		/// <returns></returns>
		public JsonResult GetAssociatedOwners(int CAObjectId) {
			// CAId here is actually OBJECTID.
			var caId = new GisDalc().GetContiguousAcresIdFromOBJECTID(CAObjectId);
			return Json(new JsonResponse(true, new {
				PropertyDescriptions = new PropertyDalc().GetAssociatedPropertyDescriptions(caId)
			}), JsonRequestBehavior.AllowGet);
		}

		[HttpPost]
		public JsonResult ChangeUserRole(string authTicket, int actingUserId, int actualUserId, PropertyRole? role, string parcelId, string county, DisclaimerDataType? productionTypes) {
			try {
				List<string> errors = new List<string>();
				Func<JsonResult> jsonStatus = () => {
					return Json(new JsonResponse(errors.Count == 0, errors.ToArray()));
				};

				// 0. Verify that all required data is present.
				if (actualUserId < 1) {
					errors.Add("Parameter 'actualUserId' is required and must be greater than 0.");
				}
				if (actingUserId < 1) {
					errors.Add("Parameter 'actingUserId' is required and must be greater than 0.");
				}
				if (string.IsNullOrWhiteSpace(parcelId)) {
					errors.Add("Parameter 'parcelId' is required and cannot be blank.");
				}
				if (string.IsNullOrWhiteSpace(county)) {
					errors.Add("Parameter 'county' is required and cannot be blank.");
				}


				if (!role.HasValue) {
					// Default to operator
					role = PropertyRole.authorized_producer;
				}
				if (!Enum.IsDefined(typeof(PropertyRole), role)) {
					errors.Add("Invalid property role: " + role.ToString());
				}

				if (!productionTypes.HasValue) {
					productionTypes = DisclaimerDataType.agriculture;
				}
				// Check for validity of production types - this is a flags enum,
				// so valid values are anything > 0 and < (sum of all values)
				var maxVal = Enum.GetValues(typeof(DisclaimerDataType)).Cast<int>().Sum();
				if (productionTypes < 0 || (int)productionTypes > maxVal || (productionTypes == 0 && role != PropertyRole.installer)) {
					errors.Add("Invalid production type: " + productionTypes.ToString());
				}

				if (errors.Count > 0) {
					return jsonStatus();
				}


				var udalc = new UserDalc();
				User actualUser = GetUserFromAuthTicket(authTicket);
				User actingUser = udalc.GetUser(actingUserId);
				if (actualUser == null) {
					errors.Add("Unable to find user account based on provided auth ticket.");
				}
				if (actingUser == null) {
					errors.Add("Unable to find user account corresponding to actingUserId == " + actingUserId.ToString());
				}
				if (errors.Count > 0) {
					return jsonStatus();
				}

				if (actualUser.Id != actualUserId) {
					// Bizarre - the auth ticket is not for the specified user id.
					errors.Add("Unauthorized action: The specified authentication ticket does not match the provided actual user ID.");
					return jsonStatus();
				}

				// 1. Ensure actual user has permission to pose as acting user
				if (actualUserId != actingUserId && !actualUser.IsAdmin) {
					errors.Add("Unauthorized action: You do not have permission to act for that user.");
					return jsonStatus();
				}

				var propDalc = new PropertyDalc();

				// 2. Verify that the property matches values in AppraisalRolls.
				if (!propDalc.DoesPropertyExist(parcelId, county)) {
					errors.Add(string.Format("Unable to find a matching appraisal roll record for parcel ID '{0}', county '{1}'", parcelId, county));
					return jsonStatus();
				}

				// 3. If the property has not been associated with the user account,
				//		return an error message to that effect.
				int clientPropertyId;
				if (!propDalc.IsPropertyAssociated(actingUserId, parcelId, county, out clientPropertyId)) {
					errors.Add("The specified property is not associated with your account. Please first add the property to your account.");
					return jsonStatus();
				}


				propDalc.ChangePropertyRoleAndProductionType(actingUser, clientPropertyId, role.Value, productionTypes.Value);

				return jsonStatus();
			} catch (Exception ex) {
				return Json(new JsonResponse(false, ex.Message));
			}
		}



		#region Helpers


		private User GetUserFromAuthTicket(string ticket) {
			FormsAuthenticationTicket faTicket = null;
			try {
				faTicket = FormsAuthentication.Decrypt(ticket);
			} catch (ArgumentException ex) {
				// Invalid ticket.
				return null;
			}
			if (faTicket != null && !faTicket.Expired) {
				var username = faTicket.Name;

				return new UserDalc().GetUser(username);
			}
			return null;
		}

		private string[] GetArrayFromFormParameter(FormCollection form, string parameterName) {
			var str = form[parameterName + "[]"].GetString();
			if (str.Length == 0) {
				// If only a single value was passed from the form, it won't have the bracket array notation.
				str = form[parameterName].GetString();
			}
			if (str.Length > 0) {
				return str.Split(',');
			}
			return new string[] {};
		}


		#endregion

	}
}
