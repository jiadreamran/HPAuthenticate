using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPEntities;

namespace HPAuthenticate.ViewModels {
	public class AdminCreateUserViewModel: UserViewModel {

		public AdminCreateUserViewModel() : base(new User(), HPEntities.Entities.Enums.UserEditMode.admin_create) { }

		public AdminCreateUserViewModel(User user): base(user, HPEntities.Entities.Enums.UserEditMode.admin_create) {
		}

		public new string Email { get; set; }
		public new string Password { get; set; }
		public new string PasswordConfirmation { get; set; }

	}
}