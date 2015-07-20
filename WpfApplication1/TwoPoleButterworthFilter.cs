using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Filters
{
    // Digital filter designed by mkfilter/mkshape/gencode   A.J. Fisher
    // Command line: /www/usr/fisher/helpers/mkfilter -Bu -Lp -o 2 -a 1.6666666667e-01 0.0000000000e+00 -l
    public class TwoPoleButterworthFilter
    {
        private bool _firstStart = true;

        private const int NZEROS = 2;
        private const int NPOLES = 2;
        private const double GAIN = 6.449489743e+00;

        private double[] xv = new double[NZEROS + 1];
        private double[] yv = new double[NPOLES + 1];

        public double Next(double inputValue)
        {
            if (_firstStart)
            {
                _firstStart = false;
                for (int i = 0; i < 10; i++) { _Next(inputValue); }
            }
            return _Next(inputValue);
        }

        private double _Next(double inputValue)
        {
            xv[0] = xv[1];
            xv[1] = xv[2];
            xv[2] = inputValue / GAIN;
            yv[0] = yv[1];
            yv[1] = yv[2];
            yv[2] = (xv[0] + xv[2]) + 2 * xv[1] + (-0.2404082058 * yv[0]) + (0.6202041029 * yv[1]);
            return yv[2];
        }

    }

    public class AccelToSpeed
    {
        private double
            _median = 0,
            _min = Double.MaxValue,
            _max = Double.MinValue;

        private double _currentSpeed;

        public void Reset()
        {
            _currentSpeed = 0;
        }

        public void SetToDeadZone(double min, double max, double median)
        {
            _min = min;
            _max = max;
            _median = median;
        }

        public double Next(double inputValue)
        {
            if (inputValue < _min || inputValue > _max) { _currentSpeed += inputValue - _median; }
            return _currentSpeed;
        }
    }

    public class FilterButterworth2
    {
        /// <summary>
        /// rez amount, from sqrt(2) to ~ 0.1
        /// </summary>
        private readonly double resonance;

        private readonly double frequency;
        private readonly int sampleRate;
        private readonly PassType passType;

        private readonly double c, a1, a2, a3, b1, b2;

        /// <summary>
        /// Array of input values, latest are in front
        /// </summary>
        private double[] inputHistory = new double[2];

        /// <summary>
        /// Array of output values, latest are in front
        /// </summary>
        private double[] outputHistory = new double[3];

        public FilterButterworth2(double frequency = 5, int sampleRate = 100, PassType passType = PassType.Lowpass, double resonance = 1.4142135623730950488016887242097)
        {
            this.resonance = resonance;
            this.frequency = frequency;
            this.sampleRate = sampleRate;
            this.passType = passType;

            switch (passType)
            {
                case PassType.Lowpass:
                    c = 1.0f / (double)Math.Tan(Math.PI * frequency / sampleRate);
                    a1 = 1.0f / (1.0f + resonance * c + c * c);
                    a2 = 2f * a1;
                    a3 = a1;
                    b1 = 2.0f * (1.0f - c * c) * a1;
                    b2 = (1.0f - resonance * c + c * c) * a1;
                    break;
                case PassType.Highpass:
                    c = (double)Math.Tan(Math.PI * frequency / sampleRate);
                    a1 = 1.0f / (1.0f + resonance * c + c * c);
                    a2 = -2f * a1;
                    a3 = a1;
                    b1 = 2.0f * (c * c - 1.0f) * a1;
                    b2 = (1.0f - resonance * c + c * c) * a1;
                    break;
            }
        }

        public enum PassType
        {
            Highpass,
            Lowpass,
        }

        public double Next(double newInput)
        {
            double newOutput = a1 * newInput + a2 * this.inputHistory[0] + a3 * this.inputHistory[1] - b1 * this.outputHistory[0] - b2 * this.outputHistory[1];

            this.inputHistory[1] = this.inputHistory[0];
            this.inputHistory[0] = newInput;

            this.outputHistory[2] = this.outputHistory[1];
            this.outputHistory[1] = this.outputHistory[0];
            this.outputHistory[0] = newOutput;

            return this.outputHistory[0];
        }
    }
}

