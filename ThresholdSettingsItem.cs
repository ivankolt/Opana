using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    // File: ThresholdSettingsItem.cs
    public class ThresholdSettingsItem
    {
        public int Article { get; set; }          // Изменено с string на int
        public string MaterialName { get; set; }
        public decimal ScrapThreshold { get; set; }
        public string UnitName { get; set; }
        public int UnitId { get; set; }
    }

}
