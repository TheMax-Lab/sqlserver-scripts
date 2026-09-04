using System;

namespace TheMaxLab.SqlServerDiagnostics.Core.Models
{
    public sealed class DiagnosticColumn
    {
        public string Name { get; set; }
        public string Key { get; set; }
        public string DataTypeName { get; set; }
        public Type DataType { get; set; }
        public int Ordinal { get; set; }
    }
}