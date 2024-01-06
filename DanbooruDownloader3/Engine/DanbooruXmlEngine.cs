﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DanbooruDownloader3.DAO;
using DanbooruDownloader3.Entity;

namespace DanbooruDownloader3.Engine
{
    /// <summary>
    /// TODO: not used yet
    /// </summary>

    public class DanbooruXmlEngine : IEngine
    {
        public int? TotalPost { get; set; }

        public int? Offset { get; set; }

        public string RawData { get; set; }

        public string ResponseMessage { get; set; }

        public bool Success { get; set; }

        public BindingList<DanbooruPost> Parse(string data, DanbooruSearchParam query, ref string errorMessage)
        {
            BindingList<DanbooruPost> list = new BindingList<DanbooruPost>();
            XDocument posts = XDocument.Parse(data);
            this.RawData = posts.ToString();

            Success = true;
            var responses = posts.Descendants("response");
            if (responses != null && responses.Count() > 0)
            {
                ResponseMessage = responses.First().Attribute("reason").Value.ToString();
                Success = Convert.ToBoolean(responses.First().Attribute("success").Value);
                if (!Success)
                {
                    return null;
                }
            }

            this.TotalPost = Convert.ToInt32(posts.Root.Attribute("count").Value);
            this.Offset = Convert.ToInt32(posts.Root.Attribute("offset").Value);

            string queryStr = GenerateQueryString(query);

            foreach (var post in posts.Descendants("post"))
            {
                DanbooruPost p = new DanbooruPost();
                p.Id = post.Attribute("id").Value.ToString();
                p.Tags = post.Attribute("tags").Value.ToString();
                p.TagsEntity = Helper.ParseTags(p.Tags, SearchParam.Provider);
                p.Source = post.Attribute("source").Value.ToString();
                p.Score = post.Attribute("score").Value.ToString();
                p.Rating = post.Attribute("rating").Value.ToString();

                p.FileUrl = AppendHttp(post.Attribute("file_url").Value.ToString(), query.Provider);
                p.Width = Convert.ToInt32(post.Attribute("width").Value);
                p.Height = Convert.ToInt32(post.Attribute("height").Value);

                p.PreviewUrl = AppendHttp(post.Attribute("preview_url").Value.ToString(), query.Provider);
                if (post.Attribute("actual_preview_width") != null &&           // moebooru extension
                    post.Attribute("actual_preview_height") != null)
                {
                    p.PreviewWidth = Convert.ToInt32(post.Attribute("actual_preview_width").Value);
                    p.PreviewHeight = Convert.ToInt32(post.Attribute("actual_preview_height").Value);
                }
                else
                {
                    p.PreviewWidth = Convert.ToInt32(post.Attribute("preview_width").Value);
                    p.PreviewHeight = Convert.ToInt32(post.Attribute("preview_height").Value);
                }

                p.SampleUrl = AppendHttp(post.Attribute("sample_url").Value.ToString(), query.Provider);
                p.SampleWidth = Convert.ToInt32(post.Attribute("sample_width").Value);
                p.SampleHeight = Convert.ToInt32(post.Attribute("sample_height").Value);

                // moebooru extension
                p.JpegUrl = AppendHttp(post.Attribute("jpeg_url").Value.ToString(), query.Provider);
                p.JpegWidth = Convert.ToInt32(post.Attribute("jpeg_width").Value);
                p.JpegHeight = Convert.ToInt32(post.Attribute("jpeg_height").Value);

                p.Filesize = Convert.ToInt32(post.Attribute("file_size").Value);
                p.Status = post.Attribute("status").Value.ToString();
                p.HasChildren = Convert.ToBoolean(post.Attribute("has_children").Value);
                p.ParentId = post.Attribute("parent_id").Value.ToString();
                p.Change = post.Attribute("change").Value.ToString();
                p.CreatorId = post.Attribute("creator_id").Value.ToString();
                p.CreatedAt = post.Attribute("created_at").Value.ToString();
                p.MD5 = post.Attribute("md5").Value.ToString();

                p.Provider = query.Provider;
                p.Query = queryStr;
                p.SearchTags = query.Tag;
                p.Referer = query.Provider.Url + @"/post/show/" + p.Id;

                list.Add(p);
            }
            return list;
        }

        public string GenerateQueryString(DanbooruSearchParam query)
        {
            string tmp = "";

            if (!String.IsNullOrWhiteSpace(query.Tag))
            {
                tmp += System.Web.HttpUtility.UrlEncode(query.Tag);
            }
            if (!String.IsNullOrWhiteSpace(query.Source))
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    tmp += "+";
                }
                tmp += "source:" + query.Source;
            }
            if (!String.IsNullOrWhiteSpace(query.OrderBy))
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    tmp += "+";
                }
                tmp += "order:" + query.OrderBy;
            }
            if (!String.IsNullOrWhiteSpace(query.Rating))
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    tmp += "+";
                }
                tmp += "rating:" + query.Rating;
            }
            if (!string.IsNullOrWhiteSpace(tmp))
            {
                tmp = "tags=" + tmp;
            }

            // page
            if (query.Page.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    tmp += "&";
                }
                tmp += "page=" + query.Page.Value.ToString();
            }

            // limit
            if (query.Limit.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(tmp))
                {
                    tmp += "&";
                }
                tmp += "limit=" + query.Limit.Value.ToString();
            }
            return tmp;
        }

        private string AppendHttp(string url, DanbooruProvider provider)
        {
            if (String.IsNullOrWhiteSpace(url)) return url;
            if (!url.StartsWith("http"))
            {
                return provider.Url + url;
            }
            return url;
        }

        public DanbooruSearchParam SearchParam
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

        public int GetNextPage()
        {
            throw new NotImplementedException();
        }

        public int GetPrevPage()
        {
            throw new NotImplementedException();
        }

        public static string GetQueryString(DanbooruProvider provider, DanbooruSearchParam query)
        {
            var queryStr = "";

            // Clean up txtTags
            var tags = query.Tag;
            while (tags.Contains("  "))
            {
                tags = tags.Replace("  ", " ");
            }
            tags = tags.Trim();
            tags = System.Web.HttpUtility.UrlEncode(tags);

            List<string> queryList = new List<string>();
            List<string> tagsList = new List<string>();

            //Tags
            if (tags.Length > 0) tagsList.Add(tags.Replace(' ', '+'));

            //Rating
            if (!String.IsNullOrWhiteSpace(query.Rating)) tagsList.Add(query.IsNotRating ? "-" + query.Rating : "" + query.Rating);

            //Source
            if (!String.IsNullOrWhiteSpace(query.Source)) tagsList.Add("source:" + query.Source);

            //Order
            if (!String.IsNullOrWhiteSpace(query.OrderBy)) tagsList.Add(query.OrderBy);

            if (tagsList.Count > 0) queryList.Add("tags=" + String.Join("+", tagsList));

            //Limit
            if (query.Limit > 0) queryList.Add("limit=" + query.Limit);

            //StartPage
            if (query.Page > 0)
            {
                if (provider.BoardType == BoardType.Danbooru) queryList.Add("page=" + query.Page);
                else if (provider.BoardType == BoardType.Gelbooru) queryList.Add("pid=" + query.Page);
            }

            if (queryList.Count > 0) queryStr = String.Join("&", queryList);

            return queryStr;
        }
    }
}