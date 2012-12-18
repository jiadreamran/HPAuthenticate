using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Exceptions {
	/// <summary>
	/// This exception is thrown when multiple 
	/// </summary>
	class MultipleWellsFoundException: Exception {

		public MultipleWellsFoundException(int permitNumber) : 
			base("Multiple wells were found for the permit number '" + permitNumber.ToString() + "'; unable to determine which well you wanted.") { }
	}
}
