using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexTextUtil.HexText.HexTextLoader
{
    internal class IntelHex : IHexFileLoader
    {
        private bool disposedValue;
        System.IO.StreamReader reader = null;
        private UInt32 relAddress = 0;
        public LoadStatus Status { get; set; } = LoadStatus.Loading;

        public IntelHex(string path)
        {
            reader = new System.IO.StreamReader(path);
        }

        public bool EOF => reader is null;
        public HexTextFileFormat FileFormat { get; } = HexTextFileFormat.IntelHex;

        public HexTextRecord Load()
        {
            // エラーチェック
            if (reader is null) return null;
            if (reader.EndOfStream)
            {
                Status = LoadStatus.NotFoundEndRecord;
                Dispose(true);
                return null;
            }
            // ファイル読み出しチェック
            var line = reader.ReadLine();
            if (line is null)
            {
                Status = LoadStatus.ReadFileError;
                Dispose(true);
                return null;
            }
            if (line[0] != ':')
            {
                Status = LoadStatus.DetectInvalidFormatLine;
                Dispose(true);
                return null;
            }
            // データ展開
            var buff = line.Substring(1);
            var bytes = ConverBytes(buff);
            if (bytes is null)
            {
                Status = LoadStatus.DetectInvalidFormatLine;
                Dispose(true);
                return null;
            }
            // チェックサムチェック
            if (!CheckSum(bytes))
            {
                Status = LoadStatus.DetectCheckSumError;
                Dispose(true);
                return null;
            }
            return AnalyzeRecord(buff, bytes);
        }

        private static byte[] ConverBytes(string line)
        {
            byte[] result = new byte[line.Length / 2];

            int cur = 0;

            try
            {
                for (int i = 0; i < line.Length; i = i + 2)
                {
                    string w = line.Substring(i, 2);
                    result[cur] = Convert.ToByte(w, 16);
                    cur++;
                }
            }catch (Exception)
            {
                return null;
            }

            return result;
        }

        // byte[]基準オフセット
        private enum RecordBytesOffset : UInt32
        {
            Length = 0,
            AddressOffsetHi = 1,
            AddressOffsetLo = 2,
            RecordType = 3,
            Data = 4,
        }
        private bool CheckSum(byte[] bytes)
        {
            // 総和算出
            byte checksum = 0;
            int i;
            for (i = 0; i < bytes.Length - 1; i++)
            {
                checksum += bytes[i];
            }
            // 2の補数
            checksum = (byte)((checksum ^ 0xFF) + 1);
            // チェックサム比較
            return (checksum == bytes[i]);
        }

        private HexTextRecord AnalyzeRecord(string line, byte[] bytes)
        {
            switch (bytes[(UInt32)RecordBytesOffset.RecordType])
            {
                case 0:
                    return AnalyzeRecord0(line, bytes);
                case 1:
                    return AnalyzeRecord1(line, bytes);
                case 2:
                    return AnalyzeRecord2(line, bytes);
                case 3:
                    return AnalyzeRecord3(line, bytes);
                case 4:
                    return AnalyzeRecord4(line, bytes);
                case 5:
                    return AnalyzeRecord5(line, bytes);

                default:
                    return null;
            }
        }
        private HexTextRecord AnalyzeRecord0(string line, byte[] bytes)
        {
            // 情報取得
            int length = bytes[(UInt32)RecordBytesOffset.Length];
            int dataBegin = (int)RecordBytesOffset.Data;
            int dataEnd = dataBegin + length;
            // レコード情報作成
            var result = new HexTextRecord();
            result.Address = GetAddressOffset(bytes) + relAddress;
            result.Record = bytes;
            result.Data = new ArraySegment<byte>(result.Record, dataBegin, dataEnd).ToArray();
            result.DataStr = line.Substring(dataBegin * 2, length * 2);
            return result;
        }
        private HexTextRecord AnalyzeRecord1(string line, byte[] bytes)
        {
            Status = LoadStatus.Success;
            Dispose(true);
            return null;
        }
        private HexTextRecord AnalyzeRecord2(string line, byte[] bytes)
        {
            // 拡張セグメントアドレス更新
            var extend = GetExtendAddress(bytes);
            relAddress = extend << 4;
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord3(string line, byte[] bytes)
        {
            // CS:IPレジスタ
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord4(string line, byte[] bytes)
        {
            // 拡張リニアアドレス更新
            var extend = GetExtendAddress(bytes);
            relAddress = extend << 16;
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord5(string line, byte[] bytes)
        {
            // EIPレジスタ
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }

        private UInt32 GetAddressOffset(byte[] bytes)
        {
            return ((UInt32)bytes[(UInt32)RecordBytesOffset.AddressOffsetHi] << 8) | (UInt32)bytes[(UInt32)RecordBytesOffset.AddressOffsetLo];
        }
        private UInt32 GetExtendAddress(byte[] bytes)
        {
            return ((UInt32)bytes[(UInt32)RecordBytesOffset.Data] << 8) | (UInt32)bytes[(UInt32)RecordBytesOffset.Data+1];
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    reader?.Dispose();
                    reader = null;
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~IntelHex()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
