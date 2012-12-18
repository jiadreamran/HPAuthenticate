using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPEntities.Exceptions;
using libMatt.Converters;
using System.Data.SqlClient;
using System.Data;
using HPEntities;
using HPEntities.Entities;
using System.Security.Cryptography;
using System.Configuration;
using HPEntities.Helpers;
using HPEntities.Entities.Enums;
using System.Text.RegularExpressions;

namespace HPEntities.Dalcs {
	public class UserDalc : AuthDalcBase {

		#region Helpers

		/// <summary>
		/// Creates and returns a unique confirmation code for emails and the like.
		/// </summary>
		/// <returns></returns>
		private string GenerateConfirmationCode() {
			var rng = new RNGCryptoServiceProvider();
			var code = new byte[20];
			rng.GetBytes(code);
			return string.Join("", code.Select(x => x.ToString("x")));
		}

		private string GetHashedPassword(string pwd) {
			return Security.BCrypt.HashPassword(pwd, Security.BCrypt.GenerateSalt());
		}


		public Dictionary<int, string> GetAvailableCourtesyTitles() {
			return (from row in ExecuteDataTable("select CourtesyTitleID, CourtesyTitle from CourtesyTitles;").AsEnumerable()
					select new {
						id = row["CourtesyTitleID"].ToInteger(),
						title = row["CourtesyTitle"].GetString()
					}).ToDictionary(
						x => x.id,
						x => x.title
					);
		}

