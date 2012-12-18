using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Security;
using HPEntities.Dalcs;

namespace HPAuthenticate.Security {
	public class AccountRoleProvider: RoleProvider {

		public override bool IsUserInRole(string username, string roleName) {
			switch (roleName.ToLower()) {
				case "admin":
					return new UserDalc().IsUserAdmin(username);
				default:
					// The role doesn't exist, so the user can't be in it.
					return false;
			}
		}

		#region Unused/unimplemented methods/properties

		public override void AddUsersToRoles(string[] usernames, string[] roleNames) {
			throw new NotImplementedException();
		}

		public override string ApplicationName {
			get {
				throw new NotImplementedException();
			}
			set {
				throw new NotImplementedException();
			}
		}

		public override void CreateRole(string roleName) {
			throw new NotImplementedException();
		}

		public override bool DeleteRole(string roleName, bool throwOnPopulatedRole) {
			throw new NotImplementedException();
		}

		public override string[] FindUsersInRole(string roleName, string usernameToMatch) {
			throw new NotImplementedException();
		}

		public override string[] GetAllRoles() {
			throw new NotImplementedException();
		}

		public override string[] GetRolesForUser(string username) {
			// We only have one role - admin - so check if the user's an admin,
			// and if so, return that, else return an empty array
			return new UserDalc().IsUserAdmin(username)
				? new string[] { "admin" }
				: new string[] { };
		}

		public override string[] GetUsersInRole(string roleName) {
			throw new NotImplementedException();
		}



		public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames) {
			throw new NotImplementedException();
		}

		public override bool RoleExists(string roleName) {
			throw new NotImplementedException();
		}

		#endregion

	}
}