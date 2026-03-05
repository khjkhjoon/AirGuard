using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.ObjectModel;
using AirGuard.WPF.Models;

namespace AirGuard.WPF.ViewModels
{
    public class TelemetryGraphViewModel : BaseViewModel
    {
        private PlotModel _batteryModel;
        private PlotModel _speedModel;
        private PlotModel _altitudeModel;

        public PlotModel BatteryModel { get => _batteryModel; private set => SetProperty(ref _batteryModel, value); }
        public PlotModel SpeedModel { get => _speedModel; private set => SetProperty(ref _speedModel, value); }
        public PlotModel AltitudeModel { get => _altitudeModel; private set => SetProperty(ref _altitudeModel, value); }

        private LineSeries _batterySeries = new();
        private LineSeries _speedSeries = new();
        private LineSeries _altitudeSeries = new();

        public TelemetryGraphViewModel()
        {
            _batteryModel = CreateModel("BATTERY", "%", OxyColor.FromRgb(0, 255, 136));
            _speedModel = CreateModel("SPEED", "m/s", OxyColor.FromRgb(0, 212, 255));
            _altitudeModel = CreateModel("ALTITUDE", "m", OxyColor.FromRgb(255, 140, 0));

            _batterySeries = GetSeries(_batteryModel);
            _speedSeries = GetSeries(_speedModel);
            _altitudeSeries = GetSeries(_altitudeModel);
        }

        private static PlotModel CreateModel(string title, string unit, OxyColor color)
        {
            var model = new PlotModel
            {
                Background = OxyColor.FromRgb(13, 18, 28),
                PlotAreaBorderColor = OxyColor.FromRgb(30, 58, 95),
                TextColor = OxyColor.FromRgb(122, 155, 181),
                TitleColor = OxyColor.FromRgb(0, 212, 255),
                TitleFontSize = 9,
                Title = $"{title} ({unit})",
                Padding = new OxyThickness(4),
            };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                IsAxisVisible = false,
                IsPanEnabled = false,
                IsZoomEnabled = false,
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColor.FromRgb(61, 90, 115),
                TicklineColor = OxyColor.FromRgb(30, 58, 95),
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromRgb(20, 35, 55),
                FontSize = 8,
                IsPanEnabled = false,
                IsZoomEnabled = false,
            });

            var series = new LineSeries
            {
                Color = color,
                StrokeThickness = 1.5,
                MarkerType = MarkerType.None,
                LineStyle = LineStyle.Solid,
            };
            model.Series.Add(series);
            return model;
        }

        private static LineSeries GetSeries(PlotModel model)
            => (LineSeries)model.Series[0];

        public void UpdateData(ObservableCollection<TelemetryPoint> history)
        {
            _batterySeries.Points.Clear();
            _speedSeries.Points.Clear();
            _altitudeSeries.Points.Clear();

            foreach (var p in history)
            {
                double t = DateTimeAxis.ToDouble(p.Time);
                _batterySeries.Points.Add(new DataPoint(t, p.Battery));
                _speedSeries.Points.Add(new DataPoint(t, p.Speed));
                _altitudeSeries.Points.Add(new DataPoint(t, p.Altitude));
            }

            _batteryModel.InvalidatePlot(true);
            _speedModel.InvalidatePlot(true);
            _altitudeModel.InvalidatePlot(true);
        }

        public void Clear()
        {
            _batterySeries.Points.Clear();
            _speedSeries.Points.Clear();
            _altitudeSeries.Points.Clear();
            _batteryModel.InvalidatePlot(true);
            _speedModel.InvalidatePlot(true);
            _altitudeModel.InvalidatePlot(true);
        }
    }
}