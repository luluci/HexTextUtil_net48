using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexTextUtil.HexText.HexTextLoader
{
    internal class MotS : IHexFileLoader
    {
        private bool disposedValue;
        System.IO.StreamReader reader = null;
        private UInt32 relAddress = 0;
        public LoadStatus Status { get; set; } = LoadStatus.Loading;

        public MotS(string path)
        {
            reader = new System.IO.StreamReader(path);
        }

        public bool EOF => reader is null;
        public HexTextFileFormat FileFormat { get; } = HexTextFileFormat.MotS;

        public HexTextRecord Load()
        {
            // エラーチェック
            if (reader is null) return null;
            if (reader.EndOfStream)
            {
                //Status = LoadStatus.NotFoundEndRecord;
                Status = LoadStatus.Success;
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
            // フォーマットチェック
            if (line[0] != 'S')
            {
                Status = LoadStatus.DetectInvalidFormatLine;
                Dispose(true);
                return null;
            }
            // データ展開
            var temp = line.Substring(1);
            var buff = $"0{temp}";
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
            }
            catch (Exception)
            {
                return null;
            }

            return result;
        }

        // byte[]基準オフセット
        private enum RecordBytesOffset : UInt32
        {
            RecordType = 0,
            Length = 1,
            AddressOffset = 2,
        }
        private bool CheckSum(byte[] bytes)
        {
            // 総和算出
            byte checksum = 0;
            int i;
            for (i = 1; i < bytes.Length - 1; i++)
            {
                checksum += bytes[i];
            }
            // 1の補数
            checksum = (byte)(checksum ^ 0xFF);
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
                case 6:
                    return AnalyzeRecord6(line, bytes);
                case 7:
                    return AnalyzeRecord7(line, bytes);
                case 8:
                    return AnalyzeRecord8(line, bytes);
                case 9:
                    return AnalyzeRecord9(line, bytes);

                default:
                    return null;
            }
        }
        private HexTextRecord AnalyzeRecord0(string line, byte[] bytes)
        {
            // ファイル名
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord1(string line, byte[] bytes)
        {
            // S1レコードではアドレスは2バイト。
            return AnalyzeDataRecord(line, bytes, 2);
        }
        private HexTextRecord AnalyzeRecord2(string line, byte[] bytes)
        {
            // S2レコードではアドレスは3バイト。
            return AnalyzeDataRecord(line, bytes, 3);
        }
        private HexTextRecord AnalyzeRecord3(string line, byte[] bytes)
        {
            // S3レコードではアドレスは4バイト。
            return AnalyzeDataRecord(line, bytes, 4);
        }
        private HexTextRecord AnalyzeRecord4(string line, byte[] bytes)
        {
            // 予約レコード
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord5(string line, byte[] bytes)
        {
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord6(string line, byte[] bytes)
        {
            // とりあえず無視
            // 次のレコード取得
            return Load();
        }
        private HexTextRecord AnalyzeRecord7(string line, byte[] bytes)
        {
            // S7,S8,S9出現でファイル終了？
            // とりあえずEOFで正常終了扱いとする
            //Status = LoadStatus.Success;
            //Dispose(true);
            return Load();
        }
        private HexTextRecord AnalyzeRecord8(string line, byte[] bytes)
        {
            // S7,S8,S9出現でファイル終了？
            // とりあえずEOFで正常終了扱いとする
            //Status = LoadStatus.Success;
            //Dispose(true);
            return Load();
        }
        private HexTextRecord AnalyzeRecord9(string line, byte[] bytes)
        {
            // S7,S8,S9出現でファイル終了？
            // とりあえずEOFで正常終了扱いとする
            //Status = LoadStatus.Success;
            //Dispose(true);
            return Load();
        }

        private HexTextRecord AnalyzeDataRecord(string line, byte[] bytes, int addressLen)
        {
            // 情報取得
            // データ長はアドレスサイズとチェックサム1バイトを含む
            int length = bytes[(UInt32)RecordBytesOffset.Length] - addressLen - 1;
            // データ位置はアドレスの次になる。
            int dataBegin = (int)RecordBytesOffset.AddressOffset + addressLen;
            int dataEnd = dataBegin + length;
            // レコード情報作成
            var result = new HexTextRecord();
            result.Address = GetAddressOffset(bytes, addressLen) + relAddress;
            result.Record = bytes;
            result.Data = new ArraySegment<byte>(result.Record, dataBegin, dataEnd - dataBegin).ToArray();
            result.DataStr = line.Substring(dataBegin * 2, length * 2);
            return result;
        }
        private UInt32 GetAddressOffset(byte[] bytes, int addressLen)
        {
            UInt32 addr = 0;
            for (int i = 0; i < addressLen; i++)
            {
                addr <<= 8;
                addr += bytes[(UInt32)RecordBytesOffset.AddressOffset + i];
            }
            return addr;
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
