using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using libMatt.Converters;

namespace SurveyApp.Models {
	public class MiscDalc: HPEntities.Dalcs.AuthDalcBase {

		public IEnumerable<string> GetCounties() {
			return ExecuteDataTable("select County from Counties;").AsEnumerable().Select(x => x["County"].GetString());
		}

		public void SaveSurveyResponse(
			string ipAddress,
			string firstName,
			string lastName,
			string county,
			int? saturationThickness,
			string cropType,
			int daysPerSeason,
			int? numberYearsUsed
		) {
			ExecuteNonQuery(@"
insert into SurveyResponses (
	IPAddress,
	CreateDatetimeUtc, 
	FirstName,
	LastName,
	County,
	EstimatedSaturationThickness,
	CropType,
	AverageIrrigationDaysPerSeason,
	NumberOfYearsUsedToCalculateAverage
) values (
	@ip,
	@date,
	@firstname,
	@lastname,
	@county,
	@saturation,
	@cropType,
	@daysPerSeason,
	@yearsUsed
);",
				new Param("@ip", ipAddress),
				new Param("@date", DateTime.UtcNow),
				new Param("@firstname", firstName),
				new Param("@lastname", lastName),
				new Param("@county", county.OrDbNull()),
				new Param("@saturation", saturationThickness ?? (object)DBNull.Value),
				new Param("@cropType", cropType.OrDbNull()),
				new Param("@daysPerSeason", daysPerSeason),
				new Param("@yearsUsed", numberYearsUsed ?? (object) DBNull.Value)
			);
		}

	}
}