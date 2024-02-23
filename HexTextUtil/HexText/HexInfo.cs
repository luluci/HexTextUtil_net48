using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HexTextUtil.HexText
{
    internal class MemoryRecord
    {
        public const UInt32 RecordSize = 16;

        [Flags]
        public enum HasData
        {
            Empty = 0,
            Byte0 = 0x0001,
            Byte1 = 0x0002,
            Byte2 = 0x0004,
            Byte3 = 0x0008,
            Byte4 = 0x0010,
            Byte5 = 0x0020,
            Byte6 = 0x0040,
            Byte7 = 0x0080,
            Byte8 = 0x0100,
            Byte9 = 0x0200,
            ByteA = 0x0400,
            ByteB = 0x0800,
            ByteC = 0x1000,
            ByteD = 0x2000,
            ByteE = 0x4000,
            ByteF = 0x8000,
        }
        private HasData[] HasDataTable = new HasData[]
        {
            HasData.Byte0, HasData.Byte1, HasData.Byte2, HasData.Byte3, HasData.Byte4, HasData.Byte5, HasData.Byte6, HasData.Byte7,
            HasData.Byte8, HasData.Byte9, HasData.ByteA, HasData.ByteB, HasData.ByteC, HasData.ByteD, HasData.ByteE, HasData.ByteF,
        };

        public UInt32 Address { get; set; } = 0;
        private HasData HasDataFlag = HasData.Empty;
        public byte[] Data { get; set; }

        public MemoryRecord()
        {
            Data = new byte[RecordSize];
        }

        public byte? this[UInt32 index]
        {
            get
            {
                //if (index >= RecordSize) return null;
                if (HasDataFlag.HasFlag(HasDataTable[index]))
                {
                    return Data[index];
                }
                return null;
            }
        }

        public void Apply(HexTextLoader.HexTextRecord record)
        {
            // Addressを基準とした相対位置
            UInt64 relMem = 0;
            // レコードに反映するデータのアドレスを算出
            // (Begin, End]
            // 開始アドレス
            UInt64 addressBegin = Address;
            if (addressBegin < record.Address)
            {
                addressBegin = record.Address;
                relMem = record.Address - Address;
            }
            // 終了アドレス
            UInt64 addressEnd = (UInt64)Address + RecordSize;
            UInt64 recordMaxAddress = (UInt64)record.Address + (UInt64)record.Data.Length;
            if (addressEnd > recordMaxAddress)
            {
                addressEnd = recordMaxAddress;
            }
            // record上の相対位置作成
            UInt64 relRec = addressBegin - record.Address;
            // レコードに反映
            for (var addr = addressBegin; addr < addressEnd; addr++, relMem++, relRec++)
            {
                Data[relMem] = record.Data[relRec];
                HasDataFlag |= HasDataTable[relMem];
            }
        }

        static public UInt32[] MakeAddresses(HexTextLoader.HexTextRecord record)
        {
            // MemoryRecordが保持するデータサイズに応じて、
            // HexTextRecordをいくつのMemoryRecordに分けて格納するかを計算する。
            int len = (int)((record.Address & 0xF) + record.Data.Length);
            int count = (int)(len / RecordSize);
            if (len % RecordSize != 0)
            {
                count++;
            }
            // 返り値用バッファ作成
            UInt32[] result = new UInt32[count];
            // Address作成
            var addr = record.Address & 0xFFFFFFF0;
            result[0] = addr;
            for (int i = 1; i < count; i++)
            {
                addr += 0x10;
                result[i] = addr;
            }
            return result;
        }
    }

    internal class HexInfo : IDisposable
    {
        private bool disposedValue;

        public UInt32 AddressBegin = UInt32.MaxValue;
        public UInt32 AddressEnd = 0;
        private Dictionary<UInt32, MemoryRecord> memoryMap;
        public HexTextLoader.HexTextFileFormat FileFormat { get; set; }

        public HexInfo()
        {
            memoryMap = new Dictionary<uint, MemoryRecord>();
        }

        public HexTextLoader.LoadStatus Load(string path)
        {
            // Loader作成
            using (var loader = HexTextLoader.HexFileLoader.HexFileLoaderFactory(path))
            {
                if (loader is null) return HexTextLoader.LoadStatus.ReadFileError;
                FileFormat = loader.FileFormat;
                // HexTextの内容解析
                while (!loader.EOF)
                {
                    // レコード取得
                    var record = loader.Load();
                    if (record is null)
                    {
                        // null出現で解析終了
                        // Statusで正常終了か異常終了か判断する
                        return loader.Status;
                    }
                    // レコード展開
                    LoadRecord(record);
                }
            }

            return HexTextLoader.LoadStatus.NotFoundEndRecord;
        }

        private void LoadRecord(HexTextLoader.HexTextRecord record)
        {
            // 有効アドレス範囲更新
            if (record.Address < AddressBegin)
            {
                AddressBegin = record.Address;
            }
            if ((record.Address + record.Data.Length) > AddressEnd)
            {
                AddressEnd = record.Address + (UInt32)record.Data.Length - 1;
            }
            // hexコンテナ更新
            // key作成
            var keys = MemoryRecord.MakeAddresses(record);
            foreach (var key in keys)
            {
                // value存在チェック
                if (!memoryMap.ContainsKey(key))
                {
                    memoryMap.Add(key, new MemoryRecord() { Address = key });
                }
                // record反映
                memoryMap[key].Apply(record);
            }
        }

        public UInt64 CalcCheckSum(UInt32 begin, UInt32 end, byte blank)
        {
            if (begin > end)
            {
                MessageBox.Show($"チェックサム計算アドレスの設定が不正です: Begin({begin}) > End({end})");
                return 0;
            }
            UInt64 checksum = 0;
            // MemoryRecordインデックスを計算
            var key = GetMemoryMapKey(begin);
            // MemoryRecord(16バイトバッファ)に対しての計算インデックス
            // 最初のMemoryRecordは途中から開始する可能性がある
            UInt32 memIndexBegin = begin - key;
            UInt32 memIndexEnd = 16;
            if (end - key < 16)
            {
                // MemoryRecordの途中まで使う場合
                memIndexEnd = end - key + 1;
            }
            // Uint32の最大値までアドレスが入るので、計算対象アドレスの超過判定のために一つ大きい型で超過判定をする
            UInt64 addrCurr = key;
            UInt64 addrEnd = end;
            while (addrCurr <= addrEnd)
            {
                // Memory読み出し
                if (memoryMap.ContainsKey(key))
                {
                    // keyが存在するとき
                    var mem = memoryMap[key];
                    for (var i = memIndexBegin; i < memIndexEnd; i++)
                    {
                        var data = mem[i];
                        if (!(data is null))
                        {
                            checksum += (UInt64)data;
                        }
                        else
                        {
                            checksum += blank;
                        }
                    }
                }
                else
                {
                    // keyが存在しないときはすべてblank
                    for (var i = memIndexBegin; i < memIndexEnd; i++)
                    {
                        checksum += blank;
                    }
                }
                // 計算アドレス情報更新
                addrCurr += 0x10;
                // keyがオーバーフローする可能性があるが、そのときはwhileも終了するので問題なし
                key += 0x10;
                memIndexBegin = 0;
                memIndexEnd = 16;
                if (end - key < 16)
                {
                    memIndexEnd = end - key + 1;
                }
            }

            return checksum;
        }
        private static UInt32 GetMemoryMapKey(UInt32 addr)
        {
            // MemoryRecordは16バイト単位で構成するので、アドレスの下位4ビットを捨てた値を基準点とする
            UInt32 result = addr & (UInt32)0xFFFFFFF0;
            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~HexText()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            //GC.SuppressFinalize(this);
        }
    }
}
