using FoodPOS.Controllers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FoodPOS
{
    public class Product : INotifyPropertyChanged
    {
        private int _stockQuantity;
        private string _name;
        private string _description;
        private decimal _price;

        public int ProductId { get; set; }
        private bool _isActive = true;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }
        public int CategoryId { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal Price
        {
            get => _price;
            set
            {
                if (_price != value)
                {
                    _price = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedPrice));
                }
            }
        }

        public decimal CostPrice { get; set; }


        public int StockQuantity
        {
            get => _stockQuantity;
            set
            {
                if (_stockQuantity != value)
                {
                    _stockQuantity = value;
                    OnPropertyChanged();
                    // Notify that all computed properties have changed
                    OnPropertyChanged(nameof(StockIcon));
                    OnPropertyChanged(nameof(StockText));
                    OnPropertyChanged(nameof(StockBackground));
                    OnPropertyChanged(nameof(StockStatusBackground));
                    OnPropertyChanged(nameof(StockStatusColor));
                    OnPropertyChanged(nameof(ButtonColor));
                    OnPropertyChanged(nameof(IsInStock));
                }
            }
        }

        public string Barcode { get; set; }

        // Add formatted price property
        public string FormattedPrice => $"Rs. {Price:N2}";

        // Enhanced stock properties
        public string StockIcon
        {
            get
            {
                if (StockQuantity <= 0) return "❌";
                if (StockQuantity <= 3) return "⚠️";
                if (StockQuantity <= 10) return "🔶";
                return "✅";
            }
        }

        public string StockText
        {
            get
            {
                if (StockQuantity <= 0) return "OUT OF STOCK";
                if (StockQuantity <= 3) return $"VERY LOW ({StockQuantity})";
                if (StockQuantity <= 10) return $"LOW ({StockQuantity})";
                return $"IN STOCK ({StockQuantity})";
            }
        }

        public System.Windows.Media.Brush StockBackground
        {
            get
            {
                if (StockQuantity <= 0) return System.Windows.Media.Brushes.LightCoral;
                if (StockQuantity <= 3) return System.Windows.Media.Brushes.LightGoldenrodYellow;
                if (StockQuantity <= 10) return System.Windows.Media.Brushes.LightYellow;
                return System.Windows.Media.Brushes.LightGreen;
            }
        }

        public System.Windows.Media.Brush StockStatusBackground
        {
            get
            {
                if (StockQuantity <= 0) return System.Windows.Media.Brushes.DarkRed;
                if (StockQuantity <= 3) return System.Windows.Media.Brushes.DarkOrange;
                if (StockQuantity <= 10) return System.Windows.Media.Brushes.Gold;
                return System.Windows.Media.Brushes.LimeGreen;
            }
        }

        public System.Windows.Media.Brush StockStatusColor => System.Windows.Media.Brushes.White;

        public System.Windows.Media.Brush ButtonColor
        {
            get
            {
                if (StockQuantity <= 0) return System.Windows.Media.Brushes.Gray;
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
            }
        }

        public bool IsInStock => StockQuantity > 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


}

public class ProductVM : Product
{
    public string CategoryName { get; set; }
}