using System.Windows;
using System.Windows.Controls;

namespace FalcrebCloudBackup.App.Controls
{
    public partial class BrandMark : UserControl
    {
        public static readonly DependencyProperty IconHeightProperty =
            DependencyProperty.Register(nameof(IconHeight), typeof(double), typeof(BrandMark), new PropertyMetadata(40.0));

        public static readonly DependencyProperty TextSizeProperty =
            DependencyProperty.Register(nameof(TextSize), typeof(double), typeof(BrandMark), new PropertyMetadata(22.0));

        public double IconHeight
        {
            get => (double)GetValue(IconHeightProperty);
            set => SetValue(IconHeightProperty, value);
        }

        public double TextSize
        {
            get => (double)GetValue(TextSizeProperty);
            set => SetValue(TextSizeProperty, value);
        }

        public BrandMark()
        {
            InitializeComponent();
        }
    }
}
