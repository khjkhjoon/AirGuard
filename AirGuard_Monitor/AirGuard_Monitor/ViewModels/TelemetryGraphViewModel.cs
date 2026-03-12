using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.ObjectModel;
using AirGuard.WPF.Models;

namespace AirGuard.WPF.ViewModels
{
    /// <summary>
    /// 텔레메트리 그래프 뷰모델 - 배터리/속도/고도 OxyPlot 실시간 그래프 관리
    /// </summary>
    public class TelemetryGraphViewModel : BaseViewModel
    {
        // 배터리 그래프 모델
        private PlotModel _batteryModel;
        // 속도 그래프 모델
        private PlotModel _speedModel;
        // 고도 그래프 모델
        private PlotModel _altitudeModel;

        // 배터리 그래프 모델 프로퍼티
        public PlotModel BatteryModel { get => _batteryModel; private set => SetProperty(ref _batteryModel, value); }
        // 속도 그래프 모델 프로퍼티
        public PlotModel SpeedModel { get => _speedModel; private set => SetProperty(ref _speedModel, value); }
        // 고도 그래프 모델 프로퍼티
        public PlotModel AltitudeModel { get => _altitudeModel; private set => SetProperty(ref _altitudeModel, value); }

        // 배터리 시리즈 (데이터 포인트 직접 접근용)
        private LineSeries _batterySeries = new();
        // 속도 시리즈
        private LineSeries _speedSeries = new();
        // 고도 시리즈
        private LineSeries _altitudeSeries = new();

        // 생성자 - 배터리/속도/고도 모델 초기화 및 시리즈 참조 캐싱
        public TelemetryGraphViewModel()
        {
            _batteryModel = CreateModel("BATTERY", "%", OxyColor.FromRgb(0, 255, 136), 0, 100);
            _speedModel = CreateModel("SPEED", "m/s", OxyColor.FromRgb(0, 212, 255), 0, 30);
            _altitudeModel = CreateModel("ALTITUDE", "m", OxyColor.FromRgb(255, 140, 0), 0, 50);

            _batterySeries = GetSeries(_batteryModel);
            _speedSeries = GetSeries(_speedModel);
            _altitudeSeries = GetSeries(_altitudeModel);
        }

        // PlotModel 생성 - 배경/축/시리즈 설정 포함
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

            // X축 - 시간축, 표시 비활성화
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                IsAxisVisible = false,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                Minimum = DateTimeAxis.ToDouble(DateTime.Now.AddMinutes(-5)),
                Maximum = DateTimeAxis.ToDouble(DateTime.Now.AddMinutes(1)),
            });

            // Y축 - 고정 범위로 "Wrong number of divisions" 방지
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = yMin,
                Maximum = yMax,
                MajorStep = (yMax - yMin) / 4.0,
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

            // 데이터 시리즈
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

        // 모델의 첫 번째 시리즈 반환
        private static LineSeries GetSeries(PlotModel model)
            => (LineSeries)model.Series[0];

        // 텔레메트리 히스토리로 그래프 데이터 갱신
        public void UpdateData(ObservableCollection<TelemetryPoint> history)
        {
            try
            {
                _batterySeries.Points.Clear();
                _speedSeries.Points.Clear();
                _altitudeSeries.Points.Clear();

                // 히스토리 포인트를 각 시리즈에 추가
                foreach (var p in history)
                {
                    double t = DateTimeAxis.ToDouble(p.Time);
                    _batterySeries.Points.Add(new DataPoint(t, p.Battery));
                    _speedSeries.Points.Add(new DataPoint(t, p.Speed));
                    _altitudeSeries.Points.Add(new DataPoint(t, p.Altitude));
                }

                // Y축 범위 자동 조정
                AdjustYAxis(_batteryModel, _batterySeries, 0, 100);
                AdjustYAxis(_speedModel, _speedSeries, 0, 30);
                AdjustYAxis(_altitudeModel, _altitudeSeries, 0, 50);

                // X축 범위 갱신 - 범위 없으면 "Wrong number of divisions" 발생
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
            catch { /* OxyPlot 내부 division 에러 무시 - 다음 틱에 정상 복구됨 */ }
        }

        // 데이터 범위에 맞게 Y축 조정 - 범위 0이면 기본값으로 fallback
        private static void AdjustYAxis(PlotModel model, LineSeries series,
                                        double defaultMin, double defaultMax)
        {
            // Y축 참조
            var axis = (LinearAxis)model.Axes[1];

            // 데이터 없거나 1개이면 기본 범위 사용
            if (series.Points.Count < 2)
            {
                axis.Minimum = defaultMin;
                axis.Maximum = defaultMax;
                axis.MajorStep = (defaultMax - defaultMin) / 4.0;
                axis.MinorStep = (defaultMax - defaultMin) / 8.0;
                return;
            }

            // 데이터 최솟값/최댓값 탐색
            double min = double.MaxValue, max = double.MinValue;
            foreach (var pt in series.Points)
            {
                if (pt.Y < min) min = pt.Y;
                if (pt.Y > max) max = pt.Y;
            }

            // min == max 이면 기본 범위로 fallback
            if (Math.Abs(max - min) < 0.001)
            {
                axis.Minimum = defaultMin;
                axis.Maximum = defaultMax;
                axis.MajorStep = (defaultMax - defaultMin) / 4.0;
                axis.MinorStep = (defaultMax - defaultMin) / 8.0;
                return;
            }

            // 10% 마진 적용 후 기본 범위 내로 클램프
            double margin = (max - min) * 0.1;
            double axisMin = Math.Max(defaultMin, min - margin);
            double axisMax = Math.Min(defaultMax, max + margin);

            // 부동소수점 엣지케이스 방어
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

        // 모든 시리즈 초기화 및 축을 기본 범위로 리셋
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

        // Y축을 지정 범위로 리셋
        private static void ResetAxis(LinearAxis axis, double min, double max)
        {
            axis.Minimum = min;
            axis.Maximum = max;
            axis.MajorStep = (max - min) / 4.0;
            axis.MinorStep = (max - min) / 8.0;
        }
    }
}