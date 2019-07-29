/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Globalization;

namespace QuantConnect
{
    /// <summary>
    /// Provides extension methods for properly parsing and serializing values while properly using
    /// an IFormatProvider/CultureInfo when applicable
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Parses the specified string as <see cref="long"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static T ConvertInvariant<T>(this object convertible)
        {
            return (T) convertible.ConvertInvariant(typeof(T));
        }

        /// <summary>
        /// Parses the specified string as <see cref="long"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static object ConvertInvariant(this object convertible, Type conversionType)
        {
            return Convert.ChangeType(convertible, conversionType, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="DateTime"/> using <see cref="DateTime.ParseExact(string,string,System.IFormatProvider)"/>
        /// with the specified <paramref name="format"/> and <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static DateTime ParseDateTimeInvariant(this string value)
        {
            return DateTime.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="DateTime"/> using <see cref="DateTime.ParseExact(string,string,System.IFormatProvider)"/>
        /// with the specified <paramref name="format"/> and <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static DateTime ParseDateTimeExactInvariant(this string value, string format)
        {
            return DateTime.ParseExact(value, format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="double"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static double ParseDoubleInvariant(this string value)
        {
            return double.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="decimal"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static decimal ParseDecimalInvariant(this string value)
        {
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="int"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static int ParseIntInvariant(this string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the provided value as a <see cref="long"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static long ParseLongInvariant(this string value)
        {
            return long.Parse(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts the provided value to a string using <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static string ToStringInvariant(this IConvertible convertible)
        {
            if (convertible == null)
            {
                return string.Empty;
            }

            return convertible.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats the provided value using the specified <paramref name="format"/> and
        /// <see cref="CultureInfo.InvariantCulture"/>
        /// </summary>
        public static string ToStringInvariant(this IFormattable formattable, string format)
        {
            if (formattable == null)
            {
                return string.Empty;
            }

            return formattable.ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Checks if the string starts with the provided <paramref name="beginning"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// while optionally ignoring case.
        /// </summary>
        public static bool StartsWithInvariant(this string value, string beginning, bool ignoreCase = false)
        {
            return value.StartsWith(beginning, ignoreCase, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// checks if the string ends with the provided <paramref name="ending"/> using <see cref="CultureInfo.InvariantCulture"/>
        /// while optionally ignoring case.
        /// </summary>
        public static bool EndsWithInvariant(this string value, string ending, bool ignoreCase = false)
        {
            return value.EndsWith(ending, ignoreCase, CultureInfo.CurrentCulture);
        }
    }
}
