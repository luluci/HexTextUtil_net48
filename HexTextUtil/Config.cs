using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System.Reactive.Disposables;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HexTextUtil
{

    public class Config : IDisposable
    {
        private CompositeDisposable disposables = new CompositeDisposable();
        private string configFilePath;
        public JsonItem json;

        private ObservableCollection<CheckSumSetting> checksumSettings;
        public ObservableCollection<CheckSumSetting> ChecksumSettings { get { return checksumSettings; } }

        public Config()
        {
            // パス設定
            configFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location + @".json";
            // 設定初期化
            // hex/motからの読み込み値を反映する設定をデフォルトでセット
            checksumSettings = new ObservableCollection<CheckSumSetting>();
            var def = new CheckSumSetting();
            def.Name = "<Manual>";
            def.AddressRangeFix = false;
            def.AddressRangeBeginText.Value = "<auto>";
            def.AddressRangeEndText.Value = "<auto>";
            checksumSettings.Add(def);
        }

        public void Load()
        {
            LoadImpl(false).Wait();
        }

        public async Task LoadAsync()
        {
            await LoadImpl(true);
        }

        /** 初回起動用に同期的に動作する
         * 
         */
        private async Task LoadImpl(bool configAwait = true)
        {
            // 設定ロード
            if (File.Exists(configFilePath))
            {
                // ファイルが存在する
                //
                var options = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    //Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    //NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
                };
                //
                using (var stream = new FileStream(configFilePath, FileMode.Open, FileAccess.Read))
                {
                    // 呼び出し元でWait()している。ConfigureAwait(false)無しにawaitするとデッドロックで死ぬ。
                    json = await JsonSerializer.DeserializeAsync<JsonItem>(stream, options).ConfigureAwait(configAwait);
                }
            }
            else
            {
                // ファイルが存在しない
                // テンプレートを作成
                json = new JsonItem()
                {
                    CheckSumSettings = new List<JsonCheckSumSetting>()
                    {
                        new JsonCheckSumSetting()
                        {
                            Name = "SampleSetting1",
                            AddressRange = new JsonAddressRange(){ Begin = "000000", End = "00FFFF"},
                            Blank = "FF",
                            Length = 2,
                            CalcTotal = false,
                            CalcTwosCompl = true,
                        }
                    }
                };
                // ファイルに保存
                Save();
            }
            // JSONデータを制御値に反映
            LoadCheckSumSettings();
        }

        private void LoadCheckSumSettings()
        {
            if (!(json is null) && !(json.CheckSumSettings is null))
            {
                foreach (var item in json.CheckSumSettings)
                {
                    var setting = new CheckSumSetting();
                    //
                    try
                    {
                        setting.Name = item.Name;
                        setting.AddressRangeBegin.Value = Convert.ToUInt32(item.AddressRange.Begin, 16);
                        setting.AddressRangeEnd.Value = Convert.ToUInt32(item.AddressRange.End, 16);
                        setting.Blank.Value = Convert.ToByte(item.Blank, 16);
                        setting.AddressRangeBeginText.Value = $"{item.AddressRange.Begin:8}";
                        setting.AddressRangeEndText.Value = $"{item.AddressRange.End:8}";
                        setting.BlankText.Value = item.Blank;
                        setting.Length.Value = LoadCheckSumSettingsLength(item.Length);
                        setting.LengthValue.Value = (uint)setting.Length.Value;
                        setting.CalcTotal.Value = item.CalcTotal;
                        setting.CalcTwosComp.Value = item.CalcTwosCompl;
                        setting.FormatTotal = item.FormatTotal;
                        setting.FormatTwosComp = item.FormatTwosCompl;
                        //
                        checksumSettings.Add(setting);
                    }
                    catch (Exception)
                    {
                        // 不正値はスルー
                    }
                }
            }
        }
        private CheckSumLength LoadCheckSumSettingsLength(uint inp)
        {
            switch (inp)
            {
                case 1: return CheckSumLength.Len1Byte;
                case 2: return CheckSumLength.Len2Byte;
                case 4: return CheckSumLength.Len4Byte;
                case 8: return CheckSumLength.Len8Byte;
                default: throw new Exception("invalid CheckSum Length, accept 1/2/4/8 byte");
            }
        }

        public void Save()
        {
            SaveImpl(false).Wait();
        }

        public async Task SaveAsync()
        {
            await SaveImpl(true);
        }

        public async Task SaveImpl(bool configAwait = true)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true,
            };
            //
            string jsonStr = JsonSerializer.Serialize(json, options);
            //
            using (var stream = new FileStream(configFilePath, FileMode.Create, FileAccess.Write))
            {
                // 呼び出し元でWait()している。ConfigureAwait(false)無しにawaitするとデッドロックで死ぬ。
                await JsonSerializer.SerializeAsync(stream, json, options).ConfigureAwait(configAwait);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    disposables.Dispose();
                }

                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    public enum CheckSumLength : uint
    {
        Len1Byte = 0,
        Len2Byte = 1,
        Len4Byte = 2,
        Len8Byte = 3,
    }
    public class CheckSumSetting : IDisposable, INotifyPropertyChanged
    {
        private bool disposedValue;
        private CompositeDisposable disposables = new CompositeDisposable();

        #region NotifyPropertyChanged
#pragma warning disable CS0067
        public event PropertyChangedEventHandler PropertyChanged;
        public void SetProperty<T>(ref T target, T value, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        {
            target = value;

            if (PropertyChanged == null)
                return;
            PropertyChangedEventArgs arg = new PropertyChangedEventArgs(caller);
            PropertyChanged.Invoke(this, arg);
        }
#pragma warning restore CS0067
        #endregion

        // Setting名称
        public string Name { get; set; } = string.Empty;
        // チェックサム計算アドレス範囲設定
        public bool AddressRangeFix { get; set; } = false;
        public ReactivePropertySlim<UInt32> AddressRangeBegin { get; set; } = new ReactivePropertySlim<UInt32>(0);
        public ReactivePropertySlim<UInt32> AddressRangeEnd { get; set; } = new ReactivePropertySlim<UInt32>(0);
        public ReactivePropertySlim<string> AddressRangeBeginText { get; set; } = new ReactivePropertySlim<string>(string.Empty);
        public ReactivePropertySlim<string> AddressRangeEndText { get; set; } = new ReactivePropertySlim<string>(string.Empty);
        // Blank
        public ReactivePropertySlim<byte> Blank { get; set; } = new ReactivePropertySlim<byte>(255);
        public ReactivePropertySlim<string> BlankText { get; set; } = new ReactivePropertySlim<string>("FF");
        // チェックサム長
        // 0:1byte, 1:2byte, 2:4byte, 3:8byte
        public ReactivePropertySlim<CheckSumLength> Length { get; set; } = new ReactivePropertySlim<CheckSumLength>(CheckSumLength.Len2Byte);
        public ReactivePropertySlim<uint> LengthValue { get; set; } = new ReactivePropertySlim<uint>((uint)CheckSumLength.Len2Byte);
        // チェックサム計算方法
        // 補数なし
        public ReactivePropertySlim<bool> CalcTotal { get; set; } = new ReactivePropertySlim<bool>(false);
        // 2の補数
        public ReactivePropertySlim<bool> CalcTwosComp { get; set; } = new ReactivePropertySlim<bool>(true);
        // チェックサム出力フォーマット
        public string FormatTotal { get; set; } = string.Empty;
        public string FormatTwosComp { get; set; } = string.Empty;

        public CheckSumSetting()
        {
            AddressRangeBegin
                .AddTo(disposables);
            AddressRangeEnd
                .AddTo(disposables);
            AddressRangeBeginText
                .Subscribe(addr =>
                {
                    if (addr == "<auto>")
                    {
                        // <auto> は何もしない
                    }
                    else
                    {
                        try
                        {
                            AddressRangeBegin.Value = Convert.ToUInt32(addr, 16);
                        }
                        catch (Exception)
                        {
                            AddressRangeBeginText.Value = $"{AddressRangeBegin.Value:X8}";
                        }
                    }
                })
                .AddTo(disposables);
            AddressRangeEndText
                .Subscribe(addr =>
                {
                    if (addr == "<auto>")
                    {
                        // <auto> は何もしない
                    }
                    else
                    {
                        try
                        {
                            AddressRangeEnd.Value = Convert.ToUInt32(addr, 16);
                        }
                        catch (Exception)
                        {
                            AddressRangeEndText.Value = $"{AddressRangeEnd.Value:X8}";
                        }
                    }
                })
                .AddTo(disposables);
            Blank
                .AddTo(disposables);
            BlankText
                .Subscribe(data =>
                {
                    try
                    {
                        Blank.Value = Convert.ToByte(data, 16);
                    }
                    catch (Exception)
                    {
                        BlankText.Value = $"{Blank.Value:X8}";
                    }
                })
                .AddTo(disposables);
            Length
                .AddTo(disposables);
            LengthValue
                .Subscribe(index =>
                {
                    switch (index)
                    {
                        case 0:
                            Length.Value = CheckSumLength.Len1Byte; break;
                        case 1:
                            Length.Value = CheckSumLength.Len2Byte; break;
                        case 2:
                            Length.Value = CheckSumLength.Len4Byte; break;
                        case 3:
                            Length.Value = CheckSumLength.Len8Byte; break;
                        default:
                            Length.Value = CheckSumLength.Len2Byte; break;
                    }
                })
                .AddTo(disposables);
            CalcTotal
                .AddTo(disposables);
            CalcTwosComp
                .AddTo(disposables);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                    disposables.Dispose();
                }

                // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // TODO: 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~CheckSumSetting()
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

    public class JsonItem
    {

        [JsonPropertyName("checksum_settings")]
        public IList<JsonCheckSumSetting> CheckSumSettings { get; set; }
    }

    public class JsonCheckSumSetting
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("address_range")]
        public JsonAddressRange AddressRange { get; set; } = new JsonAddressRange();

        [JsonPropertyName("blank")]
        public string Blank { get; set; } = "FF";

        [JsonPropertyName("length")]
        public uint Length { get; set; } = 1;

        [JsonPropertyName("calc_total")]
        public bool CalcTotal { get; set; } = false;
        [JsonPropertyName("calc_twos_compl")]
        public bool CalcTwosCompl { get; set; } = true;

        // OutputFormat
        [JsonPropertyName("fmt_total")]
        public string FormatTotal { get; set; } = string.Empty;
        [JsonPropertyName("fmt_twos_compl")]
        public string FormatTwosCompl { get; set; } = string.Empty;
    }

    public class JsonAddressRange
    {
        [JsonPropertyName("begin")]
        public string Begin { get; set; } = string.Empty;

        [JsonPropertyName("end")]
        public string End { get; set; } = string.Empty;
    }
}