		/// <summary>
		/// Looks up an existing confirmation code based on email address.
		/// </summary>
		/// <param name="email"></param>
		/// <returns></returns>
		public string GetConfirmationCode(string email) {
			return ExecuteScalar(@"
select EmailConfirmationCode
from Clients
where
	EmailAddress like @email;", new Param("@email", email)).GetString();
		}

		#endregion


		#region Creates

		/// <summary>
		/// This method creates a new user using the values in the provided User object.
		/// </summary>
		/// <param name="user">(User) The user data to save.</param>
		/// <param name="password">(string) The user's desired password, in plaintext.</param>
		/// <exception cref="ValidationException">
		///	A ValidationException is thrown is the User's values do not satisfy business rules
		///	or the user cannot be saved for some reason. The ValidationErrors property will be 
		///	populated with specific details about the failure.</exception>
		/// <returns>(bool) True on success, else false.</returns>
		public bool CreateUser(User user, string password, out string confirmationCode) {
			confirmationCode = "";

			// The user validation does not include checking to see whether the username is
			// available. Do so now.

			if (IsUsernameAvailable(user.Email)) {
				// User is now valid. Save.

				// Password may not exist if user was created via admin interface.
				var passwordHashed = string.IsNullOrEmpty(password) ? "" : GetHashedPassword(password);
				confirmationCode = GenerateConfirmationCode();
				using (var trans = BeginTransaction()) {

					// Ensure that email address is still not taken.
					bool isEmailTaken = !string.IsNullOrEmpty(user.Email) 
											&& ExecuteScalar("select 1 from Clients where EmailAddress = @email;",
															new Param("@email", user.Email)).ToBoolean();
					if (isEmailTaken) {
						throw new ValidationException("Username is already taken.");
					}

					try {
						user.Id = ExecuteScalar(@"
insert into Clients (
	FirstName,
	MiddleInitial,
	LastNameOrCompany,
	Suffix,
	PreferredName,
	EmailAddress,
	PasswordHashed,
	IsEmailConfirmed,
	EmailConfirmationCode
) values (
	@firstName,
	@mi,
	@lastName,
	@suffix,
	@preferredName,
	@email,
	@passwordHashed,
	@isConfirmed,
	@code
);

select @@IDENTITY;",
							new Param("@firstName", user.FirstName.OrDbNull()),
							new Param("@mi", user.MiddleInitial.OrDbNull()),
							new Param("@lastName", user.LastNameOrCompany),
							new Param("@suffix", user.Suffix.OrDbNull()),
							new Param("@preferredName", user.PreferredName.OrDbNull()),
							new Param("@email", user.Email.OrDbNull()),
							new Param("@passwordHashed", passwordHashed),
							new Param("@isConfirmed", !Config.Instance.RequireAccountConfirmation),
							new Param("@code", confirmationCode)
						).ToInteger();
					} catch (SqlException ex) {
						trans.Rollback();
						if (ex.Message.StartsWith("Cannot insert duplicate key row")
							&& ex.Message.Contains("IX_Clients_DisplayName")) {
							throw new ValidationException("Sorry, somebody else has already taken the preferred and last names you've entered. You'll have to choose a different one.");
						}
						throw new ValidationException(ex.Message);
					}
					if (user.Id <= 0) {
						// Something failed.
						trans.Rollback();
						return false;
					}

					var sillydalc = new SillyAbstractionDalc();
					foreach (var addr in user.Addresses) {
						// Save the physical address.
						ExecuteNonQuery(@"
insert into ClientAddresses (
	ClientID,
	AddressTypeID,
	Address,
	CityID,
	PostalCode
) values (
	@clientId,
	2,
	@address,
	@cityId,
	@zip
);",
							new Param("@clientId", user.Id),
							new Param("@address", addr.MailingAddress),
							new Param("@cityId", sillydalc.GetCityId(addr.City, addr.State)),
							new Param("@zip", addr.Zip)
						);
					}
					// And now, save the phone numbers!
					foreach (var phoneNumber in user.PhoneNumbers) {
						ExecuteNonQuery(@"
insert into ClientPhoneNumbers(
	ClientID,
	PhoneTypeID,
	PhoneNumber,
	PhoneExtension
) values (
	@clientId,
	@phoneTypeId,
	@number,
	@extension
);",
							new Param("@clientId", user.Id),
							new Param("@phoneTypeId", phoneNumber.PhoneTypeId),
							new Param("@number", phoneNumber.Number),
							new Param("@extension", phoneNumber.Extension.OrDbNull())
						);
					}

					trans.Commit();
				}
				return true;

			} else {
				throw new ValidationException("Email address is already taken; please select a different one.");
			}
			return false;
		}

		public bool CreateUser(User user, string password) {
			string throwaway;
			return CreateUser(user, password, out throwaway);
		}


		#endregion


		#region Retrievals

		/// <summary>
		/// Finds and returns the specified user. If the given username does not exist,
		/// this function returns null.
		/// </summary>
		/// <param name="email">(string) The user to retrieve.</param>
		/// <returns>(User) The user object corresponding to the given username, or null if no such user exists.</returns>
		public User GetUser(string email) {
			// This is stupid. Should have standardized on user ID before.
			// TODO: Fix this inanity.
			return GetUser(ExecuteScalar(@"select ClientID from Clients where EmailAddress = @email", new Param("@email", email)).ToInteger());
		}

		/// <summary>
		/// Gets the user (client) associated with the given ID.
		/// PERFORMANCE: Optimize this to use fewer round-trip queries.
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		public User GetUser(int userId) {
			var dt = ExecuteDataTable(@"
select
	c.ClientID,
	c.CourtesyTitleID,
	c.FirstName,
	c.MiddleInitial,
	c.LastNameOrCompany, 
	c.Suffix,
	c.PreferredName,
	c.EmailAddress,
	c.PasswordHashed,
	c.IsEmailConfirmed,
	c.IsAdmin,
	c.ActingAsClientId
from Clients c
where ClientID = @id;", new Param("@id", userId));

			if (dt.Rows.Count == 0) {
				return null;
			}

			var r = dt.Rows[0];
			var user = new User(r["ClientID"].ToInteger()) {
				CourtesyTitleId = r["CourtesyTitleId"].ToInteger(),
				FirstName = r["FirstName"].GetString(),
				MiddleInitial = r["MiddleInitial"].GetString(),
				LastNameOrCompany = r["LastNameOrCompany"].GetString(),
				Suffix = r["Suffix"].GetString(),
				PreferredName = r["PreferredName"].GetString(),
				Email = r["EmailAddress"].GetString(),
				PasswordHashed = r["PasswordHashed"].GetString(),
				IsEmailConfirmed = r["IsEmailConfirmed"].ToBoolean(),
				IsAdmin = r["IsAdmin"].ToBoolean(),
				ActingAsUserId = r["ActingAsClientId"].TryToInteger()
			};

			var sillyDalc = new SillyAbstractionDalc();
			// Look up user mailing address(es).
			dt = ExecuteDataTable(@"
select
	ClientID,
	AddressID,
	Address,
	CityID,
	PostalCode
from ClientAddresses
where ClientID = @id
order by AddressID desc;", new Param("@id", user.Id));
			user.Addresses = (from row in dt.AsEnumerable()
							  select new Address() {
								  Id = row["AddressID"].ToInteger(),
								  MailingAddress = row["Address"].GetString(),
								  CityId = row["CityID"].ToInteger(),
								  Zip = row["PostalCode"].GetString()
							  }).ToList();

			// Look up phone numbers
			dt = ExecuteDataTable(@"
select
	PhoneNumberID,
	PhoneTypeID,
	PhoneNumber,
	PhoneExtension
from ClientPhoneNumbers
where
	ClientID = @id;", new Param("@id", user.Id));
			user.PhoneNumbers = (from row in dt.AsEnumerable()
								 select new PhoneNumber(
									 row["PhoneNumberID"].ToInteger(),
									 row["PhoneTypeID"].ToInteger(),
									 row["PhoneNumber"].GetString(),
									 row["PhoneExtension"].GetString()
								)).ToList();

			// user.PropertyDescriptions = new OwnerDalc().GetAssociatedProperties(user.Email);
			return user;
		}

		/// <summary>
		/// Finds all users (clients) where lastname starting with the given term.
		/// 
		/// This function will ONLY return Client records that have not yet been
		/// claimed by a user account, i.e. where PasswordHashed is null. This is 
		/// a requirement inferred by MWinckler, not part of the explicit spec.
		/// 
		/// PERFORMANCE: Optimize this to load in fewer queries.
		/// </summary>
		/// <param name="lastName"></param>
		/// <returns></returns>
		public List<User> FindUsersByName(string lastName, bool onlyAvailableOnes, int limit) {
			var ret = new List<User>();
			var dt = ExecuteDataTable(@"
select top " + limit.ToString() + @"
	ClientID 
from Clients 
where 
	SearchName like @term
	" + (onlyAvailableOnes ?  "and PasswordHashed is null" : "") + @"
order by LastNameOrCompany asc;",
								new Param("@term", Config.Instance.FormatStringSearchTerm(Regex.Replace(lastName, "[^A-Za-z0-9]", "")))
				);
			foreach (var row in dt.AsEnumerable()) {
				// PERFORMANCE: Can combine this into the query above.
				ret.Add(GetUser(row["ClientID"].ToInteger()));
			}
			return ret;
		}

		public IEnumerable<User> FindUsersByNameOrEmail(string term, int limit) {
			var ret = new List<User>();
			var dt = ExecuteDataTable(@"
select top " + limit.ToString() + @"
	ClientID
from Clients
where
	SearchName like @term
	or EmailAddress like @term
order by LastNameOrCompany asc;", new Param("@term", Config.Instance.FormatStringSearchTerm(term.Replace(" ", "")))
				);
			return dt.AsEnumerable().Select(row => GetUser(row["ClientID"].ToInteger()));
		}




		public List<PropertyAssociation> GetAssociatedProperties(int userId) {
			var dt = ExecuteDataTable(@"
select
	cp.ClientPropertyID,
	cp.PropertyRole,
	cp.PropertyID,
	cp.ParcelID,
	cp.County,
	cp.AcceptedDisclaimer,
	cp.IsRecordVerified,
	cp.ProductionType,
	udp.Description as udp_desc,
	ar.LegalDescription as ar_desc,
	isnull(ar.OwnerName, 'Unidentified Owner') as OwnerName,
	ar.MailingAddress as Address,
	ar.MailingCity as City,
	ar.MailingState as State,
	ar.MailingZipCode as Zip,
	ar.RecordModified
from ClientProperties cp
inner join Clients c
	on c.ClientID = cp.ClientID
left join UserDefinedProperties udp
	on cp.PropertyID = udp.PropertyID
left join vwAppraisalRolls ar
	on cp.ParcelID = ar.ParcelID
	and cp.County = ar.County
where
	cp.ClientID = @clientId;", new Param("@clientId", userId));

			return (from row in dt.AsEnumerable()
					select new PropertyAssociation(
						row["ClientPropertyID"].ToInteger(),
						new Property(
							row["PropertyID"].TryToInteger(),
							row["ParcelID"].GetString(),
							row["County"].GetString(),
							(row["PropertyID"] == DBNull.Value ? row["ar_desc"].GetString() : row["udp_desc"].GetString())
						) {
							OwnerName = row["OwnerName"].GetString(),
							Address = row["Address"].GetString(),
							City = row["City"].GetString(),
							State = row["State"].GetString(),
							Zip = row["Zip"].GetString(),
							IsAppraisalRollDataModified = row["RecordModified"].ToBoolean()
						},
						row["PropertyRole"].ToEnum<PropertyRole>()
					)).ToList();

		}

		#endregion


		#region Updates


		/// <summary>
		/// Resets the user's password and returns a confirmation code to mail back to the user.
		/// 
		/// Exceptions: This function will throw a ValidationException if no user is found to match
		/// the given email address.
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		public string SavePasswordResetCode(string emailAddress) {
			var user = GetUser(emailAddress);
			if (user == null) {
				throw new ValidationException("Email address not found; please check to make sure it's correct.");
			}

			var code = GenerateConfirmationCode();
			ExecuteNonQuery(@"
update Clients
set 
	PasswordConfirmationCode = @code,
	PasswordConfirmationCreateDate = getdate()
where
	ClientId = @id;", new Param("@code", code), new Param("@id", user.Id));

			return code;
		}

		/// <summary>
		/// Clears the password confirmation code from the specified user record.
		/// </summary>
		/// <param name="userId"></param>
		public void ClearPasswordResetCode(int userId) {
			ExecuteNonQuery(@"
update Clients
set
	PasswordConfirmationCode = null,
	PasswordConfirmationCreateDate = null
where
	ClientId = @id;", new Param("@id", userId));
		}


		/// <summary>
		/// Updates a user's email address in the database and returns a confirmation code
		/// the user must enter to confirm that he owns the address.
		/// 
		/// If a failure occurs while trying to update the email address, this function will
		/// return an empty string.
		/// </summary>
		/// <param name="oldAddress"></param>
		/// <param name="newAddress"></param>
		/// <returns>(string) A confirmation code that can be used to send the user an email
		/// and have him confirm that he actually owns the new address.</returns>
		public string ChangeUserEmail(int userId, string newAddress) {
			// Update: 
			//	1. UserOwnerAssoc
			//	2. WebUsers
			string code = GenerateConfirmationCode();

			using (var trans = BeginTransaction()) {
				try {
					ExecuteNonQuery(@"
update Clients
set 
	EmailAddress = @email,
	IsEmailConfirmed = 0,
	EmailConfirmationCode = @code
where ClientID = @id;", new Param("@email", newAddress),
						new Param("@id", userId),
						new Param("@code", code)
					);
				} catch (Exception ex) {
					return string.Empty;
				}
				trans.Commit();
			}
			return code;
		}

		/// <summary>
		/// Attempts to confirm the user's email address using the provided code.
		/// If successful, populates the userId param with the user's id and returns true.
		/// </summary>
		/// <param name="code"></param>
		/// <returns></returns>
		public bool ConfirmEmail(string code, out int userId, out string username) {
			userId = 0;
			username = "";
			using (var trans = BeginTransaction()) {
				var dt = ExecuteDataTable(@"
select 
	ClientID,
	EmailAddress
from Clients
where
	EmailConfirmationCode = @code
	and IsEmailConfirmed = 0;", new Param("@code", code));
				if (dt.Rows.Count > 0) {
					userId = dt.Rows[0]["ClientID"].ToInteger();
					username = dt.Rows[0]["EmailAddress"].GetString();
					ExecuteNonQuery(@"
update Clients
set
	IsEmailConfirmed = 1,
	EmailConfirmationCode = null
where
	ClientID = @id;", new Param("@id", userId));

					trans.Commit();
					return true;
				}
			}

			return false;
		}



		/// <summary>
		/// Saves user metadata. IMPORTANT: This method does _not_ change a user's email address.
		/// To do that, call ChangeUserEmail and be prepared to handle the confirmation code it returns.
		/// </summary>
		/// <param name="user"></param>
		public void SaveUser(User user) {
			using (var trans = BeginTransaction()) {

				Func<User, object> getUpdatedPwd = u => {
					if (!string.IsNullOrEmpty(u.Password) && u.Password == u.PasswordConfirmation) {
						return GetHashedPassword(u.Password);
					}
					return DBNull.Value;
				};

				// 20111021: We now must update three different places to handle user profile data.
				ExecuteNonQuery(@"
update Clients
set
	CourtesyTitleId = @courtesyTitleId,
	FirstName = @firstName,
	MiddleInitial = @mi,
	LastNameOrCompany = @lastName,
	Suffix = @suffix,
	PreferredName = @prefName,
	PasswordHashed = ISNULL(@pwd, PasswordHashed)
where
	ClientId = @clientId;", new Param("@courtesyTitleId", user.CourtesyTitleId.HasValue && user.CourtesyTitleId.Value > 0 ? (object)user.CourtesyTitleId.Value : DBNull.Value),
						  new Param("@firstName", user.FirstName),
						  new Param("@mi", user.MiddleInitial.OrDbNull()),
						  new Param("@lastName", user.LastNameOrCompany),
						  new Param("@suffix", user.Suffix.OrDbNull()),
						  new Param("@prefName", user.PreferredName.OrDbNull()),
						  new Param("@pwd", getUpdatedPwd(user)),
						  new Param("@clientId", user.Id)
				);

				var sillyDalc = new SillyAbstractionDalc();
				// Update this user's address.
				foreach (var addr in user.Addresses) {
					// Here's where it gets a little tricky.
					// If the address type is 2, then everything's fine - save the address.
					// If the address type is 1, then everything is _not_ fine:
					//		If an address of type 2 already exists for the user, update address type 2.
					//		If the type 2 address does _not_ exist, create it.
					// HARDCODE: Warning: address type id is hardcoded here.
					ExecuteNonQuery(@"
merge ClientAddresses as target
using (
	select
		@clientId as clientId,
		@addressId as addressId,
		@address as address,
		@cityId as cityId,
		@zip as zip,
		2 as addressTypeId
) as source on (
	target.ClientId = source.clientId
	and target.AddressTypeId = source.addressTypeId
)
when matched then
	update set target.Address = source.address, target.CityID = source.cityId, target.PostalCode = source.zip
when not matched then
	insert (ClientID, Address, CityID, PostalCode, AddressTypeId) 
	values (source.clientId, source.address, source.cityId, source.zip, 2);",
							new Param("@clientId", user.Id),
							new Param("@addressId", addr.Id),
							new Param("@address", addr.MailingAddress),
							new Param("@cityId", sillyDalc.GetCityId(addr.City, addr.State)),
							new Param("@zip", addr.Zip)
						);

				}


				// Update phone numbers
				// TODO: Do this more elegantly than just deleting/re-adding them all.
				ExecuteNonQuery("delete from ClientPhoneNumbers where ClientID = @id;", new Param("@id", user.Id));

				foreach (var phone in user.PhoneNumbers) {
					ExecuteNonQuery(@"
merge ClientPhoneNumbers as target
using (
	select
		@clientId as clientId,
		@phoneNumberId as phoneNumberId,
		@phoneTypeId as phoneTypeId,
		@number as number,
		@ext as ext
) as source on (
	target.PhoneNumberID = source.phoneNumberId
)
when matched then
	update set target.PhoneTypeID = source.phoneTypeId, target.PhoneNumber = source.number, target.PhoneExtension = source.ext
when not matched then
	insert (ClientID, PhoneTypeID, PhoneNumber, PhoneExtension)
	values (source.clientId, source.phoneTypeId, source.number, source.ext);",
							new Param("@clientId", user.Id),
							new Param("@phoneNumberId", phone.Id.OrDbNull()),
							new Param("@phoneTypeId", phone.PhoneTypeId),
							new Param("@number", phone.Number),
							new Param("@ext", phone.Extension.OrDbNull())
						);
				}


				if (!string.IsNullOrEmpty(user.Password) && user.Password == user.PasswordConfirmation) {
					ExecuteNonQuery(@"
update Clients
set
	PasswordHashed = @pwd
where
	ClientId = @id;",
						new Param("@pwd", GetHashedPassword(user.Password)),
						new Param("@id", user.Id)
					);
				}

				trans.Commit();
			}

		}

		/// <summary>
		/// Sets the admin bit on the specified user id to the given value.
		/// Note that this routine will only permit @intera.com and @hpwd.com
		/// addresses to be set to admin status.
		/// </summary>
		/// <param name="userId"></param>
		/// <param name="isAdmin"></param>
		public void SetAdmin(int userId, bool isAdmin) {
			ExecuteNonQuery(@"
update Clients
set IsAdmin = @isAdmin
where 
	ClientID = @clientId
	and (
		EmailAddress like '%@hpwd.com'
		or EmailAddress like '%@intera.com'
	);", new Param("@isAdmin", isAdmin), new Param("@clientId", userId));
		}

		#endregion


		#region Deletes


		public void DeleteUser(int userId) {
			using (var trans = BeginTransaction()) {
				ExecuteNonQuery(@"
delete from ClientProperties
where ClientID = @clientId;

delete from ClientAddresses
where ClientID = @clientId;

delete from ClientPhoneNumbers
where ClientID = @clientId;

delete from Clients
where ClientID = @clientId;", new Param("@clientId", userId));
				trans.Commit();
			}
		}

		#endregion


		#region Validation

		public bool IsUsernameAvailable(string email) {
			// Permit admin creates of user accounts with blank email
			if (string.IsNullOrEmpty(email)) {
				return true;
			}
			return !ExecuteScalar("select 1 from Clients where EmailAddress like @email;", new Param("@email", email.ToLower())).ToBoolean();
		}

		#endregion


		#region Act As

		/// <summary>
		/// Removes any "act as" delegation currently in effect for the specified user.
		/// </summary>
		/// <param name="actualUserId"></param>
		public void ActAsSelf(int actualUserId) {
			ActAs(actualUserId, null);
		}

		/// <summary>
		/// Stores the user ID this user is acting as.
		/// </summary>
		/// <param name="actualUserId">(int) The user who wants to do the acting.</param>
		/// <param name="actAsUserId">(int?) The user ID to act as; if null this method clears any delegation.</param>
		public void ActAs(int actualUserId, int? actAsUserId) {
			// If this command results in acting as one's self, set the act as column null.
			if (actAsUserId.GetValueOrDefault() == actualUserId || actAsUserId.GetValueOrDefault() <= 0) {
				actAsUserId = null;
			}

			ExecuteNonQuery(@"
update Clients
set ActingAsClientId = @actAs
where ClientId = @userId;", new Param("@actAs", actAsUserId.HasValue ? (object)actAsUserId : DBNull.Value),
						  new Param("@userID", actualUserId));
		}

		#endregion



		/// <summary>
		/// Attempts to look up a user id based on the password confirmation code.
		/// If found, returns the user ID; if no user is found, this function returns 0.
		/// </summary>
		/// <param name="code"></param>
		/// <returns></returns>
		public int GetUserIdByPasswordResetCode(string code) {
			return ExecuteScalar(@"
select ClientID
from Clients
where
	PasswordConfirmationCode = @code
	and getdate() < dateadd(minute, @durationMinutes, PasswordConfirmationCreateDate);",
					new Param("@code", code),
					new Param("@durationMinutes", Config.Instance.PasswordConfirmationCodeDuration.TotalMinutes)
				).ToInteger();
		}

		/// <summary>
		/// Associates the given user data with the existing user in the database - that is,
		/// overwrites existing user with new user values while keeping the same ID.
		/// This method will overwrite _all_ user fields, including password and email.
		/// </summary>
		/// <param name="existingUser"></param>
		/// <param name="newUser"></param>
		/// <param name="confirmationCode"></param>
		/// <returns></returns>
		public bool AssociateExistingUser(User existingUser, User newUser, out string confirmationCode) {
			confirmationCode = "";
			if (existingUser == null || newUser == null) {
				return false;
			}
			confirmationCode = ChangeUserEmail(existingUser.Id, newUser.Email);
			newUser.Id = existingUser.Id;
			SaveUser(newUser);
			return true;
		}

		public bool AssociateExistingUser(User existingUser, User newUser) {
			string throwaway;
			return AssociateExistingUser(existingUser, newUser, out throwaway);
		}


		/// <summary>
		/// Returns true if the user is an admin, else false
		/// </summary>
		/// <param name="email"></param>
		/// <returns></returns>
		public bool IsUserAdmin(string email) {
			return ExecuteScalar(@"
select c.IsAdmin
from Clients c
where c.EmailAddress = @email;", new Param("@email", email)).ToBoolean();
		}
	}
}