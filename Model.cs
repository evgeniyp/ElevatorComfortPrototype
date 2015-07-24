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

                    _accelToJerk.Next(acc, deltaTimestamp / 1000);
                }
                acc = _filter.Next(acc);

                var jerk = _accelToJerk.Next(acc, deltaTimestamp / 1000);

                var speed = _calibrated ? _accelToSpeed.Next(acc, deltaTimestamp) : 0;

                _samples.Add(new DataSample() { X = x, Y = y, Z = z, Timestamp = timestamp, DT = deltaTimestamp, Acc = acc, Speed = speed / GRAVITY_ACC, Jerk = jerk });
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
                _samples.Clear();
                _calibrated = false;
            }
        }

        public void Calibrate()
        {
            lock (_lock)
            {
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
