using System;
using System.Collections.Generic;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class SqlServerVersion : IComparable<SqlServerVersion>
    {
        private static readonly IDictionary<int, int> YearToMajor = new Dictionary<int, int>
        {
            { 2012, 11 }, { 2014, 12 }, { 2016, 13 }, { 2017, 14 },
            { 2019, 15 }, { 2022, 16 }, { 2025, 17 }
        };

        public SqlServerVersion(int major, int minor, int build, int revision)
        {
            if (major <= 0) throw new ArgumentOutOfRangeException("major");
            Major = major; Minor = minor; Build = build; Revision = revision;
        }

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Build { get; private set; }
        public int Revision { get; private set; }

        public static SqlServerVersion Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new FormatException("A SQL Server version is required.");
            int year;
            if (int.TryParse(value.Trim(), out year) && YearToMajor.ContainsKey(year)) return new SqlServerVersion(YearToMajor[year], 0, 0, 0);
            Version version;
            if (!Version.TryParse(value.Trim(), out version)) throw new FormatException("Invalid SQL Server version: " + value);
            return new SqlServerVersion(version.Major, Math.Max(0, version.Minor), Math.Max(0, version.Build), Math.Max(0, version.Revision));
        }

        public int CompareTo(SqlServerVersion other)
        {
            if (other == null) return 1;
            int comparison = Major.CompareTo(other.Major);
            if (comparison != 0) return comparison;
            comparison = Minor.CompareTo(other.Minor);
            if (comparison != 0) return comparison;
            comparison = Build.CompareTo(other.Build);
            return comparison != 0 ? comparison : Revision.CompareTo(other.Revision);
        }

        public override string ToString() { return string.Format("{0}.{1}.{2}.{3}", Major, Minor, Build, Revision); }
    }
}