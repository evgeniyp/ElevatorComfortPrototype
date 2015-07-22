using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ElevatorComfort
{
    public class Model
    {
        private const int MAX_COUNT = 10000;

        private List<DataPoint> _accelList = new List<DataPoint>();
        private object _accelLock = new object();
        public void AddAccel(double x, double y)
        {
            lock (_accelLock)
            {
                _accelList.Add(new DataPoint(x, y));
                while (_accelList.Count > MAX_COUNT)
                    _accelList.RemoveAt(0);
            }
        }
        public List<DataPoint> GetAccel()
        {
            lock (_accelLock)
            {
                var result = new List<DataPoint>();
                result.AddRange(_accelList);
                return result;
            }
        }
    }
}
