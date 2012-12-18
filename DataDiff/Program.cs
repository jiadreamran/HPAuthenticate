using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NDesk.Options;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DataDiff.Enums;
using DataDiff.Entities;
using DataDiff.Dalcs;
using System.Reflection;
using System.Data;
using libMatt.Converters;

namespace DataDiff {
	class Program {
		static void Main(string[] args) {
			try {
				string
					sourceServer = @"localhost\SQLEXPRESS",
					sourceDb = "HPWD_SQL",
					sourceTable = "AppraisalRollsStaging",
					destServer = @"localhost\SQLEXPRESS",
					destDb = "HPWD_SQL",
					destTable = "AppraisalRolls";

				var p = new OptionSet() {
				{ "sourceserver=", x => sourceServer = x },
				{ "sourcedatabase=", x => sourceDb = x },
				{ "sourcetable=", x => sourceTable = x },
				{ "destserver=", x => destServer = x },
				{ "destdatabase=", x => destDb = x },
				{ "desttable=", x => destTable = x }
			};

				p.Parse(args);


				var sourceConnStr = "Server=" + sourceServer + ";database=" + sourceDb + ";Integrated Security=SSPI";
				var destConnStr = "Server=" + destServer + ";database=" + destDb + ";Integrated Security=SSPI";

				// Get the schema of the source table.
				var sourceSchema = new SqlDalc(sourceConnStr).GetSchema("dbo", sourceTable);
				var destSchema = new SqlDalc(destConnStr).GetSchema("dbo", destTable);

				var diffDt = new SqlDalc(sourceConnStr).GetMismatchedAppraisalRows(sourceTable, destTable);

				List<XElement> tableRows = new List<XElement>();
				// For each modified record found here, perform additional checks:
				// IF the modification is a deletion:
				//		Look up associated records in AppraisalRolls based on the PropID/Cnty_FIPS (OWNERSHIP_TOTAL)
				//		or the PropertyNumber/County (AppraisalRolls). If any such records exist
				// IF the modification is a mismatch:
				//		Pull the record from AppraisalRolls and highlight the changed columns.
				bool odd = false;
				foreach (var diffRow in diffDt.AsEnumerable()) {
					XAttribute cssClass = null;
					List<XElement> cells = new List<XElement>();
					cells.Add(new XElement("td"));
					cells.Add(new XElement("td", diffRow["srcCounty"].GetString()));
					cells.Add(new XElement("td", diffRow["srcPropertyNumber"].GetString()));
					cells.Add(new XElement("td", diffRow["destCounty"].GetString()));
					cells.Add(new XElement("td", diffRow["destPropertyNumber"].GetString()));
					//switch (diff.DifferenceType) {
					if (diffRow["srcCounty"] == DBNull.Value) {
						// Dropped
						// Check for associations
						cssClass = new XAttribute("class", "drop");
						string tdText = "";
						if (diffRow["IsAssociated"].ToBoolean()) {
							tdText = "Client properties are associated with the record being dropped: " + string.Join(", ", new SqlDalc(destConnStr).GetClientPropertyIds(diffRow["destPropertyNumber"].GetString(), diffRow["destCounty"].GetString()).ToArray());
							cells.Add(new XElement("td", tdText));
							cssClass.Value += " warning";
						} else {
							cells.Add(new XElement("td", tdText));
						}
					} else if (diffRow["destCounty"] == DBNull.Value) {
						cssClass = new XAttribute("class", "add");
						cells.Add(new XElement("td"));
					} else {
						// Row differs
						cssClass = new XAttribute("class", "mismatch");
						string warning = diffRow["IsAssociated"].ToBoolean()
											? "Client properties are associated with this record: " + string.Join(", ", new SqlDalc(destConnStr).GetClientPropertyIds(diffRow["srcPropertyNumber"].GetString(), diffRow["srcCounty"].GetString()).ToArray())
											: "";
						// Retrieve the differing column output
						var mismatchTable = new XElement("table",
												new XElement("thead",
													new XElement("tr",
														destSchema.Columns.Cast<DataColumn>().Select(col => new XElement("th", col)).ToArray())
												),
												new XElement("tbody",
													new SqlDalc(destConnStr).GetTableRow(destTable, "AppraisalRollID", diffRow["destId"]),
													new SqlDalc(sourceConnStr).GetTableRow(sourceTable, "AppraisalRollID", diffRow["srcId"])
												)
											);
						cells.Add(new XElement("td", warning, mismatchTable));
						if (!string.IsNullOrEmpty(warning)) {
							cssClass.Value += " warning";
						}
					}
					if (odd) {
						cssClass.Value += " odd";
					}
					odd = !odd;
					tableRows.Add(new XElement("tr", new object[] { cssClass }.Concat(cells.Cast<object>().ToArray())));
				}

				// Read the CSS from the embedded resource "default.css" in the Assets directory.
				// This CSS will be inserted into the output HTML file.
				string css = "";
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DataDiff.Assets.default.css")) {
					using (StreamReader reader = new StreamReader(stream)) {
						css = reader.ReadToEnd();
					}
				}

				// Find the path to tablediff and run it against the specified tables.
				var el = new XElement("html",
							new XElement("head",
					// Grr, .NET trying to be too smart for its own good, escaping '>' in CSS
								new XElement("style", new XAttribute("type", "text/css"), new XCData(css))
							),
							new XElement("body",
								new XElement("h1", "Differences Report: " + sourceTable + " => " + destTable),
								new XElement("table",
									new XElement("thead",
										new XElement("tr",
											new XElement("th", ""),
											new XElement("th", "Source County"),
											new XElement("th", "Source Parcel"),
											new XElement("th", "Dest. County"),
											new XElement("th", "Dest. Parcel"),
											new XElement("th", "Notes")
										)
									),
									new XElement("tbody", tableRows.ToArray())
								)
							)
						);
				Console.Write(el.ToString());
			} catch (Exception ex) {
				Console.Error.WriteLine("Error occurred:");
				Console.Error.WriteLine(ex.Message);
				Console.Error.WriteLine("Stack trace: " + ex.StackTrace);
				Console.Error.WriteLine();
				Console.Error.WriteLine("Press any key to exit...");
				Console.ReadKey();

			}
		}

	}
}
