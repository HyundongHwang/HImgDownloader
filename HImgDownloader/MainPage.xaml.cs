using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using HtmlAgilityPack;
using System.Threading.Tasks;
using HUwpBaseLib.Extensions;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Security.Cryptography;
using System.Net.Http;
using System.Text;
using HUwpBaseLib.Utils;
using Newtonsoft.Json;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using Windows.System.Threading;

// 빈 페이지 항목 템플릿은 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 에 문서화되어 있습니다.

namespace HImgDownloader
{
    /// <summary>
    /// 자체에서 사용하거나 프레임 내에서 탐색할 수 있는 빈 페이지입니다.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<MainPageItem> _items = new ObservableCollection<MainPageItem>();
        private string _oldText;
        private List<Task> _tasks = new List<Task>();

        public MainPage()
        {
            this.InitializeComponent();
            this.LbObj.ItemsSource = _items;
            this.BtnClearAll.Click += BtnClearAll_Click;
            this.BtnRemoveDownloaded.Click += BtnRemoveDownloaded_Click;
        }

        private void BtnRemoveDownloaded_Click(object sender, RoutedEventArgs e)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];

                if (item.Progress == item.FileSize)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            _items.Clear();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            Clipboard.ContentChanged += _Clipboard_ContentChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Clipboard.ContentChanged -= _Clipboard_ContentChanged;
        }

        private async Task _ProcessPageAsync(string url)
        {
            Debug.WriteLine($"_ProcessPageAsync url : {url}");
            var resStr = "";

            try
            {
                resStr = await HublUtils.HttpGetStringAsync(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (string.IsNullOrWhiteSpace(resStr))
                return;



            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(resStr);
            var imgNodes = new List<HtmlNode>();
            doc.DocumentNode.HUBL_SelectNodes("img", ref imgNodes);
            doc.DocumentNode.HUBL_SelectNodes("a", ref imgNodes);

            if (imgNodes == null)
                return;



            foreach (var imgNode in imgNodes)
            {
                var srcStr = imgNode.GetAttributeValue("src", null);
                var srcUrl = "";

                if (string.IsNullOrEmpty(srcStr))
                    continue;



                if (srcStr.StartsWith("http"))
                {
                    srcUrl = srcStr;
                }
                else
                {
                    srcUrl = HublUtils.CombineUrl(url, srcStr);
                }

                if (string.IsNullOrEmpty(srcUrl))
                    continue;

                var ext = HublUtils.GetExt(srcUrl);

                if (ext != ".png" &&
                    ext != ".jpg" &&
                    ext != ".jpeg")
                    continue;



                Debug.WriteLine("srcUrl :" + srcUrl);
                var task = _DownloadImgAsync(srcUrl);
                _tasks.Add(task);

                while (_tasks.Count > 10)
                {
                    var taskFin = await Task.WhenAny(_tasks.ToArray());
                    _tasks.Remove(taskFin);
                }
            }

            await Task.WhenAll(_tasks.ToArray());
        }

        private async Task _DownloadImgAsync(string srcUrl)
        {
            try
            {
                var newFileName = Convert.ToBase64String(new UTF8Encoding().GetBytes(srcUrl)).Replace('/', '?');
                newFileName += HublUtils.GetExt(srcUrl);
                var newFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(Package.Current.DisplayName, CreationCollisionOption.OpenIfExists);


                if (!await newFolder.Hubl_ExistsAsync(newFileName))
                    return;



                var newFile = await newFolder.CreateFileAsync(newFileName, CreationCollisionOption.ReplaceExisting);
                var item = new MainPageItem();
                item.Url = srcUrl;
                item.Progress = 0;
                item.FileSize = 100;
                _items.Add(item);

                await HublUtils.HttpGetFileAsync(srcUrl, newFile, (prog, max) =>
                {
                    item.Progress = prog;
                    item.FileSize = max;
                });

                var bm = new BitmapImage();
                var bmStream = await newFile.OpenAsync(FileAccessMode.Read);
                await bm.SetSourceAsync(bmStream);
                item.FileBmSource = bm;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private async void _Clipboard_ContentChanged(object sender, object e)
        {
            DataPackageView dataPackageView = null;

            while (true)
            {
                if (dataPackageView != null)
                    break;

                try
                {
                    dataPackageView = Clipboard.GetContent();
                }
                catch (Exception ex)
                {
                    await Task.Delay(1000);
                }
            }

            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                Debug.WriteLine($"text : {text}");

                if (_oldText == text)
                    return;

                _oldText = text;

                if (!text.StartsWith("http"))
                    return;



                try
                {
                    var unused = new Uri(text);
                }
                catch (Exception ex)
                {
                    return;
                }

                if (text.Contains("###"))
                {
                    for (int i = 1; i < 100; i++)
                    {
                        var url = text.Replace("###", i.ToString());
                        var oldItemCount = _items.Count;
                        await _ProcessPageAsync(url);

                        if (_items.Count == oldItemCount)
                            break;
                    }
                }
                else
                {
                    await _ProcessPageAsync(text);
                }
            }


        }

        private async void _Item_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var item = (sender as FrameworkElement).DataContext as MainPageItem;
            var json = JsonConvert.SerializeObject(item);
            var dlg = new MessageDialog(json);
            await dlg.ShowAsync();
        }
    }

    public class MainPageItem : INotifyPropertyChanged
    {
        public string Url { get; set; }

        #region BitmapImage FileBmSource PropertyChanged
        BitmapImage _FileBmSource;
        public BitmapImage FileBmSource
        {
            get
            {
                return _FileBmSource;
            }
            set
            {
                if (_FileBmSource != value)
                {
                    _FileBmSource = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(STR_FileBmSource));
                    }
                }
            }
        }
        public const string STR_FileBmSource = "FileBmSource";
        #endregion

        public int FileSize { get; set; }

        #region int Progress PropertyChanged
        int _Progress;
        public int Progress
        {
            get
            {
                return _Progress;
            }
            set
            {
                if (_Progress != value)
                {
                    _Progress = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Progress"));
                    }
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class MockMainPageItems : List<MainPageItem>
    {
        public MockMainPageItems()
        {
            if (!DesignMode.DesignModeEnabled)
                return;

            var rand = new Random(new object().GetHashCode());

            for (int i = 0; i < 100; i++)
            {
                this.Add(new MainPageItem()
                {
                    Url = Guid.NewGuid().ToString() + Guid.NewGuid().ToString() + Guid.NewGuid().ToString(),
                    Progress = rand.Next(0, 100),
                    FileSize = 100,
                });
            }
        }
    }
}
