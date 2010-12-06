namespace SaltScript
{
    /// <summary>
    /// A boolean type that allows undetermined (for when a function is not sure of the correct value).
    /// </summary>
    public enum FuzzyBool
    {
        True,
        False,
        Undetermined
    }

    /// <summary>
    /// Logic functions for fuzzy bool.
    /// </summary>
    public static class FuzzyBoolLogic
    {
        public static FuzzyBool Not(FuzzyBool A)
        {
            if (A == FuzzyBool.False)
                return FuzzyBool.True;
            if (A == FuzzyBool.True)
                return FuzzyBool.False;
            return FuzzyBool.Undetermined;
        }

        public static FuzzyBool And(FuzzyBool A, FuzzyBool B)
        {
            if (A == FuzzyBool.False || B == FuzzyBool.False)
                return FuzzyBool.False;
            if (A == FuzzyBool.True && B == FuzzyBool.True)
                return FuzzyBool.True;
            return FuzzyBool.Undetermined;
        }

        public static FuzzyBool Or(FuzzyBool A, FuzzyBool B)
        {
            if (A == FuzzyBool.True || B == FuzzyBool.True)
                return FuzzyBool.True;
            if (A == FuzzyBool.False && B == FuzzyBool.False)
                return FuzzyBool.False;
            return FuzzyBool.Undetermined;
        }
    }
}