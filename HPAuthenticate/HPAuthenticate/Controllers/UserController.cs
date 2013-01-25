using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPEntities;
using HPEntities.Dalcs;
using HPEntities.Exceptions;
using HPEntities.Entities;
using HPAuthenticate.ViewModels;
using HPEntities.Validators;
using System.Configuration;
using HPAuthenticate.Helpers;
using libMatt.Converters;
using HPEntities.Helpers;
using HPEntities.Entities.Enums;
using System.Text.RegularExpressions;
using System.ComponentModel.DataAnnotations;
using HPAuthenticate.Security;

namespace HPAuthenticate.Controllers {
	public class UserController : ApplicationController {

		#region Helpers


		private string GetConfirmationUrl(string confirmationCode) {
			return Url.Action("ConfirmEmail", "User", new { code = confirmationCode }, Request.Url.Scheme);
		}

		private string GetConfirmationUrl() {
			return Url.Action("ConfirmEmail", "User", new { }, Request.Url.Scheme);
		}

		/// <summary>
		/// Returns a redirect to the specified URI, or if it is blank,
		/// returns a View(defaultView).
		/// </summary>
		/// <param name="uri"></param>
		/// <param name="defaultView"></param>
		/// <returns></returns>
		private ActionResult RedirectBack(string uri, string defaultView) {
			if (!string.IsNullOrEmpty(uri)) {
				return Redirect(uri);
			}
			return View(defaultView);
		}


		#endregion

		//
		// GET: /User/

		public ActionResult Index() {
			return RedirectToAction("Details");
		}

		[HttpPost]
		public ActionResult ActAs(FormCollection coll) {
			if (ActualUser == null) {
				return RedirectToRoute("Login");
			}
			if (!ActualUser.IsAdmin) {
				this.FlashError("Your account does not have sufficient privileges to perform this action.");
				return RedirectBack(coll["return_uri"], "Details");
			}

			var actAsUserId = (coll["cancel_act_as"].ToBoolean() ? new int?() : coll["act_as_user_id"].ToInteger());
			ActAs(actAsUserId.GetValueOrDefault());
			new UserDalc().ActAs(ActualUser.Id, actAsUserId);

			return RedirectBack(coll["return_uri"], "Details");
		}

		// GET: /User/AddProperty
		public ActionResult AddProperty() {
			var vm = new NewPropertyViewModel() {
				CurrentUserDisplayName = ActingUser.DisplayName
			};
			return View(vm);
		}

