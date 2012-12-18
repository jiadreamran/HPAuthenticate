using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DataDiff.Enums;

namespace DataDiff.Entities {
	class SqlDifference {

		public SqlDifference(SqlDifferenceType differenceType, object key, params string[] columnNames) {
			this.DifferenceType = differenceType;
			this.Key = key;
			this.ColumnNames = columnNames;
		}

		public SqlDifferenceType DifferenceType { get; set; }
		public object Key { get; set; }
		public string[] ColumnNames { get; set; }
	}
}
