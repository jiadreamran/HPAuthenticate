using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using libMatt.Converters;

namespace HPEntities.Dalcs {
	public class ConfigDalc: AuthDalcBase {

		/// <summary>
		/// Returns a conversion factor for the specified units. If no such factor
		/// exists in the database, this function returns 0.
		/// </summary>
		/// <param name="fromUnitId"></param>
		/// <param name="toUnitId"></param>
		/// <returns></returns>
		public double GetConversionFactor(int fromUnitId, int toUnitId) {
			return ExecuteScalar(@"
select ConversionFactor
from UnitConversionFactors
where
	FromUnitOfMeasureId = @fromId
	and ToUnitOfMeasureId = @toId;", new Param("@fromId", fromUnitId),
								   new Param("@toId", toUnitId)).ToDouble();
		}

		public Dictionary<int, Dictionary<int, double>> GetAllUnitConversionFactors() {
			return (from row in ExecuteDataTable(@"
select
	FromUnitOfMeasureId,
	ToUnitOfMeasureId,
	ConversionFactor
from UnitConversionFactors;").AsEnumerable()
					group row by row["FromUnitOfMeasureId"].ToInteger() into fromUnitGrp
					select new {
						fromUnitId = fromUnitGrp.Key,
						toDict = fromUnitGrp.ToDictionary(x => x["ToUnitOfMeasureId"].ToInteger(), x => x["ConversionFactor"].ToDouble())
					}).ToDictionary(
						x => x.fromUnitId,
						x => x.toDict
					);
		}

		/// <summary>
		/// Returns a dictionary keyed by county ID, value: another dictionary keyed by unit (string), value: conversion factor.
		/// </summary>
		/// <returns></returns>
		public Dictionary<int, Dictionary<string, double>> GetAllMeterUnitConversionFactors() {
			return (from row in ExecuteDataTable(@"
select
	CountyID,
	UnitString,
	ConversionFactor
from MeterUnitConversionFactors;").AsEnumerable()
					group row by row["CountyID"].ToInteger() into countyGrp
					select new {
						countyId = countyGrp.Key,
						factors = countyGrp.ToDictionary(x => x["UnitString"].GetString().ToLower(), x => x["ConversionFactor"].ToDouble())
					}).ToDictionary(x => x.countyId, x => x.factors);
		}

	}
}