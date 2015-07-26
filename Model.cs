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
        public double X,         // raw X acceleration
                      Y,         // raw Y acceleration
                      Z,         // raw Z acceleration
                      Timestamp, // timestamp
                      DT,        // delta time
                      Acc,       // calculated length of acceleration vector
                      Speed,     // calculated speed
                      Jerk,      // calculated jerk
                      Vibr;      // calculated vibration
    }

    public class Model
    {
        private const double GRAVITY_ACC = 9.8;

        private readonly int _maxCount;
        private object _lock = new object();
        private List<DataSample> _samples = new List<DataSample>();

        private TwoPoleButterworthFilter _filter = new TwoPoleButterworthFilter();
        private AccelToSpeed _accelToSpeed = new AccelToSpeed();
        private AccelToJerk _accelToJerk = new AccelToJerk();
        private ZeroCrossMaximum _vibrationCounter = new ZeroCrossMaximum();

        private bool _calibrated = false;

        public Model(int maxCount)
        {
            _maxCount = maxCount;
        }

        public void AddXYZ(double x, double y, double z, double timestamp, double deltaTimestamp)
        {
            lock (_lock)
            {
                var acc = _filter.Next(z);

                if (timestamp == 0)
                {
                    _filter.Next(acc);
                    _filter.Next(acc);

                    _accelToJerk.Next(acc, deltaTimestamp);
                }
                acc = _filter.Next(acc);

                var jerk = _accelToJerk.Next(acc, deltaTimestamp);
                var speed = _calibrated ? _accelToSpeed.Next(acc, deltaTimestamp) * GRAVITY_ACC : 0;
                var vibr = _vibrationCounter.Next(acc);

                _samples.Add(new DataSample() {
                    X = x,
                    Y = y,
                    Z = z,
                    Timestamp = timestamp,
                    DT = deltaTimestamp,
                    Acc = acc,
                    Speed = speed,
                    Jerk = jerk / 100,
                    Vibr = vibr
                });
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
                _vibrationCounter.Reset();
                _accelToSpeed.Reset();
                _samples.Clear();
                _calibrated = false;
            }
        }

        public void Calibrate()
        {
            lock (_lock)
            {
                if (_samples.Count == 0) { return; }
                _accelToSpeed.Calibrate(_samples.Average(a => a.Z));
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
