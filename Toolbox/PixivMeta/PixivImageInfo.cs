using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace Toolbox.PixivMeta
{

    class PixivImageInfo : INotifyPropertyChanged
    {

        private static HttpClient HttpClient;
        
        static PixivImageInfo()
        {
            new HttpClientHandler().ServerCertificateCustomValidationCallback += (_, _, _, _) => true;
            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-cn");
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.114 Safari/537.36 Edg/91.0.864.59");
        }

        public FileInfo file;
        private string newName;
        private bool? state = null;
        private string remark;

        #region Properties
        public string FullName { get; set; }//文件位置
        public string Name { get; set; }//文件名
        public string NewName { get => newName; set { newName = value; OnPropertyChanged("NewName"); } }//新文件名
        public string NewFullName { set; get; }
        public string Artist { get; set; }//画师名
        public string Uid { get; set; }//画师ID
        public string Title { get; set; }//标题
        public string Pid { get; set; }//作品ID
        public string Page { get; set; }//图片序号，从0开始计数
        public string CreateDate { get; set; }//作品创建日期
        public string Type { get; set; }//作品类型
        public string Description { get; set; }//作品介绍
        public int PageCount { get; set; }//图片数量
        public int BookmarkCount { get; set; }//收藏
        public int LikeCount { get; set; }//点赞
        public int CommentCount { get; set; }//评论
        public int ResponseCount { get; set; }//回复
        public int ViewCount { get; set; }//浏览量
        public int Rating { get; set; }//评级
        public ReadOnlyCollection<string> Tags { get; set; }//标签列表
        public string Remark { get => remark; set { remark = value; OnPropertyChanged("Remark"); } }//备注
        public bool? State { get => state; set { state = value; OnPropertyChanged("State"); } }
        public string ErrorInfo { set; get; }//错误信息
        public string Log { set; get; }//操作记录

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string PropertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        /// <summary>
        /// 构造函数，读取图片现有的元数据
        /// </summary>
        /// <param name="path">文件完整目录</param>
        public PixivImageInfo(string path)
        {
            file = new FileInfo(path);
            BitmapDecoder decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnDemand);
            BitmapMetadata meta = (BitmapMetadata)decoder.Frames[0].Metadata;
            FullName = file.FullName;
            Name = file.Name;//包含扩展名
            if (file.Extension == ".jpg")
            {
                Artist = meta.Author?[0];
                Title = meta.Title;
                CreateDate = meta.DateTaken;
                Description = meta.Comment;
                Tags = meta.Keywords;
                Rating = meta.Rating;
            }
            Pid = Regex.Match(Name, "([0-9]+?)_p").Groups[1].Value;
            Page = Regex.Match(Name, "_p([0-9]+?)").Groups[1].Value;
            if (Pid == "")
            {
                State = false;
                Remark = "找不到Pid";
            }
            if (Page == "")
            {
                Page = "0";
            }
        }

        /// <summary>
        /// 从文件夹获取匹配图片文件（png/jpg）
        /// </summary>
        /// <param name="folder">文件夹目录</param>
        /// <returns>匹配的文件完整目录的列表</returns>
        public static List<string> GetMatchFiles(string folder)
        {

            DirectoryInfo folderInfo = new DirectoryInfo(folder);//获取文件夹信息
            List<FileInfo> fileInfos = new List<FileInfo>(folderInfo.GetFiles());//获取文件信息列表
            for (int i = 0; i < fileInfos.Count; i++)
            {
                FileInfo fileInfo = fileInfos[i];
                if (!(fileInfo.Extension == ".jpg"
                  || fileInfo.Extension == ".jpeg"
                  || fileInfo.Extension == ".png"))
                {
                    fileInfos.Remove(fileInfo);//不匹配则移除
                    i--;
                }
            }
            List<string> files = new List<string>();
            fileInfos.ForEach(x => files.Add(x.FullName));
            return files;
        }

        /// <summary>
        /// 更改图片元数据，内有ChangeInfo和UpdateMeta
        /// </summary>
        public void ChangeInfo()
        {
            try
            {
                string html = GetHtmlFromWeb();
                UpdateMeta(html);
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 从网页获取html（好像不应该叫html）
        /// </summary>
        /// <returns>未解析的html</returns>
        string GetHtmlFromWeb()
        {
            string url = "https://www.pixiv.net/artworks/" + Pid;
            string html;
            try
            {
                html = HttpClient.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                Remark = "网络异常";
                State = false;
                throw;
            }
            return html;
        }

        /// <summary>
        /// 根据html更新图片的信息
        /// </summary>
        /// <param name="html">未解析的html</param>
        void UpdateMeta(string html)
        {
            try
            {
                Title = GetTitle(html);
                Artist = GetArtist(html);
                Uid = GetUid(html);
                CreateDate = GetCreateDate(html);
                Type = GetType(html);
                Description = GetDescription(html);
                Tags = GetTags(html).AsReadOnly();
                html = html.Substring(html.IndexOf("\"likeData\""));//避免其他作品信息的影响
                PageCount = GetPageCount(html);
                BookmarkCount = GetBookmarkCount(html);
                LikeCount = GetLikeCount(html);
                CommentCount = GetCommentCount(html);
                ResponseCount = GetResponseCount(html);
                ViewCount = GetViewCount(html);
                //TODO Rating=SetRating();
                NewName = $"[{Artist}] {Title} [{Pid}_p{Page}].jpg";
            }
            catch (System.Exception)
            {
                Remark = "获取作品信息错误";
                State = false;
                throw;
            }
        }

        /// <summary>
        /// 保存图片并写入元数据
        /// </summary>
        /// <param name="saveFolder">保存目录</param>
        public void SaveFile(string saveFolder)
        {
            MemoryStream ms;//创建记忆流

            try
            {
                FileStream openStream = new FileStream(file.FullName, FileMode.Open);//创建文件流
                byte[] vs = new byte[openStream.Length];//创建临时数组
                openStream.Read(vs, 0, vs.Length);//写入数组
                openStream.Dispose();
                ms = new MemoryStream(vs);//写入记忆流
            }
            catch (Exception)//读取异常
            {
                Remark = "读取文件异常";
                State = false;
                throw;
            }

            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.Default);//解码图像
            BitmapMetadata metadata = ToMetadata();//返回作品元数据
            BitmapFrame frame = decoder.Frames[0];//抽取图像帧
            JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = 100 };//编码质量
            encoder.Frames.Add(BitmapFrame.Create(frame, frame.Thumbnail, metadata, frame.ColorContexts));//添加编码图像
            NewFullName = saveFolder + "\\" + NewName;//新文件路径

            try
            {
                FileStream createStream = new FileStream(NewFullName, FileMode.Create);//创建新文件流
                encoder.Save(createStream);//保存新文件
                createStream.Dispose();
                if (file.Extension == ".png")
                {
                    Directory.CreateDirectory(saveFolder + "\\png");//创建"\png"文件夹
                    file.CopyTo(saveFolder + "\\png\\" + file.Name, true);//png文件复制到"\png"下
                }
                if (NewName != Name)
                {
                    file.Delete();//删除原文件
                }
            }
            catch (Exception)//写入异常
            {
                State = false;
                Remark = "写入文件异常";
                throw;
            }
        }

        // 从html中查找图片信息
        #region HtmlOperations 

        static List<string> GetTags(string source)
        {
            List<string> Tags = new List<string>();
            int start = source.IndexOf("\"tags\":");
            int end = source.IndexOf("}],", start);
            source = source[start..end];
            MatchCollection matches = Regex.Matches(source, "((\"tag\":\")|({\"en\":\"))(?<tags>[^\"]+)\"");
            foreach (Match match in matches)
            {
                GroupCollection groups = match.Groups;
                Tags.Add(WebUtility.HtmlDecode(groups["tags"].Value).Replace("\\u0027", "'"));// \u0027 变为 '
            }
            return Tags;
        }

        static string GetCreateDate(string source)
        {
            int start = source.IndexOf("\"createDate\":\"") + 14;
            int end = source.IndexOf("\",", start);
            string Date = source[start..end];
            return Date;
        }

        static string GetType(string source)
        {
            int start = source.IndexOf("\"illustType\":\"") + 14;
            int end = source.IndexOf("\",", start);
            string Type = source[start..end];
            Type.Replace("0", "illustration");//插画
            Type.Replace("1", "manga");//漫画
            Type.Replace("2", "ugoira");//动图
            return Type;
        }

        static string GetPid(string source)//好像没啥用
        {
            int start = source.IndexOf("\"illustId\":\"") + 12;
            int end = source.IndexOf("\",", start);
            string Pid = source[start..end];
            return Pid;
        }
        static string GetArtist(string source)
        {
            int start = source.IndexOf("\"userName\":\"") + 12;
            int end = source.IndexOf("\"}", start);
            string User = source[start..end];
            User = AvoidNameError(User);//避免Windows文件名错误
            User = Regex.Replace(User, "@.*", "");
            User = Regex.Replace(User, "＠.*", "");
            User = Regex.Replace(User, "個展.*", "");
            // User = Regex.Replace(User, "しろすず.*", "しろすず");
            // User = Regex.Replace(User, "碧風羽.*", "碧風羽");
            // User = Regex.Replace(User, "Sila.*", "");
            User = Regex.Replace(User, "(仕事募集中)", "");
            User = Regex.Replace(User, "(お仕事募集中)", "");
            User = Regex.Replace(User, " ／ 仕事募集中", "");
            User = Regex.Replace(User, "お仕事募集中", "");
            return User;
        }

        static string GetTitle(string source)
        {
            int start = source.IndexOf("\"illustTitle\":\"") + 15;
            int end = source.IndexOf("\",", start);
            string Title = source[start..end];
            Title = AvoidNameError(Title);//避免Windows文件名错误
            return Title;
        }

        static string AvoidNameError(string name)//避免Windows文件名错误
        {
            name = WebUtility.HtmlDecode(name);
            name = name.Replace("\\u0027", "'");
            name = name.Replace("\\\"", "＂");
            name = name.Replace(":", "꞉");
            name = name.Replace("?", "？");
            name = name.Replace("*", "＊");
            name = name.Replace("|", "︱");
            name = name.Replace("<", "﹤");
            name = name.Replace(">", "﹥");
            name = name.Replace("/", "／");
            name = name.Replace("\\", "／");
            return name;
        }
        static string GetDescription(string source)
        {
            int start = source.IndexOf("\"illustComment\":\"") + 17;
            int end = source.IndexOf("\",\"", start);
            string Intro = source[start..end];
            Intro = HtmlToText(Intro);
            return Intro;
        }

        static string GetUid(string source)
        {
            int start = source.IndexOf("\"userId\":\"") + 10;
            int end = source.IndexOf("\",", start);
            string Uid = source[start..end];
            return Uid;
        }

        static int GetPageCount(string source)
        {
            int start = source.IndexOf("\"pageCount\":") + 12;
            int end = source.IndexOf(",", start);
            int PageCount = Convert.ToInt32(source[start..end]);
            return PageCount;
        }

        static int GetBookmarkCount(string source)
        {
            int start = source.IndexOf("\"bookmarkCount\":") + 16;
            int end = source.IndexOf(",", start);
            int BookmarkCount = Convert.ToInt32(source[start..end]);
            return BookmarkCount;
        }

        static int GetLikeCount(string source)
        {
            int start = source.IndexOf("\"likeCount\":") + 12;
            int end = source.IndexOf(",", start);
            int LikeCount = Convert.ToInt32(source[start..end]);
            return LikeCount;
        }

        static int GetCommentCount(string source)
        {
            int start = source.IndexOf("\"commentCount\":") + 15;
            int end = source.IndexOf(",", start);
            int CommentCount = Convert.ToInt32(source[start..end]);
            return CommentCount;
        }

        static int GetResponseCount(string source)
        {
            int start = source.IndexOf("\"responseCount\":") + 16;
            int end = source.IndexOf(",", start);
            int ResponseCount = Convert.ToInt32(source[start..end]);
            return ResponseCount;
        }
        static int GetViewCount(string source)
        {
            int start = source.IndexOf("\"viewCount\":") + 12;
            int end = source.IndexOf(",", start);
            int ViewCount = Convert.ToInt32(source[start..end]);
            return ViewCount;
        }
        static string HtmlToText(string source)  //! 仅对作品介绍进行适配
        {
            source = WebUtility.UrlDecode(source);
            source = WebUtility.HtmlDecode(source);
            source = Regex.Replace(source, @"<( )*br( )*>", "\n", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"<( )*li( )*>", "\n", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"<( )*br( )*/>", "\n", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"<( )*li( )*/>", "\n", RegexOptions.IgnoreCase);
            source = Regex.Replace(source, @"<[^>]*>", string.Empty, RegexOptions.IgnoreCase);
            return source;
        }

        #endregion

        public BitmapMetadata ToMetadata()
        {
            BitmapMetadata metadata = new BitmapMetadata("jpg")
            {
                Author = new ReadOnlyCollection<string>(new List<string>() { Artist }),
                Title = Title,
                Comment = Description,
                DateTaken = CreateDate,
                Keywords = Tags
            };
            //metadata.Rating = SetRating();
            return metadata;
        }

        /*
        int SetRating()//TODO 根据浏览点赞收藏数设定分级
        {
            int rating;
            int index = ViewCount + LikeCount + BookmarkCount + CommentCount + ResponseCount;//综合指标
            if (index >= 0)//5级条件
                rating = 5;
            else if (index >= 0)//4级条件
                rating = 4;
            else if (index >= 0)//3级条件
                rating = 3;
            else if (index >= 0)//2级条件
                rating = 2;
            else if (index >= 0)//1级条件
                rating = 1;
            else
                rating = 0;//不分级
            return rating;
        }
        */


    }
}
