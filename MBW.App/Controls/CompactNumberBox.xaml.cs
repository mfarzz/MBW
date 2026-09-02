using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MBW.App.Controls
{
    public sealed partial class CompactNumberBox : UserControl
    {
        public CompactNumberBox()
        {
            InitializeComponent();
            Loaded += CompactNumberBox_Loaded;
            PartNumberBox.GotFocus += (_, _) => HideDeleteButton(PartNumberBox);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(CompactNumberBox),
                new PropertyMetadata(0d));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(
                nameof(Minimum),
                typeof(double),
                typeof(CompactNumberBox),
                new PropertyMetadata(double.MinValue));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(
                nameof(Maximum),
                typeof(double),
                typeof(CompactNumberBox),
                new PropertyMetadata(double.MaxValue));

        public double SmallChange
        {
            get => (double)GetValue(SmallChangeProperty);
            set => SetValue(SmallChangeProperty, value);
        }

        public static readonly DependencyProperty SmallChangeProperty =
            DependencyProperty.Register(
                nameof(SmallChange),
                typeof(double),
                typeof(CompactNumberBox),
                new PropertyMetadata(1d));

        public double LargeChange
        {
            get => (double)GetValue(LargeChangeProperty);
            set => SetValue(LargeChangeProperty, value);
        }

        public static readonly DependencyProperty LargeChangeProperty =
            DependencyProperty.Register(
                nameof(LargeChange),
                typeof(double),
                typeof(CompactNumberBox),
                new PropertyMetadata(10d));

        private void CompactNumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            HideDeleteButton(PartNumberBox);
        }

        private static void HideDeleteButton(DependencyObject parent)
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button { Name: "DeleteButton" } deleteButton)
                {
                    deleteButton.Visibility = Visibility.Collapsed;
                    return;
                }

                HideDeleteButton(child);
            }
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            var next = Value + SmallChange;
            if (next > Maximum)
            {
                next = Maximum;
            }

            Value = next;
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            var next = Value - SmallChange;
            if (next < Minimum)
            {
                next = Minimum;
            }

            Value = next;
        }
    }
}
