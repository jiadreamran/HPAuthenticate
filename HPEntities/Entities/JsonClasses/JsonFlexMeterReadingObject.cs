using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses
{
    /// <summary>
    /// Reading object created in the mapping application
    /// </summary>
    public class JsonFlexMeterReadingObject
    {
        //public DateTime ReadingDate { set; get; }
        public DateTime ReadingDateJson { set; get; }
        //public string ReadingDateDisplay { set; get; }
        public double? ReadingValue { set; get; }
        public double? GallonsPerMinute { set; get; }
        public int ReadingType { set; get; }
        public string ReadingTypeDisplay { set; get; }
        public int ReportingYear { set; get; }
        public int IsSubmitted { set; get; }
        public string ActualUser { set; get; }
        public string MeterInstallationReadingID { set; get; }
        public string MeterInstallationID { set; get; }

        //private DateTime ReadingDate;

        //public void SetReadingDate() 
        //{
        //    ReadingDate = new DateTime().T
        //}
    }
}
