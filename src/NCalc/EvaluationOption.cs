using System;

namespace NCalc
{
    // Summary:
    //     Provides enumerated values to use to set evaluation options.
    [Flags]
    public enum EvaluateOptions
    {
        // Summary:
        //     Specifies that no options are set.
        None = 1 << 0,
        //
        // Summary:
        //     Specifies case-insensitive matching.
        IgnoreCase = 1 << 1,
        //
        // Summary:
        //     No-cache mode. Ingores any pre-compiled expression in the cache.
        NoCache = 1 << 2,
        //
        // Summary:
        //     Treats parameters as arrays and result a set of results.
        IterateParameters = 1 << 3,
        //
        // Summary:
        //     When using Round(), if a number is halfway between two others, it is rounded toward the nearest number that is away from zero.
        RoundAwayFromZero = 1 << 4,
        //
        // Summary:
        //     Ignore case on string compare
        MatchStringsWithIgnoreCase = 1 << 5,
        //
        // Summary:
        //     Use ordinal culture on string compare
        MatchStringsOrdinal = 1 << 6,
        //
        // Summary:
        //     Use checked math
        OverflowProtection = 1 << 7,

        /// <summary>
        ///     Allow calculation with boolean values.
        /// </summary>
        BooleanCalculation = 1 << 8,

        /// <summary>
        ///     When using Abs(), return a double instead of a decimal.
        /// </summary>
        UseDoubleForAbsFunction = 1 << 9
    }
}
