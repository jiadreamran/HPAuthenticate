using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPEntities.Dalcs;
using System.Web.Security;
using HPEntities;
using HPEntities.Exceptions;
using HPAuthenticate.Helpers;
using HPEntities.Validators;
using HPEntities.Helpers;
using libMatt.Converters;
using HPAuthenticate.Security;

namespace HPAuthenticate.Controllers
{
    public class SessionController : ApplicationController
    {
        //
        // GET: /Session/
		[AllowAnonymous]
        public ActionResult Login()
        {
            return View();
        }

		[HttpPost]
		[AllowAnonymous]
		public ActionResult Login(FormCollection coll) {
			// Attempt to retrieve the user identified by username supplied
			if (!string.IsNullOrEmpty(coll["email"])) {
				if (Membership.ValidateUser(coll["email"], coll["password"])) {
					var user = (User)Membership.GetUser(coll["email"]);
					if (!(user.IsEmailConfirmed || !Config.Instance.RequireAccountConfirmation)) {
						this.FlashError("Your email address is not yet confirmed. Please enter your confirmation code below.");
						return RedirectToAction("ConfirmEmail", "User");
					} else {
						LoginUser(user.Email, coll["remember_me"].ToBoolean(), Response);
						if (!string.IsNullOrEmpty(coll["redirect_to"])) {
							return Redirect(Url.Content(coll["redirect_to"]));
						} else {
							return RedirectToAction("Details", "User");
						}
					}
				}
			}

			// If execution arrives here, login failed.
			ModelState.AddModelError("", "Invalid username/password.");
			return View();
		}

		[AllowAnonymous]
		public static void Logout(HttpSessionStateBase session) {
			// Destroy session and remove auth cookies
			session.Clear();
			session.Abandon();
			FormsAuthentication.SignOut();
		}


		[AllowAnonymous]
		public ActionResult Logout() {
			Logout(Session);
			return RedirectToAction("Index", "Home");
		}


		[AllowAnonymous]
		public ActionResult ResetPassword(string code) {
			ViewBag.NeedsPasswordForm = false;
			if (!string.IsNullOrEmpty(code)) {
				// The user has a code and is ready to choose a new password.
				// Try to look it up.
				var uid = new UserDalc().GetUserIdByPasswordResetCode(code);
				if (uid > 0) {
					ViewBag.NeedsPasswordForm = true;
				} else {
					this.FlashError("Invalid password reset code.");
				}
			}
			return View();
		}

		[HttpPost]
		[AllowAnonymous]
		public ActionResult ResetPassword(FormCollection form) {
			ViewBag.NeedsPasswordForm = false;
			if (!string.IsNullOrEmpty(form["new_password"])) {

				// Validate it.
				List<string> errors;
				if (ValidationHelper.IsValidPassword(Request["new_password"], Request["new_password_confirmation"], out errors)) {
					var udalc = new UserDalc();
					var uid = udalc.GetUserIdByPasswordResetCode(Request["code"]);
					if (uid == 0) {
						this.FlashError("Invalid password reset code.");
						ViewBag.NeedsPasswordForm = true;
					} else {
						var user = udalc.GetUser(uid);
						user.Password = Request["new_password"];
						user.PasswordConfirmation = Request["new_password_confirmation"];
						udalc.SaveUser(user);
						udalc.ClearPasswordResetCode(uid);
						LoginUser(user.Email, false, Response);
						this.FlashInfo("Password successfully changed. You are now logged in.");
						return RedirectToAction("Details", "User");
					}
				} else {
					this.FlashError(string.Join(" ", errors));
					ViewBag.NeedsPasswordForm = true;
				}
			} else if (!string.IsNullOrEmpty(Request["user_entered_code"])) {
				return ResetPassword(Request["user_entered_code"]);
			} else {
				var emailAddress = string.Copy(Request["email"]);
				if (!string.IsNullOrEmpty(emailAddress)) {
					string code;
					try {
						code = new UserDalc().SavePasswordResetCode(emailAddress);
					} catch (ValidationException ex) {
						Logger.LogError(ex);
						this.FlashError(ex.Message);
						return View();
					}
					var link = Url.Action("ResetPassword", "Session", new { code = code }, Request.Url.Scheme);
					var msgBody = @"To finish changing your password, please follow the link below:

" + link + @"

You may need to copy and paste it into your browser. If the link doesn't work, go to:

" + Url.Action("ResetPassword", "Session", new { }, Request.Url.Scheme) + @"

and enter the following code:

" + code + @"

If you did not request this password reset, please disregard this email.";
					if (!string.IsNullOrEmpty(code) && MailHelper.Send(emailAddress, "High Plains account password reset", msgBody)) {
						this.FlashInfo("Password reset email sent.");
						ViewBag.ResetComplete = true;
					} else {
						this.FlashError("Unable to send password reset email. Check to make sure your address is correct.");
					}
				} else {
					this.FlashError("Please enter your email address below.");
				}
			}
			return View();
		}

		[AllowAnonymous]
		public static void LoginUser(string username, bool rememberLogin, HttpResponseBase response) {
			if (string.IsNullOrWhiteSpace(username)) {
				throw new ArgumentNullException("username", "Username cannot be blank.");
			}
			if (response == null) {
				throw new ArgumentNullException("request", "HttpRequestWrapper cannot be null.");
			}
			// Set auth cookie
			FormsAuthentication.SetAuthCookie(username, rememberLogin);
			response.Cookies[FormsAuthentication.FormsCookieName].HttpOnly = false;
		}


		internal static User GetCurrentUser(HttpContextBase httpContext) {
			var cookie = httpContext.Request.Cookies[FormsAuthentication.FormsCookieName];
			if (cookie != null) {
				var ticket = FormsAuthentication.Decrypt(cookie.Value);
				if (ticket != null) {
					return new UserDalc().GetUser(ticket.Name);
				}
			}
			return null;
		}


	}
}
