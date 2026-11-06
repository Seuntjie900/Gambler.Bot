using Avalonia.Threading;
using Gambler.Bot.Classes;
using Gambler.Bot.Classes.Charts;
using LiveChartsCore;
using LiveChartsCore.ConditionalDraw;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Gambler.Bot.ViewModels.Common
{
    public class ProfitChartViewModel : ViewModelBase
    {
        public int MaxItems { get; set; } = -1;
        RealTimeDataCollection DataPoints = new RealTimeDataCollection();

        public ISeries[] Series { get; set; }

        public LabelVisual Title
        {
            get;
            set;
        } =
new LabelVisual
        {
            Text = "Profit over bets",
            TextSize = 25,
            Padding = new LiveChartsCore.Drawing.Padding(15),
            Paint = new SolidColorPaint(SKColors.DarkSlateGray)
        };

        int ChartItems = 0;
        decimal ChartProfit = 0;
        delegate void dAddPoint(decimal profit);
        bool enabled = true;

        static readonly SolidColorPaint WinPointPaint = new SolidColorPaint(SKColors.Green);
        static readonly SolidColorPaint LossPointPaint = new SolidColorPaint(SKColors.Red);

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                this.RaisePropertyChanged();
            }
        }

        private string _freezeText = "Freeze";

        public string FreezeText
        {
            get { return _freezeText; }
            set
            {
                _freezeText = value;
                this.RaisePropertyChanged();
            }
        }

        public ProfitChartViewModel(Microsoft.Extensions.Logging.ILogger logger) : base(logger)
        {
            ResetCommand = ReactiveCommand.Create(Reset);
            FreezeCommand = ReactiveCommand.Create(ToggleFreeze);
            FreezeText = "Freeze Chart";
            LineSeries<SimpleDataPoint> series =
        new LineSeries<SimpleDataPoint>
            {
                Values = DataPoints,
                Fill = null,
                   
                Stroke = new SolidColorPaint(SKColors.LightBlue, 1) ,
                //Mapping = (sample, index) => new(sample.Argument, sample.Value),
                GeometrySize = 3,
                LineSmoothness = 0,
            };
            
            series.PointMeasured += Series_PointMeasured;            
            series.PointCreated += Series_PointCreated;
            
            ;

            Series = new ISeries[] { series };
        }

        private void Series_PointCreated(
            LiveChartsCore.Kernel.ChartPoint<SimpleDataPoint, LiveChartsCore.SkiaSharpView.Drawing.Geometries.CircleGeometry, LiveChartsCore.SkiaSharpView.Drawing.Geometries.LabelGeometry> obj)
        {
            /*foreach(var x in obj.Context.Entity.MetaData.ChartPoints)
            {
            }*/
        }

        private void Series_PointMeasured(
            LiveChartsCore.Kernel.ChartPoint<SimpleDataPoint, LiveChartsCore.SkiaSharpView.Drawing.Geometries.CircleGeometry, LiveChartsCore.SkiaSharpView.Drawing.Geometries.LabelGeometry> obj)
        {
            //if going up: green
            if(obj.Model.Win)
            {        
                
                obj.Visual.Fill = WinPointPaint;
                obj.Visual.Stroke = WinPointPaint;
                
                //obj.Visual.
            } else
            { //if going down: red
                obj.Visual.Fill = LossPointPaint;
                obj.Visual.Stroke = LossPointPaint;
                
            }
            //(obj.Context.Series as LineSeries<SimpleDataPoint>).Stroke = obj.Visual.Stroke;
        }

        public void AddPoint(decimal BetProfit, bool win)
        {
            if(Enabled)
            {
                if(!Dispatcher.UIThread.CheckAccess())
                {
                    if (UISettings.Settings.AsyncUIUpdates)
                        Dispatcher.UIThread
                            .Post(
                                () =>
                                {
                                    AddPoint(BetProfit, win);
                                });
                    else
                        Dispatcher.UIThread
                            .Invoke(
                                () =>
                                {
                                    AddPoint(BetProfit, win);
                                });
                }
                else
                {
                    ChartProfit += BetProfit;
                    DataPoints.Add(new SimpleDataPoint(ChartItems++, (double)ChartProfit, win));
                    //profitChart.AddPoint(ChartItems++, (double)Profit);
                    while(DataPoints.Count > (MaxItems > 0 ? MaxItems : UISettings.Settings.ChartBets))
                    {
                        DataPoints.RemoveRangeAt(
                            0,
                            (DataPoints.Count - (MaxItems > 0 ? MaxItems : UISettings.Settings.ChartBets)) + 1);
                    }
                }
            }
        }

        public async Task AddRange(List<decimal> points)
        {
            if(!Dispatcher.UIThread.CheckAccess())
                await Dispatcher.UIThread
                    .InvokeAsync(
                        async () =>
                        {
                            await AddRange(points);
                        });
            else
            {
                DataPoints.AddRange(
                    points.Select(m => new SimpleDataPoint(ChartItems++, (double)(ChartProfit += m), m > 0)).ToList());
                while(DataPoints.Count > (MaxItems > 0 ? MaxItems : UISettings.Settings.ChartBets))
                {
                    DataPoints.RemoveRangeAt(
                        0,
                        (DataPoints.Count - (MaxItems > 0 ? MaxItems : UISettings.Settings.ChartBets)) + 1);
                }
            }
        }

        public ICommand ResetCommand { get; }

        public void Reset()
        {
            bool OriginalEnabled = Enabled;
            Enabled = false;
            ChartProfit = 0;
            ChartItems = 0;
            DataPoints.Clear();
            Enabled = OriginalEnabled;
        }


        public ICommand FreezeCommand { get; }

        private void ToggleFreeze()
        {
            Enabled = !Enabled;
            if(Enabled)
                FreezeText = "Freeze";
            else
                FreezeText = "Resume";
        }
    }

    
}
