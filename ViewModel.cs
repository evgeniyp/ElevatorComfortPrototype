using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ElevatorComfort
{
    public class ViewModel
    {
        private const double COMMAND_RATE = 99.058823529411764705882352941176; // RATE ON DEVICE = 72
        private const int SECONDS_TO_REMEMBER = 600;
        private const int LOWPASS_FREQ = 5;

        public PlotModel Plot { get; private set; }

        private LineSeries _z = new LineSeries("Accel Z");
        private LineSeries _speedZ = new LineSeries("Speed Z");

        public ViewModel()
        {
            this.Plot = new PlotModel { Title = "Accel Data" };
            this.Plot.Series.Add(new LineSeries("Acceleration"));
        }

        public void SetSeries(List<DataPoint> dataPoints)
        {
            ((Plot.Series[0]) as LineSeries).Points.Clear();
            ((Plot.Series[0]) as LineSeries).Points.AddRange(dataPoints);
            Invalidate();
        }

        public void Invalidate()
        {
            Plot.InvalidatePlot(true);
        }

        public void AddZ(double x, double y)
        {
            //_z.Points.Add(new DataPoint(x, y));
            //while (_z.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            //{
            //    _z.Points.RemoveAt(0);
            //}
        }
        public void AddSpeed(double x, double y)
        {
            //_speedZ.Points.Add(new DataPoint(x, y));
            //while (_speedZ.Points.Count > COMMAND_RATE * SECONDS_TO_REMEMBER)
            //{
            //    _speedZ.Points.RemoveAt(0);
            //}
        }

        public double GetZMin()
        {
            if (_z.Points.Count == 0) { return -1; }
            else { return _z.Points.Min(m => m.Y); }
        }

        public double GetZMax()
        {
            if (_z.Points.Count == 0) { return -1; }
            else { return _z.Points.Max(m => m.Y); }
        }

        public double GetZMedian()
        {
            if (_z.Points.Count == 0) { return -1; }
            else
            {
                return _z.Points.Average(a => a.Y);
                //int count = _z.Points.Count();
                //var orderedPoints = _z.Points.OrderBy(p => p.Y);
                //double median = _z.Points.ElementAt(count / 2).Y + orderedPoints.ElementAt((count - 1) / 2).Y;
                //median /= 2;
                //return median;
            }
        }

        public void Clear()
        {
            _z.Points.Clear();
            _speedZ.Points.Clear();

        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(new List<DataPoint>[2] { _z.Points, _speedZ.Points });
        }

        public int Deserialize(string serialized)
        {
            try
            {
                List<DataPoint>[] result = JsonConvert.DeserializeObject<List<DataPoint>[]>(serialized);
                Clear();
                _z.Points.AddRange(result[0]);
                _speedZ.Points.AddRange(result[1]);

                return (int)_z.Points.Max(p => p.X);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }

}
