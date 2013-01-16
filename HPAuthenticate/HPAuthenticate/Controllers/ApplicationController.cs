using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HPEntities;
using HPEntities.Dalcs;
using libMatt.Converters;
using System.Configuration;
using System.Web.Security;

namespace HPAuthenticate.Controllers
{
    public class ApplicationController : Controller {

		protected override void Initialize(System.Web.Routing.RequestContext requestContext) {
            base.Initialize(requestContext);
            if (User != null && User.Identity != null) {
				ViewBag.CurrentUsername = User.Identity.Name;
			}
			ViewBag.IsAdmin = false;
			ViewBag.Acting = false;
			ViewBag.IsReportingDeployed = ConfigurationManager.AppSettings["is_reporting_deployed"].ToBoolean();
        }

		protected override void OnActionExecuting(ActionExecutingContext filterContext) {
			// Certain variables are required regardless of the page - initialize them here.
			ViewBag.CurrentUser = _actualUser = SessionController.GetCurrentUser(HttpContext);
			ViewBag.IsAdmin = false;
			ViewBag.Acting = false;

			if (_actualUser != null) {
				if (_actualUser.IsAdmin && _actualUser.ActingAsUserId.HasValue) {
					_actingUser = new UserDalc().GetUser(_actualUser.ActingAsUserId.Value);
				} else {
					_actingUser = _actualUser;
				}
				ViewBag.IsAdmin = _actualUser.IsAdmin;
				ViewBag.ActingUser = _actingUser;
				ViewBag.ActualUser = _actualUser;
				ViewBag.Acting = _actualUser.Id != _actingUser.Id;


			}

            
            // If the site is set offline, unless the user is an admin,
            // redirect to a maintenance page.
            string appOfflineUrl = ConfigurationManager.AppSettings["app_offline"];
            if (filterContext.ActionDescriptor.ActionName != "Login" && !string.IsNullOrEmpty(appOfflineUrl) && (_actualUser == null || !_actualUser.IsAdmin)) {
                // Redirect
                filterContext.Result = new RedirectResult(appOfflineUrl);
            }

			base.OnActionExecuting(filterContext);
		}

		protected override void OnActionExecuted(ActionExecutedContext filterContext) {

			base.OnActionExecuted(filterContext);
		}


		private User _actualUser;
		private User _actingUser;


/*
		protected User Authorize() {
			// Slightly redundant, but ensures IsAdmin is set to a safe default
			// before proceeding.
			ViewBag.IsAdmin = false;

			_actualUser = _actingUser = SessionController.GetCurrentUser(HttpContext);
			if (_actualUser == null) {
				// Access denied.
				this.FlashWarning("You do not have access to that page; please login first.");
				return null;
			}

			ViewBag.IsAdmin = _actualUser.IsAdmin;

			if (_actualUser.IsAdmin && _actualUser.ActingAsUserId.HasValue) {
				ActAs(_actualUser.ActingAsUserId.Value);
			}

			ViewBag.ActingUser = _actingUser;
			ViewBag.ActualUser = _actualUser;
			ViewBag.Acting = _actualUser.Id != _actingUser.Id;
			return _actingUser;
		}
*/
		protected void ActAs(int userId) {
			if (userId > 0 && (_actingUser == null || _actingUser.Id != userId)) {
				_actingUser = new UserDalc().GetUser(userId);
				ViewBag.ActingUser = _actingUser;
			}
		}

		protected User ActualUser {
			get {
				return _actualUser;
			}
		}

		protected User ActingUser {
			get { return _actingUser; }
		}


    }
}
