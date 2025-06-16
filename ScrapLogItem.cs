using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    public class ScrapLogItem
    {
        public DateTime LogDate { get; set; }
        public string MaterialArticle { get; set; }
        public string MaterialName { get; set; }
        public decimal QuantityScrap { get; set; }
        public string UnitName { get; set; }
        public decimal CostScrap { get; set; }
        public string WrittenOffBy { get; set; }
    }
}
