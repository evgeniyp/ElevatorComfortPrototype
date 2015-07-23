﻿using Newtonsoft.Json;
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
        public PlotModel Plot { get; private set; }

        private LineSeries _x = new LineSeries() { Color = OxyColor.FromRgb(150, 255, 150), StrokeThickness = 1, Title = "[1] raw X" },
                           _y = new LineSeries() { Color = OxyColor.FromRgb(150, 150, 255), StrokeThickness = 1, Title = "[2] raw Y" },
                           _z = new LineSeries() { Color = OxyColor.FromRgb(255, 150, 150), StrokeThickness = 1, Title = "[3] raw Z" },
                           _dt = new LineSeries() { Color = OxyColor.FromRgb(255, 200, 100), StrokeThickness = 1, Title = "[4] time interval" },
                           _acc = new LineSeries() { Color = OxyColor.FromRgb(127, 127, 127), StrokeThickness = 2, Title = "[5] acceleration" },
                           _speed = new LineSeries() { Color = OxyColor.FromRgb(0, 255, 0), StrokeThickness = 2, Title = "[6] speed" },
                           _jerk = new LineSeries() { Color = OxyColor.FromRgb(255, 0, 0), StrokeThickness = 2, Title = "[7] jerk" },
                           _vibr = new LineSeries() { Color = OxyColor.FromRgb(0, 0, 255), StrokeThickness = 2, Title = "[8] vibration" };

        public ViewModel()
        {
            Plot = new PlotModel();
            Plot.Series.Add(_x);
            Plot.Series.Add(_y);
            Plot.Series.Add(_z);
            Plot.Series.Add(_dt);
            Plot.Series.Add(_acc);
            Plot.Series.Add(_speed);
            Plot.Series.Add(_jerk);
            Plot.Series.Add(_vibr);
        }

        public void SetSeries(DataSample[] dataSamples)
        {
            _x.Points.Clear();
            _y.Points.Clear();
            _z.Points.Clear();
            _dt.Points.Clear();
            _acc.Points.Clear();
            _speed.Points.Clear();
            _jerk.Points.Clear();
            _vibr.Points.Clear();

            for (int i = 0; i < dataSamples.Length; i++)
            {
                var s = dataSamples[i];

                _x.Points.Add(new DataPoint(s.T, s.X));
                _y.Points.Add(new DataPoint(s.T, s.Y));
                _z.Points.Add(new DataPoint(s.T, s.Z));
                _dt.Points.Add(new DataPoint(s.T, s.DT));
                _acc.Points.Add(new DataPoint(s.T, s.Acc));
                _speed.Points.Add(new DataPoint(s.T, s.Speed));
                _jerk.Points.Add(new DataPoint(s.T, s.Jerk));
                _vibr.Points.Add(new DataPoint(s.T, s.Vibr));
            }
        }

        public void ToggleVisibleSeries(int index)
        {
            Plot.Series[index].IsVisible = !Plot.Series[index].IsVisible;
        }

        public void Invalidate()
        {
            Plot.InvalidatePlot(true);
        }
    }

}