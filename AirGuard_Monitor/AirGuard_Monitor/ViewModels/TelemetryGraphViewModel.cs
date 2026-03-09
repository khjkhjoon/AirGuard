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
            _batteryModel = CreateModel("BATTERY", "%", OxyColor.FromRgb(0, 255, 136), 0, 100);
            _speedModel = CreateModel("SPEED", "m/s", OxyColor.FromRgb(0, 212, 255), 0, 30);
            _altitudeModel = CreateModel("ALTITUDE", "m", OxyColor.FromRgb(255, 140, 0), 0, 50);

            _batterySeries = GetSeries(_batteryModel);
            _speedSeries = GetSeries(_speedModel);
            _altitudeSeries = GetSeries(_altitudeModel);
        }

        private static PlotModel CreateModel(string title, string unit, OxyColor color,
                                             double yMin, double yMax)
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
                Minimum = DateTimeAxis.ToDouble(DateTime.Now.AddMinutes(-5)),
                Maximum = DateTimeAxis.ToDouble(DateTime.Now.AddMinutes(1)),
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = yMin,                      // 고정 범위로 "Wrong number of divisions" 방지
                Maximum = yMax,
                MajorStep = (yMax - yMin) / 4.0,      // 눈금 간격 명시
                MinorStep = (yMax - yMin) / 8.0,
                AbsoluteMinimum = yMin,
                AbsoluteMaximum = yMax,
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
            try
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

                AdjustYAxis(_batteryModel, _batterySeries, 0, 100);
                AdjustYAxis(_speedModel, _speedSeries, 0, 30);
                AdjustYAxis(_altitudeModel, _altitudeSeries, 0, 50);

                // X축 범위 갱신 — 범위 없으면 "Wrong number of divisions" 발생
                if (history.Count >= 2)
                {
                    double xMin = DateTimeAxis.ToDouble(history[0].Time);
                    double xMax = DateTimeAxis.ToDouble(history[^1].Time);
                    if (xMax <= xMin) xMax = xMin + 1;
                    foreach (var m in new[] { _batteryModel, _speedModel, _altitudeModel })
                    {
                        var xAxis = (DateTimeAxis)m.Axes[0];
                        xAxis.Minimum = xMin;
                        xAxis.Maximum = xMax;
                    }
                }

                _batteryModel.InvalidatePlot(true);
                _speedModel.InvalidatePlot(true);
                _altitudeModel.InvalidatePlot(true);
            }
            catch { /* OxyPlot 내부 division 에러 무시 — 다음 틱에 정상 복구됨 */ }
        }

        /// <summary>
        /// 데이터 범위에 맞게 Y축을 조정합니다.
        /// 범위가 0이면 기본값을 사용해 "Wrong number of divisions" 를 방지합니다.
        /// </summary>
        private static void AdjustYAxis(PlotModel model, LineSeries series,
                                        double defaultMin, double defaultMax)
        {
            var axis = (LinearAxis)model.Axes[1];

            // 데이터가 없거나 1개이면 기본 범위 사용
            if (series.Points.Count < 2)
            {
                axis.Minimum = defaultMin;
                axis.Maximum = defaultMax;
                axis.MajorStep = (defaultMax - defaultMin) / 4.0;
                axis.MinorStep = (defaultMax - defaultMin) / 8.0;
                return;
            }

            double min = double.MaxValue, max = double.MinValue;
            foreach (var pt in series.Points)
            {
                if (pt.Y < min) min = pt.Y;
                if (pt.Y > max) max = pt.Y;
            }

            // 최솟값 = 최댓값이면 기본 범위로 fallback
            if (Math.Abs(max - min) < 0.001)
            {
                axis.Minimum = defaultMin;
                axis.Maximum = defaultMax;
                axis.MajorStep = (defaultMax - defaultMin) / 4.0;
                axis.MinorStep = (defaultMax - defaultMin) / 8.0;
                return;
            }

            double margin = (max - min) * 0.1;
            double axisMin = Math.Max(defaultMin, min - margin);
            double axisMax = Math.Min(defaultMax, max + margin);

            // axisMin == axisMax 방어 (부동소수점 엣지케이스)
            if (Math.Abs(axisMax - axisMin) < 0.001)
            {
                axisMin = defaultMin;
                axisMax = defaultMax;
            }

            double step = (axisMax - axisMin) / 4.0;

            axis.Minimum = axisMin;
            axis.Maximum = axisMax;
            axis.MajorStep = step;
            axis.MinorStep = step / 2.0;
        }

        public void Clear()
        {
            _batterySeries.Points.Clear();
            _speedSeries.Points.Clear();
            _altitudeSeries.Points.Clear();

            // 축을 기본 범위로 리셋
            ResetAxis((LinearAxis)_batteryModel.Axes[1], 0, 100);
            ResetAxis((LinearAxis)_speedModel.Axes[1], 0, 30);
            ResetAxis((LinearAxis)_altitudeModel.Axes[1], 0, 50);

            _batteryModel.InvalidatePlot(true);
            _speedModel.InvalidatePlot(true);
            _altitudeModel.InvalidatePlot(true);
        }

        private static void ResetAxis(LinearAxis axis, double min, double max)
        {
            axis.Minimum = min;
            axis.Maximum = max;
            axis.MajorStep = (max - min) / 4.0;
            axis.MinorStep = (max - min) / 8.0;
        }
    }
}