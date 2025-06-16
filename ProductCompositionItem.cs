using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    public class ProductCompositionItem
    {
        public string MaterialType { get; set; } // "Ткань" или "Фурнитура"
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } // "м.кв.", "шт." и т.д.
    }
}
