namespace Quarp.Extensions
{
    internal static class CvarExtinsions
    {
        public static void Bound(this Cvar cvar, float min, float max)
        {
            if (cvar.Value < min)
                Cvar.Set(cvar.Name, min);
            else if (cvar.Value > max)
                Cvar.Set(cvar.Name, max);
        }
    }
}
