using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexTextUtil.HexText.HexTextLoader
{
    internal enum HexTextFileFormat
    {
        IntelHex,
        MotS,
    }

    internal class HexTextRecord
    {
        public UInt32 Address { get; set; } = 0;
        public string DataStr { get; set; } = string.Empty;
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public byte[] Record { get; set; } = Array.Empty<byte>();
    }

    internal enum LoadStatus {
        Loading = 0,
        Success,
        NotFoundEndRecord,          // エンドレコード検出前にEOF到達
        DetectInvalidFormatLine,
        ReadFileError,
        DetectCheckSumError,
    }

    internal interface IHexFileLoader : IDisposable
    {
        LoadStatus Status { get; set; }
        bool EOF { get; }
        HexTextFileFormat FileFormat { get; }

        HexTextRecord Load();
    }
}