		[HttpPost]
		public ActionResult AddProperty(NewPropertyViewModel vm) {

			var propDescValidationErrors = new List<string>();
			// Construct property descriptions manually.
			if (!string.IsNullOrWhiteSpace(Request["property_descs"])) {
				foreach (var desc in Request["property_descs"].Split(',')) {
					// If we have to save a new owner to the database, we'll need his property descriptions populated.
					// Note the UrlDecode - this is because the properties are added to a hidden field in javascript 
					// and escaped there.
					var ar = HttpUtility.UrlDecode(desc).Split('|');
					if (ar.Length < 4) {
						propDescValidationErrors.Add("Invalid property description: '" + HttpUtility.HtmlEncode(desc) + "'");
					}

					// Note the join on pipe -- this permits descriptions to contain pipe characters.
					// Descriptions are the only field likely to have them at all - custom/parcel ID are
					// integers, and no county in its right mind would name itself with a pipe character.
					var p = new Property(ar[0].TryToInteger(), ar[1], ar[2], string.Join("|", ar.Skip(3).ToArray()));

					// MWinckler.20111101: Nobody is allowed to add new properties, at least for now.
					// Therefore, if it's marked as new (i.e. the user tried to inject it into the page),
					// ignore it.
					if (!p.IsNew) {
						vm.Properties.Add(p);
					}
				}
			}

			ModelState.Clear();
			// Refresh validation on the PropertyDescriptions, since
			// MVC has already automagically validated the model and
			// found property descriptions to be lacking.
			TryValidateModel(vm);
			foreach (var e in propDescValidationErrors) {
				ModelState.AddModelError("Properties", e);
			}

			var propDalc = new PropertyDalc();
			// If properties are already associated with the account, show an error.
			foreach (var property in vm.Properties) {
				if (propDalc.IsPropertyAssociated(ActingUser.Id, property.ParcelId, property.County)) {
					ModelState.AddModelError("Properties", "One or more of these properties are already associated with your account. If you wish to change roles, please delete the existing property from your account and add it again with the different role.");
					break;
				}
			}

			if (ModelState.IsValid && vm.RequireAddress) {
				// Validate phone numbers as well.
				var ar = vm.OwnerPhone.Split('x');
				if (!TryValidateModel(new PhoneNumber(ar[0], ar.Length > 1 ? ar[1] : ""), "Owner")) {
					// Put the validation errors into a single "OwnerPhone" key on the modelstate.
					if (ModelState.ContainsKey("Owner.Number")) {
						ModelState.AddModelError("OwnerPhone", ModelState["Owner.Number"].Errors[0].ErrorMessage);
					}
					if (ModelState.ContainsKey("Owner.Extension")) {
						ModelState.AddModelError("OwnerPhone", ModelState["Owner.Extension"].Errors[0].ErrorMessage);
					}
				}
			}

			if (ModelState.IsValid) {
				// Add the property description.
				foreach (var desc in vm.Properties) {
					/* MWinckler.20111101: No more adding new properties for now
					if (desc.IsNew) {
						// Create a new property record with this user as the owner, then associate it.
						// In order to do this, the user must be an owner. We've checked for that above.
						desc.CustomId = propDalc.CreateProperty(desc, user);
					}
					*/
					if (!string.IsNullOrWhiteSpace(desc.ParcelId)) {
						propDalc.UpdateAppraisalRollDataAsNecessary(ActingUser.Id, desc.ParcelId, desc.County, vm.OwnerMailingAddress, vm.OwnerCity, vm.OwnerState, vm.OwnerZip, vm.OwnerPhone);
					}
					// Associate the existing property to this user as an operator.
					propDalc.AssociateProperty(ActingUser, desc, PropertyRole.authorized_producer, true, vm.ProductionTypes, vm.IsDataWrong);
				}

				this.FlashInfo("Property added.");
			} else {
				this.FlashError("Validation errors occured while trying to save the property.");
				vm.CurrentUserDisplayName = ActingUser.DisplayName;
				return View(vm);
			}
			return RedirectToAction("Details");
		}


		[HttpPost]
		public ActionResult DeleteProperty(FormCollection form) {
			new PropertyDalc().DissociateProperty(ActingUser, form["clientPropertyId"].ToInteger());

			this.FlashInfo("Property removed.");
			return RedirectToAction("Details");
		}

		// I don't know the "MVC Way" to do this. I need the user controller and admin
		// controller to share the same view, passing it different models, and also
		// each controller needs to reference the view multiple times. I think declaring
		// a constant view name to user is better than including the string multiple times
		// in both controllers.
		const string CREATE_VIEWNAME = "CreateUser";

		//
		// GET: /User/Create

		[AllowAnonymous]
		public ActionResult Create() {
			return View(CREATE_VIEWNAME, new UserViewModel(new User(), UserEditMode.create));
		}

		//
		// POST: /User/Create

