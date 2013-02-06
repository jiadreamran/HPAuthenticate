using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities.Enums;
using HPEntities.Exceptions;
using libMatt.Formatters;
using HPEntities.Helpers;
using HPEntities.Entities;
using libMatt.Converters;
using System.Data;

namespace HPEntities.Dalcs {
	/// <summary>
	/// Handles interactions between users and properties - associations, dissociations, etc.
	/// </summary>
	public class PropertyDalc: AuthDalcBase {

		/// <summary>
		/// This overload associates an owned property with its owner user. Do not call this function
		/// to associate properties for meter installers/authorized producers; use AssociateProperty instead.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="parcelId"></param>
		/// <param name="county"></param>
		/// <param name="role"></param>
		public void AssociateOwnedProperty(User user, Property property, bool isOwnerDataBogus) {
			// This overload is not valid for tenants/meter installers.
			AssociateProperty(user, property, PropertyRole.owner, false, null, isOwnerDataBogus);
		}

		public void AssociateProperty(User user, Property property, PropertyRole role, bool didUserAcceptDisclaimer, DisclaimerDataType? productionType, bool isOwnerDataBogus) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}

			ExecuteNonQuery(@"
merge ClientProperties as target
using (
	select
		@id as clientId,
		@role as role,
		@customId as customId,
		@parcelId as parcelId,
		@county as county,
		@disclaimer as disclaimer,
		0 as isVerified,
		@productionType as productionType,
		@isOwnerDataBogus as isOwnerDataBogus
) as source on (
	target.ClientId = source.clientId
	and (
		target.PropertyID = source.customId
		or (
			target.ParcelID = source.parcelId
			and target.County = source.county
		)
	)
)
when not matched then
	insert (
		ClientId,
		PropertyRole,
		PropertyID,
		ParcelID,
		County,
		AcceptedDisclaimer,
		IsRecordVerified,
		ProductionType,
		IsOwnerDataWrong
	) values (
		source.clientId,
		source.role,
		source.customId,
		source.parcelId,
		source.county,
		source.disclaimer,
		source.isVerified,
		source.productionType,
		source.isOwnerDataBogus
	);",
				new Param("@id", user.Id),
				new Param("@role", (int)role),
				new Param("@customId", property.CustomId > 0 ? (object)property.CustomId : DBNull.Value),
				new Param("@parcelId", property.ParcelId.OrDbNull()),
				new Param("@county", property.County.OrDbNull()),
				new Param("@disclaimer", didUserAcceptDisclaimer),
				new Param("@productionType", productionType.OrDbNull()),
				new Param("@isOwnerDataBogus", isOwnerDataBogus)
			);

		}

		/// <summary>
		/// Removes the property from the user's associations.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="parcelId"></param>
		/// <param name="county"></param>
		public void DissociateProperty(User user, int? customId, string parcelId, string county) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			ExecuteNonQuery(@"
delete from ClientProperties
where
	ClientID = @clientId
	and (
		CustomID = @customId
		or (
			ParcelID = @parcelId
			and County = @county
		)
	);", new Param("@clientId", user.Id),
							new Param("@customId", customId),
							new Param("@parcelId", parcelId),
							new Param("@county", county.OrDbNull())
						);
		}

		public void DissociateProperty(User user, int clientPropertyId) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			// Technically client property ID is the unique identifier, but user id 
			// is present to help ensure a user can only delete his own properties.
			ExecuteNonQuery(@"
delete from ClientProperties 
where 
	ClientID = @userId
	and ClientPropertyID = @clientPropertyId;", 
							new Param("@userId", user.Id),
							new Param("@clientPropertyId", clientPropertyId)
				);
		}


		public void ChangePropertyRoleAndProductionType(User user, int clientPropertyId, PropertyRole role, DisclaimerDataType productionTypes) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			ExecuteNonQuery(@"
update ClientProperties
set
	PropertyRole = @role,
	ProductionType = @prodType
where
	ClientID = @userId
	and ClientPropertyID = @propId;", new Param("@role", (int)role),
								new Param("@prodType", (int)productionTypes),
								new Param("@userId", user.Id),
								new Param("@propId", clientPropertyId)
							);
		}

		/// <summary>
		/// Creates a new property definition in UserDefinedProperties.
		/// 
		/// As of 20111104, this is unused.
		/// </summary>
		/// <param name="desc"></param>
		/// <param name="user"></param>
		/// <returns></returns>
		public int? CreateProperty(Property desc, User user) {
			return ExecuteScalar(@"
insert into UserDefinedProperties(
	Description,
	IsRecordVerified,
	CreateDate
) values (
	@desc,
	0,
	getdate()
);

select @@IDENTITY;", new Param("@desc", desc.Description)).ToInteger();
		}

		public List<Property> GetPropertiesByOwner(string ownerName, string address, string city, string state, string zip) {
			// 20111104: The query below is more complex than necessary at present;
			//	users are no longer able to add properties. Leaving intact for now
			//	in case that feature comes back, but if this is still around in the
			//	deep future feel free to remove the union and simplify to only using
			//	appraisal rolls data - which will also mean getting rid of the PropertyID.
			var dt = ExecuteDataTable(@"
select
	null as PropertyId,
	ar.ParcelId,
	ar.County,
	LegalDescription as Description,
	ar.OwnerName
from vwAppraisalRollsUnedited ar
where
	OwnerName like @ownerName
	or (
		MailingAddress = @addr
		and MailingCity = @city
		and MailingState = @state
		and MailingZipCode = @zip
	)

union

select distinct
	cp.PropertyID as PropertyId,
	null as ParcelId,
	null as County,
	Description,
	c.DisplayName as OwnerName
from UserDefinedProperties udp
inner join ClientProperties cp
	on cp.PropertyId = udp.PropertyId
inner join Clients c
	on cp.ClientID = c.ClientID
inner join ClientAddresses ca
	on ca.ClientID = c.ClientID
inner join Cities
	on Cities.CityID = ca.CityID
inner join States
	on Cities.StateID = States.StateID
where
	c.DisplayName like @ownerName
	or (
		ca.Address like @addr
		and ca.PostalCode like @zip
		and Cities.City like @city
		and States.State like @state
	);", 
				new Param("@ownerName", ownerName),
				new Param("@addr", address),
				new Param("@city", city),
				new Param("@state", state),
				new Param("@zip", zip)
			);

			return (from row in dt.AsEnumerable()
					select new Property(
						row["PropertyId"].TryToInteger(),
						row["ParcelId"].GetString(),
						row["County"].GetString(),
						row["Description"].GetString()
					) {
						OwnerName = row["OwnerName"].GetString()
					}).ToList();
							
		}

		/// <summary>
		/// If the provided contact info differs from the info stored in AppraisalRolls,
		/// this function saves the differences to AppraisalRollEdits so that they'll be
		/// subsequently picked up in queries on vwAppraisalRolls.
		/// </summary>
		/// <param name="parcelId"></param>
		/// <param name="county"></param>
		/// <param name="address"></param>
		/// <param name="city"></param>
		/// <param name="state"></param>
		/// <param name="zip"></param>
		/// <param name="phoneNumber"></param>
		public void UpdateAppraisalRollDataAsNecessary(int userId, string parcelId, string county, string address, string city, string state, string zip, string phoneNumber) {
			var dt = ExecuteDataTable(@"
select
	OwnerName,
	MailingAddress,
	MailingCity,
	MailingState,
	MailingZipCode,
	PhoneNumber
from vwAppraisalRolls
where
	ParcelID = @id
	and County = @county;", new Param("@id", parcelId),
							new Param("@county", county));

			Func<string, string, object> getUpdatedValue = (oldVal, newVal) => {
				return (newVal != null && oldVal != newVal) ? (object)newVal : DBNull.Value;
			};

			foreach (var row in dt.AsEnumerable()) {
				var updatedAddr = getUpdatedValue(row["MailingAddress"].GetString(), address);
				var updatedCity = getUpdatedValue(row["MailingCity"].GetString(), city);
				var updatedState = getUpdatedValue(row["MailingState"].GetString(), state);
				var updatedZip = getUpdatedValue(row["MailingZipCode"].GetString(), zip);
				var updatedPhone = getUpdatedValue(row["PhoneNumber"].GetString(), phoneNumber);

				// If any owner info doesn't match, save the revised info into AppraisalRollEdits.
				if (new[] { updatedAddr, updatedCity, updatedState, updatedZip, updatedPhone }.Any(x => x != DBNull.Value)) {
					// Update the values in AppraisalRollEdits. This query should
					// insert new values if no previous user-entered values exist,
					// and if previous entries _do_ exist it should overwrite only
					// those fields that have since changed.
					ExecuteNonQuery(@"
merge AppraisalRollEdits as target
using (
	select
		@addr as address,
		@city as city,
		@state as state,
		@zip as zip,
		@phone as phone,
		@parcelId as parcelId,
		@county as county,
		@clientId as clientId
) as source
on target.ParcelID = source.parcelId
	and target.County = source.county
when matched then
	update set 
		target.MailingAddress = isnull(source.address, target.MailingAddress),
		target.MailingCity = isnull(source.city, target.MailingCity),
		target.MailingState = isnull(source.state, target.MailingState),
		target.MailingZipCode = isnull(source.zip, target.MailingZipCode),
		target.PhoneNumber = isnull(source.phone, target.PhoneNumber),
		target.LastEditorClientID = source.clientId,
		target.LastEditDateTime = getdate()
when not matched then
	insert  (ParcelID, County, MailingAddress, MailingCity, MailingState, MailingZipCode, PhoneNumber, LastEditorClientID, LastEditDateTime)
	values (source.parcelId, source.county, source.address, source.city, source.state, source.zip, source.phone, source.clientId, getdate());",
							new Param("@addr", updatedAddr),
							new Param("@city", updatedCity),
							new Param("@state", updatedState),
							new Param("@zip", updatedZip),
							new Param("@phone", updatedPhone),
							new Param("@parcelId", parcelId),
							new Param("@county", county),
							new Param("@clientId", userId)
						);
	
				}
			}

		}

		/// <summary>
		/// Checks AppraisalRolls to see if the specified parcelId/county record exists.
		/// </summary>
		/// <param name="parcelId">(string) The parcel ID (PropertyNumber) to search for.</param>
		/// <param name="county"></param>
		public bool DoesPropertyExist(string parcelId, string county) {
			return GetPropertyCount(parcelId, county) > 0;
		}

		public bool IsPropertyAssociated(int userId, string parcelId, string county) {
			int throwaway;
			return IsPropertyAssociated(userId, parcelId, county, out throwaway);
		}

		/// <summary>
		/// Returns true if the specified parcel/county has already been associated with
		/// the given user account; else returns false.
		/// </summary>
		/// <param name="userId"></param>
		/// <param name="parcelId"></param>
		/// <param name="county"></param>
		/// <returns></returns>
		public bool IsPropertyAssociated(int userId, string parcelId, string county, out int clientPropertyId) {
			clientPropertyId = ExecuteScalar("select ClientPropertyId from ClientProperties where ClientID = @clientId and ParcelID = @parcelId and County = @county;",
									new Param("@clientId", userId),
									new Param("@parcelId", parcelId),
									new Param("@county", county)
								).ToInteger();
			return clientPropertyId > 0;
		}

		/// <summary>
		/// Associates the given contiguous acres definition with the specified parcel/county combination.
		/// 
		/// Exceptions: ArgumentException, SqlException
		/// </summary>
		/// <param name="contiguousAcresId"></param>
		/// <param name="parcelId"></param>
		/// <param name="county"></param>
		/// <param name="actualUserId"></param>
		/// <param name="actingUserId"></param>
		public void AssociateContiguousAcres(int contiguousAcresId, List<Tuple<string, string>> parcelCounties, int actualUserId, int actingUserId) {
			if (contiguousAcresId <= 0) {
				throw new ArgumentException("You must provide a valid contiguousAcresId.");
			}
			if (parcelCounties.Count == 0) {
				throw new ArgumentException("You must provide at least one valid parcel/county combination.");
			}
			var udalc = new UserDalc();
			if (actualUserId <= 0 || udalc.GetUser(actualUserId) == null) {
				throw new ArgumentException("You must provide a valid actualUserId.");
			}
			if (actingUserId <= 0 || udalc.GetUser(actingUserId) == null) {
				throw new ArgumentException("You must provide a valid actingUserId.");
			}
			BeginTransaction();
			try {

				ExecuteNonQuery(@"
delete from PropertyContiguousAcres
where
	ContiguousAcresId = @caId;", new Param("@caId", contiguousAcresId));

				foreach (var pc in parcelCounties) {
					ExecuteNonQuery(@"
insert into PropertyContiguousAcres (
	ContiguousAcresId,
	ParcelId,
	County,
	CreateDatetime,
	CreateActingUserId,
	CreateActualUserId
) values (
	@caId,
	@parcelId,
	@county,
	getdate(),
	@actingUserId,
	@actualUserId
);",
								new Param("@caId", contiguousAcresId),
								new Param("@parcelId", pc.Item1),
								new Param("@county", pc.Item2),
								new Param("@actingUserId", actingUserId),
								new Param("@actualUserId", actualUserId)
						);
				}


				Commit();
			} catch (Exception ex) {
				Rollback();

			}

		}

		/// <summary>
		/// Retrieves a list of the contiguous acres IDs associated with the specified user ID and/or properties.
		/// </summary>
		/// <param name="actingUserId"></param>
		/// <param name="parcelCounties"></param>
		/// <returns></returns>
		public int[] GetContiguousAcresIds(int actingUserId, IEnumerable<Tuple<string, string>> parcelCounties) {
			List<int> ret = new List<int>();

			foreach (var pd in parcelCounties) {
				ret.AddRange(ExecuteDataTable(
					@"select ContiguousAcresId from PropertyContiguousAcres where ParcelId = @parcelId and County = @county;",
					new Param("@parcelId", pd.Item1),
					new Param("@county", pd.Item2)
				).AsEnumerable().Select(x => x["ContiguousAcresId"].ToInteger()));
			}

			// Also add in any records where the given actingUserId was the creator
			ret.AddRange(ExecuteDataTable(
				@"select ContiguousAcresId from PropertyContiguousAcres where CreateActingUserId = @id;",
				new Param("@id", actingUserId)
			).AsEnumerable().Select(x => x["ContiguousAcresId"].ToInteger()));

			return ret.Distinct().ToArray();
		}

		public int[] GetAssociatedCAIds(int userId) {
			return ExecuteDataTable(@"
select
	ContiguousAcresId
from PropertyContiguousAcres pca
inner join ClientProperties cp
	on cp.ParcelID = pca.ParcelId
	and cp.County = pca.County
where cp.ClientID = @userId;", new Param("@userId", userId)).AsEnumerable().Select(row => row["ContiguousAcresId"].ToInteger()).ToArray();
		}


		/// <summary>
		/// Returns an array of all properties associated with the given contiguous acres ID.
		/// </summary>
		/// <param name="caId"></param>
		/// <returns></returns>
		public PropertyDescription[] GetAssociatedPropertyDescriptions(int caId) {
			PropertyRole prole;
			return (ExecuteDataTable(@"
select
	cp.ClientPropertyID,
	cp.ClientID,
	cp.PropertyRole,
	pca.ParcelID,
	pca.County,
	cp.PropertyID,
	cp.AcceptedDisclaimer,
	cp.IsRecordVerified,
	cp.ProductionType,
	cp.IsOwnerDataWrong,
	ar.LegalDescription,
	isnull(ar.OwnerName, c.DisplayName) as OwnerName,
	c.EmailAddress
from PropertyContiguousAcres pca
left join ClientProperties cp
	on pca.ParcelID = cp.ParcelID
	and pca.County = cp.County
left join Clients c
	on cp.ClientID = c.ClientID
left join vwAppraisalRolls ar
	on cp.ParcelID = ar.ParcelID
	and cp.County = ar.County
where
	pca.ContiguousAcresId = @caId;", new Param("@caId", caId)).AsEnumerable().Select(
						x => new PropertyDescription(
							x["OwnerName"].GetString(),
							x["LegalDescription"].GetString(),
							x["EmailAddress"].GetString(),
							x["PropertyRole"].TryParseEnum<PropertyRole>(out prole) && prole == PropertyRole.owner,
							x["PropertyRole"].GetString(),
							x["County"].GetString(),
							x["ParcelId"].GetString()
						)
					).ToArray());
	
		}


		public int GetPropertyCount(string parcelId, string county) {
			return ExecuteScalar("select count(ParcelId) from vwAppraisalRolls where ParcelID = @parcelId and County = @county;",
									new Param("@parcelId", parcelId),
									new Param("@county", county)
								).ToInteger();
		}

	}
}
