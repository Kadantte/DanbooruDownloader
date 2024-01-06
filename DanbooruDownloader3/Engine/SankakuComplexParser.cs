﻿using DanbooruDownloader3.DAO;
using DanbooruDownloader3.Entity;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace DanbooruDownloader3.Engine
{
    public class SankakuComplexParser : IEngine
    {

        private static Regex _postCount = new Regex("Posts: (.*)Books");

        private static bool isHttps(DanbooruProvider provider)
        {
            return provider.Url.ToLowerInvariant().StartsWith("https");
        }

        /// <summary>
        /// Parse the post details after added to the download list or from batch job.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="postHtml"></param>
        /// <param name="overideTagParsing"></param>
        /// <returns></returns>
        public static DanbooruPost ParsePost(DanbooruPost post, string postHtml, bool overideTagParsing)
        {
            try
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(postHtml);
                string file_url = "";
                string sample_url = "";

                // reparse tags with type
                if (overideTagParsing)
                {
                    ReparseTags(post, doc);
                }

                // Flash Game or bmp
                if (post.PreviewUrl != null && post.PreviewUrl.EndsWith("download-preview.png"))
                {
                    var links = doc.DocumentNode.SelectNodes("//a");
                    foreach (var link in links)
                    {
                        // flash
                        if (link.InnerText == "Save this file (right click and save as)")
                        {
                            file_url = Helper.FixUrl(link.GetAttributeValue("href", ""), isHttps(post.Provider));
                            // http://cs.sankakucomplex.com/data/f6/23/f623ea7559ef39d96ebb0ca7530586b8.swf
                            post.MD5 = file_url.Substring(file_url.LastIndexOf("/") + 1);
                            post.MD5 = post.MD5.Substring(0, 32);

                            break;
                        }
                        // bmp
                        if (link.InnerText == "Download")
                        {
                            file_url = Helper.FixUrl(link.GetAttributeValue("href", ""), isHttps(post.Provider));
                            break;
                        }
                    }
                }
                else
                {
                    var lowresElement = doc.DocumentNode.SelectSingleNode("//a[@id='lowres']");
                    if (lowresElement != null)
                    {
                        sample_url = Helper.FixUrl(lowresElement.GetAttributeValue("href", ""), isHttps(post.Provider));
                    }
                    var highresElement = doc.DocumentNode.SelectSingleNode("//a[@id='highres']");
                    if (highresElement != null)
                    {
                        file_url = Helper.FixUrl(highresElement.GetAttributeValue("href", ""), isHttps(post.Provider));
                    }
                }

                post.FileUrl = file_url;
                if (!string.IsNullOrWhiteSpace(file_url) && string.IsNullOrWhiteSpace(sample_url))
                    sample_url = file_url;
                post.SampleUrl = sample_url;

                // Created datetime
                post.CreatedAt = "N/A";
                post.CreatedAtDateTime = DateTime.MinValue;
                try
                {
                    // Issue #277
                    var lis = doc.DocumentNode.SelectNodes("//div[@id='post-view']//div[@id='stats']//a");
                    foreach (var item in lis)
                    {

                        if (item.Attributes.Contains("href") && item.Attributes["href"].Value.Contains("?tags=date"))
                        {
                            post.CreatedAt = item.Attributes["title"].Value;
                            post.CreatedAtDateTime = DanbooruPostDao.ParseDateTime(post.CreatedAt, post.Provider);
                            break;
                        }

                    }
                }
                catch (Exception ex)
                {
                    Program.Logger.Error("Unable to parse date", ex);
                }

                return post;
            }
            catch (Exception ex)
            {
                string filename = "Dump for Post " + post.Id + post.Provider.Name + " Query " + post.Query + ".txt";
                bool result = Helper.DumpRawData(postHtml, filename);
                if (!result) Program.Logger.Error("Failed to dump rawdata to: " + filename, ex);
                throw;
            }
        }

        /// <summary>
        /// Reparse tags from post details.
        /// </summary>
        /// <param name="post"></param>
        /// <param name="doc"></param>
        private static void ReparseTags(DanbooruPost post, HtmlDocument doc)
        {
            post.TagsEntity.Clear();
            var tags = doc.DocumentNode.SelectNodes("//ul[@id='tag-sidebar']/li");
            foreach (var tag in tags)
            {
                var tagEntity = new DanbooruTag();
                var cls = tag.Attributes["class"].Value;    // Fix Issue #146
                switch (cls)
                {
                    case "tag-type-idol":
                        // idol complex
                        tagEntity.Type = DanbooruTagType.Artist;
                        break;

                    case "tag-type-artist":
                        // sankaku
                        tagEntity.Type = DanbooruTagType.Artist;
                        break;

                    case "tag-type-photo_set":
                        // idol complex: usually album name
                        tagEntity.Type = DanbooruTagType.Circle;
                        break;

                    case "tag-type-studio":
                        // sankaku: circlename
                        tagEntity.Type = DanbooruTagType.Circle;
                        break;

                    case "tag-type-meta":
                        // both
                        tagEntity.Type = DanbooruTagType.Faults;
                        break;

                    case "tag-type-medium":
                        // both
                        tagEntity.Type = DanbooruTagType.Faults;
                        break;

                    case "tag-type-general":
                        // both
                        tagEntity.Type = DanbooruTagType.General;
                        break;

                    case "tag-type-copyright":
                        // both
                        tagEntity.Type = DanbooruTagType.Copyright;
                        break;

                    case "tag-type-character":
                        // both
                        tagEntity.Type = DanbooruTagType.Character;
                        break;

                    default:
                        tagEntity.Type = DanbooruTagType.Unknown;
                        break;
                }
                tagEntity.Name = Helper.DecodeEncodedNonAsciiCharacters(tag.InnerText.Trim());
                if (String.IsNullOrWhiteSpace(tagEntity.Name))
                {

                    tagEntity.Name = Helper.DecodeEncodedNonAsciiCharacters(tag.SelectSingleNode(".//a").InnerText);
                }

                // no more tags count 20240106
                //// Fix Issue #268
                //var match = _postCount.Match(tag.InnerText.Trim());
                //var countStr = "0";
                //if (match.Success)
                //{
                //    countStr = match.Groups[1].Value;
                //}
                //var modifier = 1;
                //if (countStr.EndsWith("K"))
                //{
                //    modifier = 1000;
                //    countStr = countStr.Replace("K", "");
                //}
                //else if (countStr.EndsWith("M"))
                //{
                //    modifier = 1000000;
                //    countStr = countStr.Replace("M", "");
                //}
                //double.TryParse(countStr, out double count);
                //tagEntity.Count = (int)(count * modifier);

                post.TagsEntity.Add(tagEntity);
                tag.Remove();
            }
            post.TagsEntity = post.TagsEntity.OrderByDescending(x => x.Type).ThenBy(x => x.Name).ToList();
        }

        /// <summary>
        /// Parse search page result and return the images with initial tags.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="searchParam"></param>
        /// <returns></returns>
        public BindingList<DanbooruPost> Parse(string data, DanbooruSearchParam searchParam, ref string errorMessage)
        {
            try
            {
                this.SearchParam = searchParam;
                this.RawData = data;

                BindingList<DanbooruPost> posts = new BindingList<DanbooruPost>();

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(data);

                // get error message
                // post-premium-browsing_error
                var sankakuPremiumError = doc.DocumentNode.SelectSingleNode("//div[@class='post-premium-browsing_error']");
                if (sankakuPremiumError != null)
                {
                    errorMessage = $"Sankaku Premium Error: {sankakuPremiumError.InnerText.Trim()}";
                    Program.Logger.Error(errorMessage);
                    return posts;
                }

                // remove popular preview and images in mail notice
                var nodeIds = new string[] { "popular-preview", "has-mail-notice" };
                foreach (var nodeId in nodeIds)
                {
                    var node = doc.DocumentNode.SelectSingleNode($"//div[@id='{nodeId}']");
                    if (node != null)
                    {
                        node.Remove();
                    }
                }

                // get all thumbs
                // var thumbs = doc.DocumentNode.SelectNodes("//span");
                var thumbs = doc.DocumentNode.SelectNodes("//span[contains(@class, 'thumb')]");
                if (thumbs != null && thumbs.Count > 0)
                {
                    foreach (var thumb in thumbs)
                    {
                        //if (thumb.GetAttributeValue("class", "").Contains("thumb"))
                        //{
                        DanbooruPost post = new DanbooruPost();
                        post.Id = thumb.GetAttributeValue("id", "-1").Substring(1);

                        post.Provider = searchParam.Provider;
                        post.SearchTags = searchParam.Tag;
                        post.Query = GenerateQueryString(searchParam);

                        // get the link to post
                        HtmlNode a = null;
                        foreach (var node in thumb.ChildNodes)
                        {
                            if (node.Name == "a")
                            {
                                a = node;
                                break;
                            }
                        }
                        if (a == null)
                        {
                            Program.Logger.Warn(String.Format($"Cannot get post link for {post.Id}."));
                            continue;
                        }
                        post.Referer = Helper.FixUrl(searchParam.Provider.Url + a.GetAttributeValue("href", ""), isHttps(post.Provider));

                        // get thumbnail
                        HtmlNode img = null;
                        foreach (var node in a.ChildNodes)
                        {
                            if (node.Name == "img")
                            {
                                img = node;
                                break;
                            }
                        }
                        if (img == null)
                        {
                            Program.Logger.Warn(String.Format($"Cannot get image thumbs for {post.Id}."));
                        }
                        else
                        {
                            if (img.GetAttributeValue("src", "").Contains("images/no-visibility.svg"))
                            {
                                Program.Logger.Warn(String.Format($"No access for post {post.Id}."));
                                continue;
                            }
                            var title = img.GetAttributeValue("data-auto_page", "");
                            post.Tags = title.Substring(0, title.LastIndexOf("Rating:")).Trim();
                            post.Tags = Helper.DecodeEncodedNonAsciiCharacters(post.Tags);
                            post.TagsEntity = Helper.ParseTags(post.Tags, SearchParam.Provider);

                            post.Hidden = Helper.CheckBlacklistedTag(post, searchParam.Option);

                            var status = img.GetAttributeValue("class", "").Replace("preview", "").Trim();
                            if (status.Contains("deleted"))
                                post.Status = "deleted";
                            else if (status.Contains("pending"))
                                post.Status = "pending";
                            else
                                post.Status = status;

                            post.PreviewUrl = Helper.FixUrl(img.GetAttributeValue("src", ""), isHttps(post.Provider));
                            post.PreviewHeight = img.GetAttributeValue("height", 0);
                            post.PreviewWidth = img.GetAttributeValue("width", 0);

                            // Rating:R18+ Score:5.0 Size:1425x1188 User:kabeshi"
                            post.Source = "";
                            post.Score = title.Substring(title.LastIndexOf("Score:") + 6);
                            post.Score = post.Score.Substring(0, post.Score.IndexOf(" ")).Trim();

                            string resolution = title.Substring(title.LastIndexOf("Size:") + 5);
                            resolution = resolution.Substring(0, resolution.IndexOf(" ")).Trim();
                            string[] resArr = resolution.Split('x');
                            post.Width = Int32.Parse(resArr[0]);
                            post.Height = Int32.Parse(resArr[1]);

                            string rating = title.Substring(title.LastIndexOf("Rating:") + 7, 1);
                            //rating = rating.Substring(0, rating.IndexOf(" ")).Trim();
                            post.Rating = rating.ToLower();

                            post.CreatorId = title.Substring(title.LastIndexOf("User:") + 5);

                            post.MD5 = post.PreviewUrl.Substring(post.PreviewUrl.LastIndexOf("/") + 1);
                            post.MD5 = post.MD5.Substring(0, post.MD5.LastIndexOf("."));
                        }
                        posts.Add(post);
                        //}
                    }
                }

                // idol complex
                var siteTitle = doc.DocumentNode.SelectSingleNode("//*[@id='site-title']");
                if (siteTitle != null)
                {
                    var strTitle = siteTitle.InnerText.Split('\n').First();
                    var strCount = "-1";

                    if (!Regex.IsMatch(strTitle, @".* = \d+") && strTitle.LastIndexOf("(") > 0)
                    {
                        // single tag
                        // Sankaku Channel/ginhaha (1,198)
                        strCount = strTitle.Substring(strTitle.LastIndexOf("("));
                    }
                    else
                    {
                        // compound tag
                        // Sankaku Channel/= = (13,957) + rating:e = 585
                        strCount = strTitle.Split('=').Last().Trim();
                    }
                    strCount = strCount.Replace("(", "");
                    strCount = strCount.Replace(",", "");
                    strCount = strCount.Replace(".", "");
                    strCount = strCount.Replace(")", "");
                    Int32.TryParse(strCount, out int count);
                    TotalPost = count;
                }
                else
                {
                    TotalPost = posts.Count;
                }

                if (!SearchParam.Page.HasValue && SearchParam.Page > 0) SearchParam.Page = 1;
                Offset = TotalPost * SearchParam.Page;

                // get next id for 26th page and current page return full list (20 posts)
                if (searchParam.Page >= 25 && posts.Count == 20)
                {
                    searchParam.NextKey = posts[posts.Count - 1].Id;
                }

                return posts;
            }
            catch (Exception ex)
            {
                var filename = Helper.SanitizeFilename($"Dump for Sankaku Image List - {searchParam.Tag} - page {searchParam.Page}.txt");
                var result = Helper.DumpRawData(data, filename);
                if (!result) Program.Logger.Error($"Failed to dump rawdata to: {filename}", ex);
                throw;
            }
        }

        public int? TotalPost { get; set; }

        public int? Offset { get; set; }

        public string RawData { get; set; }

        public string ResponseMessage
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool Success
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public string GenerateQueryString(DanbooruSearchParam query)
        {
            var parameters = new List<String>();
            var tags = new List<String>();
            // https://chan.sankakucomplex.com/?next=20614511&tags=neocoill&page=26

            // next key
            if (!String.IsNullOrEmpty(query.NextKey))
            {
                parameters.Add("next=" + query.NextKey);
            }

            if (!String.IsNullOrWhiteSpace(query.Tag))
            {
                // convert spaces into '+'
                tags.Add(query.Tag.Replace(' ', '+'));
            }
            if (!String.IsNullOrWhiteSpace(query.Source))
            {
                tags.Add("source:" + query.Source);
            }
            if (!String.IsNullOrWhiteSpace(query.OrderBy))
            {
                tags.Add(query.OrderBy);
            }
            if (!String.IsNullOrWhiteSpace(query.Rating))
            {
                tags.Add(query.IsNotRating ? "-" + query.Rating : query.Rating);
            }
            if (tags.Count > 0)
            {
                parameters.Add("tags=" + String.Join("+", tags));
            }

            // page
            if (query.Page.HasValue && query.Page > 1)
            {
                parameters.Add("page=" + query.Page.Value.ToString());
            }
            else
            {
                parameters.Add("commit=Search");
            }

            return String.Join("&", parameters);
        }

        public DanbooruSearchParam SearchParam { get; set; }

        public int GetNextPage()
        {
            if (!SearchParam.Page.HasValue) SearchParam.Page = 1;

            return SearchParam.Page.Value + 1;
        }

        public int GetPrevPage()
        {
            if (!SearchParam.Page.HasValue) SearchParam.Page = 1;
            var temp = SearchParam.Page.Value - 1;
            if (temp > 0) return temp;
            else return 1;
        }

        public DanbooruTagCollection parseTagsPage(string data, int page)
        {
            DanbooruTagCollection tagCol = new DanbooruTagCollection();
            List<DanbooruTag> tags = new List<DanbooruTag>();
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(data);

            int index = 1 + ((page - 1) * 50);
            // select all tags
            var tables = doc.DocumentNode.SelectNodes("//table[contains(@class,'highlightable')]");
            foreach (var table in tables)
            {
                if (!(table.Attributes["class"].Value == "highlightable"))
                {
                    table.Remove();
                    continue;
                }

                var rows = table.SelectNodes("//table[contains(@class,'highlightable')]//tr");
                int countIndex = 1, nameIndex = 3, typeIndex = 9;
                foreach (var row in rows)
                {
                    //if (row.ChildNodes.Count != 11 && row.ChildNodes.Count != 7) continue;
                    var cols = row.ChildNodes;
                    if (cols[1].Name == "th")
                    {
                        for (int i = 0; i < cols.Count; ++i)
                        {
                            if (cols[i].Name == "th")
                            {
                                if (cols[i].InnerText.Replace("\n", "") == "Posts")
                                {
                                    countIndex = i;
                                    continue;
                                }
                                if (cols[i].InnerText.Replace("\n", "") == "Name")
                                {
                                    nameIndex = i;
                                    continue;
                                }
                                if (cols[i].InnerText.Replace("\n", "") == "Type")
                                {
                                    typeIndex = i;
                                    continue;
                                }
                            }
                        }
                        continue;
                    }
                    if (cols[1].Name != "td") continue;

                    DanbooruTag tag = new DanbooruTag();
                    tag.Id = index.ToString();
                    tag.Count = Int32.Parse(cols[countIndex].InnerText);
                    tag.Name = Helper.RemoveControlCharacters(System.Net.WebUtility.HtmlDecode(cols[nameIndex].ChildNodes[3].InnerText.Replace("\n", "")));

                    string tagType = cols[typeIndex].InnerText.Replace("\n", "");
                    if (tagType.EndsWith("(edit)")) tagType = tagType.Substring(0, tagType.Length - 6);
                    tagType = tagType.ToLowerInvariant();
                    if (tagType == "general")
                        tag.Type = DanbooruTagType.General;
                    else if (tagType == "character")
                        tag.Type = DanbooruTagType.Character;
                    else if (tagType == "artist")
                        tag.Type = DanbooruTagType.Artist;
                    else if (tagType == "copyright")
                        tag.Type = DanbooruTagType.Copyright;
                    else if (tagType == "idol")
                        tag.Type = DanbooruTagType.Artist;
                    else if (tagType == "photo_set")
                        tag.Type = DanbooruTagType.Circle;
                    else
                        tag.Type = DanbooruTagType.Faults;

                    tags.Add(tag);
                    ++index;
                }
            }

            tagCol.Tag = tags.ToArray();
            return tagCol;
        }
    }
}