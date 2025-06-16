using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UchPR
{
    public class MaterialStockItem
    {
        public string Article { get; set; }
        public string Name { get; set; }

        public decimal BaseQuantity { get; set; }
        public decimal TotalCost { get; set; }
        public int BaseUnitId { get; set; }

        public decimal DisplayQuantity { get; set; }
        public decimal AverageCost => BaseQuantity > 0 ? TotalCost / BaseQuantity : 0;

        // Для ComboBox
        public List<UnitOfMeasurement> AvailableUnits { get; set; }
        public UnitOfMeasurement SelectedUnit { get; set; }
    }

    public class UnitOfMeasurement
    {
        public int Code { get; set; }
        public string Name { get; set; }
        public decimal ConversionFactor { get; set; } = 1.0m;
    }

}
