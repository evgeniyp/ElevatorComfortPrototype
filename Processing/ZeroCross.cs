using System;

namespace Processing
{
    public class ZeroCrossMaximum
    {
        private double _currentMax;
        private double _lastMax;
        private double _lastValue;

        public void Reset()
        {
            _currentMax = 0;
            _lastMax = 0;
            _lastValue = 0;
        }

        public double Next(double value)
        {
            if (Math.Sign(_lastValue) != Math.Sign(value) && _lastValue != 0)
            {
                _lastMax = _currentMax;
                _currentMax = 0;
            }
            else
            {
                if (Math.Abs(value) > _currentMax)
                {
                    _currentMax = Math.Abs(value);
                }
            }

            _lastValue = value;
            return _lastMax;
        }
    }
}