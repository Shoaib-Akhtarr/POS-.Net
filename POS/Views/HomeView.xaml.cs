using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace POS.Views
{
    public partial class HomeView : UserControl
    {
        private static readonly Color[] PaymentColors =
        {
            Color.FromRgb(15, 148, 136),
            Color.FromRgb(59, 130, 246),
            Color.FromRgb(245, 158, 11),
            Color.FromRgb(139, 92, 246),
            Color.FromRgb(236, 72, 153),
            Color.FromRgb(100, 116, 139)
        };

        public HomeView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (_, _) => DrawPaymentDonut();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
                oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newViewModel)
                newViewModel.PropertyChanged += OnViewModelPropertyChanged;

            DrawPaymentDonut();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.HomeViewModel.PaymentMethodMix))
                Dispatcher.InvokeAsync(DrawPaymentDonut);
        }

        private void DrawPaymentDonut()
        {
            if (PaymentDonutCanvas is null || DataContext is not ViewModels.HomeViewModel viewModel)
                return;

            PaymentDonutCanvas.Children.Clear();
            var payments = viewModel.PaymentMethodMix.Where(item => item.Percent > 0).ToList();
            if (payments.Count == 0)
                return;

            const double center = 90;
            const double radius = 88;
            var angle = -90d;

            for (var index = 0; index < payments.Count; index++)
            {
                var sweep = payments[index].Percent / payments.Sum(item => item.Percent) * 360d;
                var endAngle = angle + Math.Min(sweep, 359.99d);
                var startRadians = angle * Math.PI / 180d;
                var endRadians = endAngle * Math.PI / 180d;
                var start = new Point(center + radius * Math.Cos(startRadians), center + radius * Math.Sin(startRadians));
                var end = new Point(center + radius * Math.Cos(endRadians), center + radius * Math.Sin(endRadians));

                var figure = new PathFigure { StartPoint = new Point(center, center), IsClosed = true };
                figure.Segments.Add(new LineSegment(start, true));
                figure.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0, sweep > 180d, SweepDirection.Clockwise, true));
                figure.Segments.Add(new LineSegment(new Point(center, center), true));

                var geometry = new PathGeometry();
                geometry.Figures.Add(figure);
                var slice = new Path
                {
                    Data = geometry,
                    Fill = new SolidColorBrush(PaymentColors[index % PaymentColors.Length]),
                    ToolTip = $"{payments[index].Method}: Rs {payments[index].Amount:N0} ({payments[index].Percent:N1}%)"
                };
                PaymentDonutCanvas.Children.Add(slice);
                slice.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    BeginTime = TimeSpan.FromMilliseconds(index * 30)
                });
                angle += sweep;
            }
        }
        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.HomeViewModel vm)
            {
                vm.IsLowStockModalOpen = false;
            }
        }
    }
}
