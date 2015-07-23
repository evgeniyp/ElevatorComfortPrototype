using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Text;
using Processing;

namespace ElevatorComfort
{
    public struct DataSample
    {
        public double X,     // raw X acceleration
                      Y,     // raw Y acceleration
                      Z,     // raw Z acceleration
                      T,     // timestamp
                      DT,    // delta time
                      Acc,   // calculated length of acceleration vector
                      Speed, // calculated speed
                      Jerk,  // calculated jerk
                      Vibr;  // calculated vibration
    }

    public class Model
    {
        private readonly int _maxCount;
        private object _lock = new object();
        private double _lastT;
        private List<DataSample> _samples = new List<DataSample>();

        private TwoPoleButterworthFilter _filter = new TwoPoleButterworthFilter();
        private AccelToSpeed _accelToSpeed = new AccelToSpeed();

        private bool _calibrated = false;

        public Model(int maxCount = 100)
        {
            _maxCount = maxCount;
        }

        public void AddXYZ(double x, double y, double z, double t)
        {
            lock (_lock)
            {
                var deltaTime = _lastT == 0 ? 0 : t - _lastT;

                var acc = Math.Sqrt(x * x + y * y + z * z);

                if (_lastT == 0)
                {
                    _filter.Next(acc);
                    _filter.Next(acc);
                    _filter.Next(acc);
                }
                acc = _filter.Next(acc);

                var speed = _calibrated ? _accelToSpeed.Next(acc, t) : 0;

                _samples.Add(new DataSample() { X = x, Y = y, Z = z, T = t, DT = deltaTime, Acc = acc, Speed = speed });
                _lastT = t;
                while (_samples.Count > _maxCount) { _samples.RemoveAt(0); }
            }
        }

        public DataSample[] GetPoints()
        {
            lock (_lock)
            {
                var result = new DataSample[_samples.Count];
                _samples.CopyTo(result);
                return result;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _accelToSpeed.Reset();
                _lastT = 0;
                _samples.Clear();
                _calibrated = false;
            }
        }

        public void Calibrate()
        {
            lock (_lock)
            {
                _accelToSpeed.SetToDeadZone(_samples.Average(a => a.Acc));
                _calibrated = true;
            }
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(_samples);
        }

        public void Deserialize(string s)
        {
            _samples = JsonConvert.DeserializeObject<List<DataSample>>(s);
        }
    }
}
