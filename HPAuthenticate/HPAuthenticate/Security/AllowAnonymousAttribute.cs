﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HPAuthenticate.Security {
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public sealed class AllowAnonymousAttribute : Attribute { }
}