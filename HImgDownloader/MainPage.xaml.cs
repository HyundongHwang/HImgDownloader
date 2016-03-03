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
using HImgDownloader.Models;
using HUwpBaseLib.Logs;
using Windows.System.Profile;
using Windows.Security.ExchangeActiveSyncProvisioning;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;
using Windows.UI.ViewManagement;

// 빈 페이지 항목 템플릿은 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 에 문서화되어 있습니다.

namespace HImgDownloader
{
    public class Contact
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Mobile { get; set; }
    }




    /// <summary>
    /// 자체에서 사용하거나 프레임 내에서 탐색할 수 있는 빈 페이지입니다.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private ObservableCollection<ImgItem> _items = new ObservableCollection<ImgItem>();
        private string _oldText;
        private List<Task> _tasks = new List<Task>();

        public MainPage()
        {
            var path = ApplicationData.Current.LocalFolder.Path;
            this.InitializeComponent();
            this.LbObj.ItemsSource = _items;
            this.BtnClearAll.Click += BtnClearAll_Click;
            this.BtnRemoveDownloaded.Click += BtnRemoveDownloaded_Click;
        }

        private async void BtnRemoveDownloaded_Click(object sender, RoutedEventArgs e)
        {
            Log.d("BtnRemoveDownloaded_Click");
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            Log.d("BtnClearAll_Click");
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

        private void _ScrapImgUrls(string htmlStr, string tagName, string propName, ref List<string> imgUrlList)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(htmlStr);



            var imgNodes = new List<HtmlNode>();
            doc.DocumentNode.HUBL_SelectNodes(tagName, ref imgNodes);

            foreach (var imgNode in imgNodes)
            {
                var srcValue = imgNode.GetAttributeValue(propName, null);

                if (string.IsNullOrEmpty(srcValue))
                    continue;



                var imgUrl = "";

                if (srcValue.StartsWith("http"))
                {
                    imgUrl = srcValue;
                }
                else
                {
                    imgUrl = HublUtils.CombineUrl(imgUrl, srcValue);
                }

                if (string.IsNullOrEmpty(imgUrl))
                    continue;



                imgUrlList.Add(imgUrl);
            }
        }

        private async Task _ProcessPageAsync(string url)
        {
            Log.d($"_ProcessPageAsync url : {url}");
            var htmlStr = "";

            try
            {
                htmlStr = await HublUtils.HttpGetStringAsync(url);
            }
            catch (Exception ex)
            {
                Log.exception(ex);
            }

            if (string.IsNullOrWhiteSpace(htmlStr))
                return;

            var imgUrlList = new List<string>();
            _ScrapImgUrls(htmlStr, "img", "src", ref imgUrlList);
            _ScrapImgUrls(htmlStr, "a", "href", ref imgUrlList);

            foreach (var imgUrl in imgUrlList)
            {
                var ext = HublUtils.GetExt(imgUrl);

                if (ext != ".png" &&
                    ext != ".jpg" &&
                    ext != ".jpeg")
                    continue;



                Log.d("srcUrl :" + imgUrl);
                var task = _DownloadImgAsync(imgUrl);
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
                var item = new ImgItem();
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
                Log.exception(ex);
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
                Log.d($"text : {text}");

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
            var item = (sender as FrameworkElement).DataContext as ImgItem;
            var json = JsonConvert.SerializeObject(item);
            var dlg = new MessageDialog(json);
            await dlg.ShowAsync();
        }
    }
}
