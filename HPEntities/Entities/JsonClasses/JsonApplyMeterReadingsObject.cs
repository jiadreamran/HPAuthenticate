using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses
{
    /// <summary>
    /// Created by mjia to specifically deal with the streamline meter reading.
    /// A reading object from the mapping application (Flex side). Used specifically for "ApplyReadingEdits"
    /// Contains two arrays: adds and deletes
    /// </summary>

    /// </summary>
    public class JsonApplyMeterReadingsObject
    {
        public JsonFlexMeterReadingObject[] adds { get; set; }
        public JsonFlexMeterReadingObject[] deletes { get; set; }
    }
}