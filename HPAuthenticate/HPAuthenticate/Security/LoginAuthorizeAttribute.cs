using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace HPAuthenticate.Security {
	public sealed class LoginAuthorizeAttribute: AuthorizeAttribute {
		public override void OnAuthorization(AuthorizationContext filterContext) {
			bool skipAuthorization = filterContext.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)
			|| filterContext.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true);
			if (!skipAuthorization) {
				base.OnAuthorization(filterContext);
			}
		}


	}
}