using System;

namespace CS2RuntimeProfiler.Core
{
    public static class SimulationEfficiency
    {
        public static double Calculate(double selectedSpeed, double actualSpeed)
        {
            if (selectedSpeed <= 0)
                return 0;

            return Math.Max(0, actualSpeed / selectedSpeed);
        }
    }
}