		[HttpPost]
		[AllowAnonymous]
		public ActionResult Create(UserViewModel uvm) {
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
				UserDalc udalc = new UserDalc();
				try {
					string confirmationCode;
					User existingUser = null;

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
						success = udalc.AssociateExistingUser(existingUser, uvm.User, out confirmationCode);
					} else {
						success = udalc.CreateUser(uvm.User, uvm.User.Password, out confirmationCode);
					}

					if (success) {
						if (Config.Instance.RequireAccountConfirmation) {
							var confirmationUrl = GetConfirmationUrl(confirmationCode);

							var msg = @"
Thank you for creating an account for the High Plains Underground Water Conservation District. In order to activate your new account, please follow the link below:

" + confirmationUrl + @"

You may need to copy and paste the link into your browser. If that doesn't work, go to:

" + GetConfirmationUrl() + @"

and enter the following code:

" + confirmationCode + @"

Thanks again!

HPWD Staff";
							if (MailHelper.Send(uvm.User.Email, "Action Required: Confirm your High Plains account", msg)) {
								this.FlashInfo("Account created! A confirmation code has been emailed to you. Please follow the instructions in that email to activate your account.");
							} else {
								try {
									MailHelper.NotifyAdmins("User account created but sending confirmation code failed! Code: " + confirmationCode);
								} catch (Exception ex) {
									Logger.LogMailError(ex);
								}
								this.FlashError("Your account was created, but we were unable to send a confirmation code to your email address. Admins have been notified of this problem.");
							}
							return RedirectToAction("ConfirmEmail");
						} else {
							this.FlashInfo("Account created successfully!");
							SessionController.LoginUser(uvm.User.Email, false, Response);
							return RedirectToAction("Details");
						}
					}

				} catch (HPEntities.Exceptions.ValidationException ex) {
					foreach (var err in ex.ValidationErrors) {
						ModelState.AddModelError("", err);
					}
					this.FlashError("Errors occurred while trying to create your account.");
				}
			}
			return View(CREATE_VIEWNAME, uvm);
		}



		//
		// GET: /User/Edit/5

		public ActionResult Edit(int id) {
			return View();
		}


		public ActionResult Details() {
			return View(new UserViewModel(ActingUser, UserEditMode.edit));
		}

		[HttpPost]
		public ActionResult Details(UserViewModel uvm) {
			// TODO: It'd be nice if this was automagically set like other properties.
			// I don't know why it isn't.
			uvm.EditMode = UserEditMode.edit;

			string vError, flashMsg;
			if (!Helpers.UserHelper.PopulatePhoneNumbers(uvm, Request, out vError, out flashMsg)) {
				if (!string.IsNullOrEmpty(vError)) {
					ModelState.AddModelError("PhoneNumbers", vError);
				}
				if (!string.IsNullOrEmpty(flashMsg)) {
					this.FlashError(flashMsg);
				}
				return View(uvm);
			}
			ModelState.Clear();
			TryValidateModel(uvm);

			if (uvm.User.Id != ActingUser.Id) {
				// Fishy! This user is trying to submit changes for a different user.
				ModelState.AddModelError("access_denied", "You do not have permission to take this action.");
			}


			if (ModelState.IsValid) {
				try {
					new UserDalc().SaveUser(uvm.User);
					this.FlashInfo("Saved changes.");
				} catch (Exception ex) {
					this.FlashError("An error occurred while trying to save changes - profile not updated.");
					Logger.LogError(ex);
				}
			}

			return View(uvm);
		}

		[HttpPost]
		public ActionResult EditEmail(FormCollection form) {
			var newAddress = form["Email"];

			if (ValidationHelper.IsValidEmail(newAddress)) {
				var confirmationCode = new UserDalc().ChangeUserEmail(ActingUser.Id, newAddress);
				if (string.IsNullOrEmpty(confirmationCode)) {
					this.FlashError("Unable to update email address. Administrators have been notified of the problem.");
				} else {

					if (!SendConfirmationCode(newAddress, confirmationCode)) {
						// Something has gone horribly wrong. Revert the email change.
						new UserDalc().ChangeUserEmail(ActingUser.Id, ActingUser.Email);
						Logger.LogError(string.Format("Unable to change email address for user {0} ({1}, {2}); requested address: {3}",
														ActingUser.Id.ToString(),
														ActingUser.DisplayName,
														ActingUser.Email,
														newAddress)
										);

						this.FlashError("An error occurred while trying to change your email address. Please try again later.");
					} else {
						this.FlashInfo("Email changed and confirmation code sent. Please check your email, then confirm your address below.");
						ActingUser.Email = newAddress;
						SessionController.Logout(Session);
						return RedirectToAction("ConfirmEmail", "User");
					}
				}
			} else {
				ModelState.AddModelError("Email", "Please enter a valid email address.");
				this.FlashError("Unable to update email address: please make sure you entered a valid email address.");
			}
			return View("Details", new UserViewModel(ActingUser, UserEditMode.edit));
		}

		[AllowAnonymous]
		public ActionResult ConfirmEmail(FormCollection coll) {
			ViewBag.Confirmed = false;
			if (!string.IsNullOrEmpty(Request["code"])) {
				// Look up the provided code and attempt to confirm it.
				int userId;
				string username;
				if (new UserDalc().ConfirmEmail(Request["code"].Trim(), out userId, out username)) {
					SessionController.LoginUser(username, false, Response);
					ViewBag.Confirmed = true;
				} else {
					this.FlashError("Invalid confirmation code.");
				}
			} else if (!string.IsNullOrWhiteSpace(Request["email_address"])) {
				// Attempt to resend the code.
				SendConfirmationCode(Request["email_address"], new UserDalc().GetConfirmationCode(Request["email_address"]));
				// Display success regardless of what happens; this prevents malicious
				// users from fishing for email addresses via this form.
				this.FlashInfo("Confirmation code sent.");
			}
			return View();
		}

		//
		// GET: /User/Delete/5
		/*
		public ActionResult Delete(int id) {
			return View();
		}
		*/

		//
		// POST: /User/Delete/5
		/*
		[HttpPost]
		public ActionResult Delete(int id, FormCollection collection) {
			try {
				// TODO: Add delete logic here

				return RedirectToAction("Index");
			} catch {
				return View();
			}
		}
		*/

		#region Helpers

		private bool SendConfirmationCode(string email, string code) {
			if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code)) {
				return false;
			}
			var confirmationUrl = GetConfirmationUrl(code);
			var msgBody = @"
