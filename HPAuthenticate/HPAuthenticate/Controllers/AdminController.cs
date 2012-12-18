using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPAuthenticate.ViewModels;
using HPEntities.Dalcs;
using HPEntities.Exceptions;
using HPEntities;
using libMatt.Converters;
using HPAuthenticate.Helpers;
using Newtonsoft.Json;

namespace HPAuthenticate.Controllers
{
    public class AdminController : ApplicationController
    {
		// I don't know the "MVC Way" to do this. I need the user controller and admin
		// controller to share the same view, passing it different models, and also
		// each controller needs to reference the view multiple times. I think declaring
		// a constant view name to user is better than including the string multiple times
		// in both controllers.
		const string CREATE_VIEWNAME = "CreateUser";


        //
        // GET: /Admin/
		[Authorize(Roles="admin")]
        public ActionResult Index()
        {
            return View();
        }

		[Authorize(Roles = "admin")]
		public ActionResult ManageAccounts() {
			return View();
		}

		[Authorize(Roles = "admin")]
		public ActionResult ManageUsageReports() {
			return View();
		}


		[Authorize(Roles = "admin")]
		public ActionResult AddUser() {
			return View("CreateUser", new AdminCreateUserViewModel());
		}

		[HttpPost]
		[Authorize(Roles = "admin")]
		public ActionResult AddUser(AdminCreateUserViewModel uvm) {

			string flashMsg = null, vError = null;
			if (!Helpers.UserHelper.PopulatePhoneNumbers(uvm, Request, out vError, out flashMsg)) {
				if (!string.IsNullOrEmpty(vError)) {
					ModelState.AddModelError("PhoneNumbers", vError);
				}
				if (!string.IsNullOrEmpty(flashMsg)) {
					this.FlashError(flashMsg);
				}
				return View(CREATE_VIEWNAME, uvm);
			}

			ModelState.Clear();

			TryValidateModel(uvm);
			// Validate each phone number.
			foreach (var p in uvm.User.PhoneNumbers) {
				TryValidateModel(p);
			}

			if (ModelState.IsValid) {
				var udalc = new UserDalc();

				try {
					User existingUser;
					// MWinckler.20111101: This is my interpretation of the requirements,
					//						not explicitly stated by the client.
					// This model is either a brand-new client record or the user autocompleted
					// the last name into an existing client record. If the latter, then
					// x_user_id will be populated. However, even if that's populated, check
					// the name against the existing client name. If it doesn't match, then
					// assume the user actually meant to create a new record.
					int? x_user_id = Request["x_user_id"].TryToInteger();

					bool success = false;
					if (Helpers.UserHelper.TryGetExistingUser(x_user_id, uvm, out existingUser)) {
						success = udalc.AssociateExistingUser(existingUser, uvm.User);
					} else {
						success = udalc.CreateUser(uvm.User, uvm.User.Password);
					}

					if (success) {
						this.FlashInfo("Account created.");
						// TODO: Act-as the new user and redirect to user profile page.
						ActAs(uvm.User.Id);
						return RedirectToAction("Details", "User");
					} else {
						this.FlashError("Errors occurred while trying to create the account.");
					}

				} catch (ValidationException ex) {
					foreach (var err in ex.ValidationErrors) {
						ModelState.AddModelError("", err);
					}
					this.FlashError("Errors occurred while trying to create the account.");
				}
			}

			return View(CREATE_VIEWNAME, uvm);
		}


		#region Ajax support

		[Authorize(Roles = "admin")]
		public JsonResult SetAdmin(int userId, bool isAdmin) {
			try {
				if (ActualUser.IsAdmin) {
					// Check to see whether the account being set is in the allowed domains, hpwd.com and intera.com
					var udalc = new UserDalc();
					var specimen = udalc.GetUser(userId);
					if (specimen == null) {
						return Json(new { success = false, error = "Specified user does not exist." });
					}
					if (specimen.Email.EndsWith("@hpwd.com") || specimen.Email.EndsWith("@intera.com")) {
						udalc.SetAdmin(userId, isAdmin);
						return Json(new { success = true });
					} else {
						return Json(new { success = false, error = "This account is a non-HPWD account and cannot become an admin." });
					}
				} else {
					return Json(new { success = false, error = "Your account does not have sufficient privileges to perform this action." });
				}
			} catch (Exception ex) {
				Logger.LogError(ex);
				return Json(new { success = false, error = ex.Message });
			}
		}

		[Authorize(Roles = "admin")]
		public ActionResult FindUsageReportsByNameOrEmail(string searchTerm) {
			if (string.IsNullOrEmpty(searchTerm)) {
				return Json(new string[] {});
			}
			return new JsonNetResult() {
				Formatting = Newtonsoft.Json.Formatting.None,
				Data = new ReportingDalc().GetUsageReportsByUserNameOrEmail(searchTerm)
			};
		}

		[Authorize(Roles = "admin")]
		public JsonResult UnsubmitCA(int year, int caId) {
			try {
				if (ActualUser.IsAdmin) {
					new ReportingDalc().UnsubmitCA(year, caId);
				} else {
					return Json(new { success = false, error = "Your account does not have sufficient privileges to perform this action." });
				}
				return Json(new { success = true });
			} catch (Exception ex) {
				Logger.LogError(ex);
				return Json(new { success = false, error = ex.Message });
			}
		}

		[Authorize(Roles = "admin")]
		public JsonResult SetReportingOverride(int userId, bool canReport) {
			try {
				if (ActualUser.IsAdmin) {
					new ReportingDalc().SetReportingAccessOverride(userId, ActualUser.Id, canReport);
				} else {
					return Json(new { success = false, error = "Your account does not have sufficient privileges to perform this action." });
				}
				return Json(new { success = true });
			} catch (Exception ex) {
				Logger.LogError(ex);
				return Json(new { success = false, error = ex.Message });
			}
		}

		#endregion


	}
}
