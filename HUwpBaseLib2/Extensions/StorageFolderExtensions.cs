using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace HUwpBaseLib.Extensions
{
    public static class StorageFolderExtensions
    {
        public static async Task<bool> Hubl_ExistsAsync(this StorageFolder thisObj, string fileName)
        {
            var result = false;
            var lstfiles = await thisObj.GetFilesAsync(CommonFileQuery.OrderByName);
            var foundfile = lstfiles.FirstOrDefault(x => x.Name == fileName);
            result = foundfile == null;
            return result;
        }
    }
}
