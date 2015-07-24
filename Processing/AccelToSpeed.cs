using System;

namespace Processing
{
    public class AccelToSpeed
    {
        private double
            _average = 0,
            _lastValue = 0;

        private double _currentSpeed;

        public void Reset()
        {
            _currentSpeed = 0;
            _lastValue = 0;
            _average = 0;
        }

        public void Calibrate(double average)
        {
            _average = average;
            _currentSpeed = 0;
        }

        public double Next(double inputValue, double secondsElapsed)
        {
            var currentValue = inputValue - _average;
            _currentSpeed += (_lastValue + currentValue) / 2 * secondsElapsed;
            _lastValue = currentValue;
            return _currentSpeed;
        }
    }

    public class AccelToJerk
    {
        private double _lastJerk = 0;
        private double _lastAccel = 0;

        public void Reset()
        {
            _lastAccel = 0;
        }

        public double Next(double accel, double secondsElapsed)
        {
            if (secondsElapsed == 0) { return _lastJerk; }

            var jerk = (accel - _lastAccel) / secondsElapsed;
            _lastAccel = accel;
            _lastJerk = jerk;
            return jerk;
        }
    }
}