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
        private const int NZEROS = 2;
        private const int NPOLES = 2;
        private const double GAIN = 6.449489743e+00;

        private double[] xv = new double[NZEROS + 1];
        private double[] yv = new double[NPOLES + 1];

        public double Next(double inputValue)
        {
            xv[0] = xv[1];
            xv[1] = xv[2];
            xv[2] = inputValue / GAIN;
            yv[0] = yv[1]; yv[1] = yv[2];
            yv[2] = (xv[0] + xv[2]) + 2 * xv[1] + (-0.2404082058 * yv[0]) + (0.6202041029 * yv[1]);
            return yv[2];
        }

    }
}

