using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Media.Imaging;

namespace HImgDownloader.Models
{
    public class ImgItem : INotifyPropertyChanged
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

        #region int FileSize PropertyChanged
        int _FileSize;
        public int FileSize
        {
            get
            {
                return _FileSize;
            }
            set
            {
                if (_FileSize != value)
                {
                    _FileSize = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(STR_FileSize));
                    }
                }
            }
        }
        public const string STR_FileSize = "FileSize";
        #endregion

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

    public class MockImgItems : List<ImgItem>
    {
        public MockImgItems()
        {
            if (!DesignMode.DesignModeEnabled)
                return;

            var rand = new Random(new object().GetHashCode());

            for (int i = 0; i < 100; i++)
            {
                this.Add(new ImgItem()
                {
                    Url = Guid.NewGuid().ToString() + Guid.NewGuid().ToString() + Guid.NewGuid().ToString(),
                    Progress = rand.Next(0, 100),
                    FileSize = 100,
                });
            }
        }
    }
}
