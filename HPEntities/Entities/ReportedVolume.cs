using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
    public class ReportedVolume {

        public int CalculatedVolumeGallons { get; set; }
        public int? UserRevisedVolume { get; set; }
        public int? UserRevisedVolumeUnitId { get; set; }
    }
}
