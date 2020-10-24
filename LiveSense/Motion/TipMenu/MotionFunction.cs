using System;

namespace LiveSense.Motion.TipMenu
{
    public enum MotionFunction
    {
        Triangle,
        Sine,
        DoubleBounce,
        SharpBounce,
        Saw,
        Square,
        StepSine,
        StepSaw
    }

    public static class Extensions
    {
        public static float Calculate(this MotionFunction function, float time)
        {
            switch (function)
            {
                case MotionFunction.Triangle: return (float)Math.Abs(Math.Abs(time * 2 - 1.5) - 1);
                case MotionFunction.Sine: return (float)(-Math.Sin(time * Math.PI * 2) / 2 + 0.5);
                case MotionFunction.Saw: return time;
                case MotionFunction.Square: return (time == 0 || time == 1) ? 0.5f : (time < 0.5f ? 1 : 0);
                case MotionFunction.DoubleBounce:
                    {
                        var x = time * Math.PI * 2 - Math.PI / 4;
                        return (float)(-(Math.Pow(Math.Sin(x), 5) + Math.Pow(Math.Cos(x), 5)) / 2 + 0.5);
                    }
                case MotionFunction.SharpBounce:
                    {
                        var x = (time + 0.41957) * Math.PI / 2;
                        var s = Math.Sin(x) * Math.Sin(x);
                        var c = Math.Cos(x) * Math.Cos(x);
                        return (float)Math.Sqrt(Math.Max(c - s, s - c));
                    }
                case MotionFunction.StepSine:
                    {
                        var x = (float)(-Math.Sin(time * Math.PI * 2) / 2 + 0.5);
                        return (int)Math.Round(x * 4) / 4f;
                    }
                case MotionFunction.StepSaw: return (int)Math.Round(time * 4) / 4f;
                default: return 0;
            }
        }
    }
}
