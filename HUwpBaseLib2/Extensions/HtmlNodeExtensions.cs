using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HUwpBaseLib.Extensions
{
    public static class HtmlNodeExtensions
    {
        public static void HUBL_SelectNodes(this HtmlNode thisObj, string name, ref List<HtmlNode> resultItems)
        {
            foreach (var node in thisObj.ChildNodes)
            {
                if (node.Name.ToLower() == name.ToLower())
                {
                    resultItems.Add(node);
                }

                HUBL_SelectNodes(node, name, ref resultItems);
            }
        }
    }
}
