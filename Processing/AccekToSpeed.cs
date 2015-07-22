using System;

namespace Processing
{
    public class AccelToSpeed
    {
        private double
            _median = 0,
            _min = Double.MaxValue,
            _max = Double.MinValue,
            _lastValue = 0;

        private double _currentSpeed;

        public void Reset()
        {
            _currentSpeed = 0;
            _lastValue = 0;
        }

        public void SetToDeadZone(double min, double max, double median)
        {
            _min = min;
            _max = max;
            _median = median;
        }

        public double Next(double inputValue, double secondsElapsed)
        {
            //if (inputValue > _min && inputValue < _max) { return _currentSpeed; }

            var currentValue = inputValue - _median;
            _currentSpeed += (_lastValue + currentValue) / 2 * secondsElapsed;
            _lastValue = currentValue;
            return _currentSpeed;
        }
    }

}