Hi,

You recently updated your email address on the High Plains Underground Water Conservation District. To complete this process, please click the link below:

" + confirmationUrl + @"

You may need to copy and paste the link into your web browser. If the link doesn't work, go to:

" + GetConfirmationUrl() + @"

and enter the following code:

" + code + @"

If you did not request this change to your email address, please ignore this message.";

			return MailHelper.Send(email, "Action Required: HPWD Account Email Address Change", msgBody);
		}

		#endregion


		#region Ajax Support

		/// <summary>
		/// Gets all owners whose names contain the specified term.
		/// This function returns an array of AutocompleteResult objects,
		/// with labels and values.
		/// 
		/// Authorization required.
		/// </summary>
		/// <param name="term"></param>
		/// <returns></returns>
		public JsonResult FindOwners(string term) {
			return Json(new OwnerDalc().GetOwnersByName(term, Config.Instance.AutocompleterResultLimit).ToArray(), JsonRequestBehavior.AllowGet);
		}

		// Authorization required.
		public JsonResult FindProperties(string ownerName, string address, string city, string state, string zip) {
			return Json(new PropertyDalc().GetPropertiesByOwner(ownerName, address, city, state, zip), JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// This method restricts results to records that do not yet have a password associated with them.
		/// It will NOT return ALL matching records from the Clients table; cf. FindAllClients.
		/// </summary>
		/// <param name="term"></param>
		/// <returns></returns>
		[AllowAnonymous] // Anonymity allowed due to usage from "create user" page
		public JsonResult FindClients(string term) {
			return Json(new UserDalc().FindUsersByName(term, true, Config.Instance.AutocompleterResultLimit).Select(x => new UserViewModelMinimal(x)), JsonRequestBehavior.AllowGet);
		}

		/// <summary>
		/// Returns all clients matching the name specified.
		/// 
		/// Authorization required.
		/// </summary>
		/// <param name="term"></param>
		/// <returns></returns>
		public JsonResult FindAllClients(string term) {
			return Json(new UserDalc().FindUsersByName(term, false, Config.Instance.AutocompleterResultLimit).Select(x => new UserViewModelMinimal(x)), JsonRequestBehavior.AllowGet);
		}

		// Authorization required.
		public JsonResult FindClientsByNameOrEmail(string term) {
			return Json(new UserDalc().FindUsersByNameOrEmail(term, Config.Instance.AutocompleterResultLimit).Select(x => new UserViewModelMinimal(x)), JsonRequestBehavior.AllowGet);
		}

		[AllowAnonymous] // Used anonymously from create user page
		public JsonResult IsEmailAvailable(string email) {
			return Json(new UserDalc().IsUsernameAvailable(email));
		}

		#endregion
	}
}
