using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using HtmlAgilityPack;

namespace SessionCheckLib
{
    /// <summary>
    /// チェックを担うクラス
    /// </summary>

    public class Checker
    {

        public FormData _formdata 
        {
            set { this.formdata = value; }
            get { return this.formdata; }
        }

        public FormData[] _formdatas 
        {
            private set { this.formdatas = value; }
            get { return this.formdatas; }
        }

        public String _loginUrl
        {
            set { this.loginUrl = value; }
            get { return this.loginUrl; }
        }
        public String _homeUrl 
        {
            set { this.homeUrl = value; }
            get { return this.homeUrl; }
        }
        public String _getOnlyUrl 
        {
            set { this.getOnlyUrl = value; }
            get { return this.getOnlyUrl; }
        }

        public CheckResult _checkResult
        {
            private set { this.checkResult = value; }
            get { return this.checkResult; }
        }

        private String loginUrl, homeUrl, getOnlyUrl;
        private FormData formdata;
        private FormData[] formdatas;
        private CheckResult checkResult;
        CheckerWebResponse firstResponse;

        // public CheckResult retCheck;
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="checker">コピーコンストラクタ（ライトコピー）</param>
        public Checker(Checker checker)
        {
            //retCheck = new CheckResult();
            loginUrl = checker._loginUrl;
            homeUrl = checker._homeUrl;
            getOnlyUrl = checker._getOnlyUrl;
            formdata = checker._formdata;

            //参照渡し
            formdatas = checker.formdatas;
            checkResult = checker.checkResult;
            firstResponse = checker.firstResponse;
        }
        public Checker()
        {
        }

        /// <summary>
        /// URLからフォームを抽出し、FormDataをリターンするメソッド
        /// </summary>
        /// <param name="postdata">Postするバイトデータ</param>
        /// <returns>取得したフォームデータの配列</returns>
        public void searchForms(String postdata)
        {
            firstResponse = getResponse(loginUrl, postdata, null,null);

            //(2)HtmlDocumentクラスにHTMLをセット
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.OptionAutoCloseOnEnd = false;  //最後に自動で閉じる（？）
            doc.OptionCheckSyntax = false;     //文法チェック。
            doc.OptionFixNestedTags = true;    //閉じタグが欠如している場合の処理
            HtmlNode.ElementsFlags.Remove("form");

            doc.LoadHtml(firstResponse.body);
            /*
            System.Diagnostics.Debug.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
            System.Diagnostics.Debug.Write(firstResponse.body);
            DefaultTraceListener dtl = (DefaultTraceListener)Debug.Listeners["Default"];
            dtl.LogFileName = "./debug.txt";
            */
            Debug.WriteLine(firstResponse.body);
            ArrayList dataList = new ArrayList();
            // FORMの解析

            HtmlAgilityPack.HtmlNodeCollection nodecol = doc.DocumentNode.SelectNodes("//form");
            HtmlAgilityPack.HtmlNodeCollection nodecola = doc.DocumentNode.SelectNodes("//input");
            HtmlAgilityPack.HtmlNodeCollection nodecolas = doc.DocumentNode.SelectNodes("//meta");
            if (nodecol != null)
            {
                foreach (HtmlNode elements in nodecol)
                {
                    FormData formData = new FormData((String)elements.GetAttributeValue("name", ""), (String)elements.GetAttributeValue("action", ""));
                    //System.Console.WriteLine("ACTION:" + elements.GetAttribute("action") + System.Environment.NewLine);
                    recSearchform(elements, ref formData);
                    dataList.Add(formData);
                }
            }
            
            formdatas = (FormData[])dataList.ToArray(typeof(FormData));
        }

        /// <summary>
        /// FormのActionから実URLを推測するメソッド
        /// </summary>
        /// <param name="fromUrl">取得したページのURL</param>
        /// <param name="toUrl">FormのAction</param>
        /// <returns>推測されるURL</returns>
        public String getUrlFromAction(String fromUrl, String toUrl)
        {

            if (toUrl.StartsWith("http://") || toUrl.StartsWith("https://"))
            {
                return toUrl;
            }

            String actionUrl;

            if (toUrl.StartsWith("./") | toUrl.StartsWith("../"))
            {
                actionUrl = fromUrl.Remove(fromUrl.LastIndexOf("/"));
                actionUrl += "/" + toUrl;
                Uri s = new Uri(actionUrl);
                actionUrl = s.AbsoluteUri;
            }
            else if (toUrl.StartsWith("/"))
            {
                Uri s = new Uri(fromUrl);
                actionUrl = fromUrl.Remove(fromUrl.IndexOf(s.AbsolutePath));
                actionUrl += toUrl;
            }
            else
            {
                actionUrl = fromUrl.Remove(fromUrl.LastIndexOf("/"));
                actionUrl += "/" + toUrl;
                Uri s = new Uri(actionUrl);
                actionUrl = s.AbsoluteUri;
            }
            return actionUrl;
        }

        //再帰的にフォームを探す
        private void recSearchform(HtmlNode seed, ref FormData formData)
        {

            foreach (HtmlNode elements in seed.SelectNodes(@".//input"))
            {
                if (elements.Name.ToUpper().Equals("INPUT"))
                {
                    ///Debug
                    System.Console.WriteLine(" name:" + elements.GetAttributeValue("name","") + System.Environment.NewLine);
                    if (!formData.table.ContainsKey(elements.GetAttributeValue("name","")))
                    {
                        if (elements.GetAttributeValue("type","").ToUpper().Equals("IMAGE"))
                        {
                            formData.table[elements.GetAttributeValue("name", "") + ".x"] = new FormInput(elements.GetAttributeValue("type", ""), elements.GetAttributeValue("value", ""));
                            formData.table[elements.GetAttributeValue("name", "") + ".y"] = new FormInput(elements.GetAttributeValue("type", ""), elements.GetAttributeValue("value", ""));
                        }
                        else if (elements.GetAttributeValue("type", "").ToUpper().Equals("SUBMIT"))
                        {
                        }
                        else
                        {
                            formData.table[elements.GetAttributeValue("name", "")] = new FormInput(elements.GetAttributeValue("type", ""), elements.GetAttributeValue("value", ""));
                        }
                    }
                }
               // recSearchform(elements, ref formData);
            }
             
        }

        /// <summary>
        /// 実際にチェックを行うメソッド
        /// </summary>
        public void checkSessions()
        {
            //LoginURL Test
            //ログイン前に振ってる物のチェック
            //CheckerWebResponse firstResponse = getResponse(loginUrl, null, null);

            //HomeURL Test
            //ログイン前に振った物をそのまま利用してるもの
            String formText = "";
            foreach (KeyValuePair<string, FormInput> pair in formdata.table)
            {
                if (pair.Key != null && !pair.Key.Equals(""))
                {
                    formText += Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value.value) + "&";
                }
            }
            formText = formText.Substring(0, formText.Length - 1);
            CheckerWebResponse secondResponse = getResponse(homeUrl, formText, firstResponse.cookieContainer,loginUrl);

            //セッションアダプション用
            //振るタイミングは、ログイン画面を開いた後から、ログインをクリックする前までの間
            CookieContainer cookieCont = copyContainer(secondResponse.cookieContainer, homeUrl);
            Hashtable randomSessiontable = new Hashtable();
            bool checkRand = false;
            Hashtable mergeTable = new Hashtable();
            foreach (DictionaryEntry dic in firstResponse.cookietable)
            {
                mergeTable[dic.Key] = dic.Value;
            }
            foreach (DictionaryEntry dic in secondResponse.cookietable)
            {
                mergeTable[dic.Key] = dic.Value;
            }
            foreach (DictionaryEntry dic in mergeTable)
            {
                CookieData tmp = (CookieData)dic.Value;
                if (isSession(tmp))
                {
                    checkRand = true;
                    Random rand = new Random();
                    String randst = "";
                    char[] randchar = new char[tmp.value.Length];
                    for (int i = 0; i < tmp.value.Length; ++i)
                    {
                        randst += rand.Next(9);
                    }
                    cookieCont.GetCookies(new Uri(homeUrl))[tmp.name].Value = randst;
                    randomSessiontable.Add(tmp.name, randst);
                }
            }
            CheckerWebResponse secondRandomSessionResponse = null;
            if (checkRand)
            {
                secondRandomSessionResponse = getResponse(homeUrl, formText, cookieCont,loginUrl);
            }
            else
            {
                secondRandomSessionResponse = new CheckerWebResponse();
                secondRandomSessionResponse.body = null;
            }

            //セッションが一画面ごとに変わっていないかチェックするためのもの
            CheckerWebResponse secondResponseFixedCheckResult = null;
            if (secondResponse.statusCode >= 300 && secondResponse.statusCode <= 399)
            {
                secondResponseFixedCheckResult = getResponse(secondResponse.location, null, secondResponse.cookieContainer,loginUrl);
            }
            else if (getOnlyUrl != null)
            {
                secondResponseFixedCheckResult = getResponse(getOnlyUrl, null, secondResponse.cookieContainer,loginUrl);
            }

            //Check
            Hashtable firstCookietable = firstResponse.cookietable;
            Hashtable secondCookietable = secondResponse.cookietable;
            Hashtable secondResponseFixedChecktable = null;
            Hashtable secondRandomCookietable = secondRandomSessionResponse.cookietable;

            //ログイン前に割り振ったセッションをそのまま利用していないかのチェック
            fixChecker(firstCookietable, secondCookietable);

            //ログイン後にセッションを毎回変えているかのチェック
            if (secondResponseFixedCheckResult != null)
            {
                secondResponseFixedChecktable = secondResponseFixedCheckResult.cookietable;
                fixChecker(secondCookietable, secondResponseFixedChecktable);
            }

            //ログイン直前に割り振られた偽セッションをそのまま利用していないかのチェック
            foreach (DictionaryEntry dic in randomSessiontable)
            {
                String key = (String)dic.Key;
                if (secondRandomCookietable.ContainsKey(key))
                {
                    if ((String)randomSessiontable[key] == ((CookieData)secondRandomCookietable[key]).value)
                    {
                        ((CookieData)secondRandomCookietable[key]).fix = true;
                    }
                }
                else
                {
                    secondRandomCookietable.Add(key, new CookieData(key, (String)randomSessiontable[key], null, null, null, false, false));
                    ((CookieData)secondRandomCookietable[key]).fix = true;
                }
            }

            //レスポンス生成
            CheckResult retCheck = new CheckResult();

            //Set body 
            retCheck.firstBody = firstResponse.body;
            retCheck.secondBody = secondResponse.body;
            if (secondResponseFixedCheckResult != null) retCheck.secondResponseFixedCheckBody = secondResponseFixedCheckResult.body;
            retCheck.secondRandomBody = secondRandomSessionResponse.body;

            //レスポンスにセット
            retCheck.firstCookieCheck = firstCookietable;
            retCheck.secondCookieCheck = secondCookietable;
            retCheck.secondRandomCookieCheck = secondRandomCookietable;
            if (secondResponseFixedCheckResult != null) retCheck.secondResponseFixedCheck = secondResponseFixedChecktable;
            checkResult = retCheck;
        }

        //セッションっぽいものならTrueを返す
        public virtual bool isSession(CookieData data)
        {
            if (data == null)
            {
                return false;
            }
            if (
                data.name.ToLower().IndexOf("phpsessid") != -1 ||
                data.name.IndexOf("jsessionid") != -1 ||
                data.name.ToLower().IndexOf("asp.net_sessionid") != -1 ||
                System.Text.RegularExpressions.Regex.IsMatch(data.value, @"^[0-9abcdef]{16,}$")
                )
            {
                return true;
            }
            return false;
        }

        //cookieからhashtableへ
        public static Hashtable makeCookieTable(string stringCookies)
        {
            Hashtable cookietable = new Hashtable();
            if (stringCookies != null)
            {
                String[] splitcookie = Regex.Split(stringCookies, "(?<!expires=[^ ]{3}),",RegexOptions.IgnoreCase);

                for (int i = 0; i < splitcookie.Length; ++i)
                {
                    String[] namevalues = splitcookie[i].Split(new String[] { "; " }, StringSplitOptions.None);
                    CookieData cookiemake = new CookieData();

                    for (int t = 0; t < namevalues.Length; ++t)
                    {
                        String[] tmp = namevalues[t].Split('=');
                        /*
                        if (tmp.Length == 1)
                        {
                            cookiemake.name = tmp[0];
                            cookiemake.value = "";
                        }
                         * */
                        if (tmp[0].ToLower() == "path")
                        {
                            cookiemake.path = tmp[1];
                        }
                        else if (tmp[0].ToLower() == "domain")
                        {
                            cookiemake.domain = tmp[1];
                        }
                        else if (tmp[0].ToLower() == "httponly")
                        {
                            cookiemake.httponly = true;
                        }
                        else if (tmp[0].ToLower() == "secure")
                        {
                            cookiemake.secure = true;
                        }
                        else if (tmp[0].ToLower() == "expires")
                        {
                            cookiemake.expires = tmp[1];
                        }
                        else
                        {
                            cookiemake.name = tmp[0];
                            cookiemake.value = tmp.Length > 1 ? tmp[1] : "";
                            cookiemake.fix = false;
                        }
                    }
                    cookietable[cookiemake.name] = cookiemake;
                }
            }
            return cookietable;
        }

        //Urlにcookie持ってpostして結果を返すメソッド
        private CheckerWebResponse getResponse(String url, String post, CookieContainer cookie, String referer)
        {
            HttpWebRequest httpConnection = (HttpWebRequest)WebRequest.Create(url);
            httpConnection.AllowAutoRedirect = false;
            httpConnection.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) SessionFixationChecker";
            if (referer != null)
            {
                httpConnection.Referer = referer;
            }

            //Proxy設定　デバッグ用
            //System.Net.WebProxy proxy = new System.Net.WebProxy("localhost", 8080);
            //httpConnection.Proxy = proxy;

            //Set Cookie
            if (cookie == null)
            {
                cookie = new CookieContainer();
            }
            else
            {
                CookieCollection collection = cookie.GetCookies(new Uri(url));
                /*
                foreach (var dic in collection)
                {
                    Console.Write(dic);
                }
                */
            }

            httpConnection.CookieContainer = cookie;

            //連送防止用Sleep
            //System.Threading.Thread.Sleep(100);

            //POST
            if (post != null)
            {
                if (post != "")
                {
                    httpConnection.Method = "POST";
                    httpConnection.ContentType = "application/x-www-form-urlencoded";
                }
            }

            //Do Not Expect 100 
            httpConnection.ServicePoint.Expect100Continue = false;

            //ストリームを開いてPOST
            //(これ以降設定をいじれなくなるのでここまでで設定を終わらせる
            if (post != null)
            {
                if (post != "")
                {
                    httpConnection.GetRequestStream().Write(Encoding.ASCII.GetBytes(post), 0, Encoding.ASCII.GetBytes(post).Length);
                }
            }



            //Get Response
            try
            {
                CheckerWebResponse resp = new CheckerWebResponse();
                HttpWebResponse httpResponse = (HttpWebResponse)httpConnection.GetResponse();
                Stream resStream = httpResponse.GetResponseStream();
                MemoryStream ms = new MemoryStream();

                while (true)
                {
                    byte[] tmp = new byte[1024];
                    int len = resStream.Read(tmp, 0, 1024);
                    if (len <= 0)
                    {
                        break;
                    }
                    else
                    {
                        ms.Write(tmp, 0, len);
                    }
                }
                byte[] byteData = ms.ToArray();
                ms.Close();

                Encoding encode = EncodeLib.GetCode(byteData);
                if (encode != null)
                {
                    resp.body = encode.GetString(byteData);
                }
                else
                {
                    //デフォルトではUnicode
                    resp.body = Encoding.Unicode.GetString(byteData);
                }
                //クッキーを入手
                String cookietext = httpResponse.Headers["Set-Cookie"];

                resStream.Close();

                resp.cookieContainer = cookie;
                resp.cookietable = makeCookieTable(cookietext);
                resp.location = httpResponse.Headers["Location"];
                resp.statusCode = httpResponse.StatusCode.GetHashCode();

                return resp;
            }
            catch (System.Net.WebException ex)
            {
                //HTTPプロトコルエラーかどうか調べる
                if (ex.Status == System.Net.WebExceptionStatus.ProtocolError)
                {
                    //HttpWebResponseを取得
                    System.Net.HttpWebResponse erresp = (System.Net.HttpWebResponse)ex.Response;
                    String cookietext = erresp.Headers["Set-Cookie"];
                    String location = erresp.Headers["Location"];
                    CheckerWebResponse resp = new CheckerWebResponse();

                    resp.cookieContainer = cookie;
                    resp.cookietable = makeCookieTable(cookietext);
                    resp.statusCode = erresp.StatusCode.GetHashCode();
                    resp.body = erresp.StatusDescription;
                    resp.location = location;
                    return resp;
                }
                else
                {
                    Console.WriteLine(ex.Message);
                    //HttpWebResponseを取得
                    System.Net.HttpWebResponse erresp = (System.Net.HttpWebResponse)ex.Response;
                    if (ex.Response == null) 
                    {
                        throw new Exception(ex.Message);
                    }
                    String cookietext = erresp.Headers["Set-Cookie"];
                    CheckerWebResponse resp = new CheckerWebResponse();

                    resp.cookieContainer = cookie;
                    resp.cookietable = makeCookieTable(cookietext);
                    resp.statusCode = 900;
                    resp.body = erresp.StatusDescription;
                    return resp;
                }
            }
        }

        private void fixChecker(Hashtable checktable, Hashtable infotable)
        {
            foreach (DictionaryEntry dic in checktable)
            {
                String key = (String)dic.Key;
                if (infotable.ContainsKey(key))
                {
                    if (((CookieData)checktable[key]).nvEqual((CookieData)infotable[key]))
                    {
                        ((CookieData)checktable[key]).fix = true;
                    }
                }
                else
                {
                    ((CookieData)checktable[key]).fix = true;
                }
            }
        }

        private CookieContainer copyContainer(CookieContainer from, String url)
        {
            CookieCollection fromCookieCollection = from.GetCookies(new Uri(url));
            CookieCollection toCookieCollection = new CookieCollection();
            Cookie[] s = new Cookie[fromCookieCollection.Count];
            fromCookieCollection.CopyTo(s, 0);
            for (int i = 0; i < s.Length; ++i)
            {
                toCookieCollection.Add(s[i]);
            }
            CookieContainer ret = new CookieContainer();
            ret.Add(toCookieCollection);

            return ret;
        }

    }


    //Data
    public class CheckerWebResponse
    {
        public String body;
        public Hashtable cookietable;
        public CookieContainer cookieContainer;
        public int statusCode;
        public String location;

        public CheckerWebResponse()
        {
            body = "";
            cookietable = new Hashtable();
            cookieContainer = new CookieContainer();
            statusCode = 400;
            location = "";
        }
    }
    /// <summary>
    /// フォームのデータを纏める物（formタグに相当）
    /// Dictionary(string, FormInput) table　が、実際のinput要素を纏めるテーブル
    /// </summary>
    public class FormData
    {
        public String action;
        public Dictionary<string, FormInput> table;
        public String name;
        public FormData(String _name, String _action)
        {
            name = _name;
            action = _action;
            table = new Dictionary<String, FormInput>();
        }
    }

    /// <summary>
    /// フォームのデータを纏める物（inputタグに相当）
    /// プロパティは各要素に相当
    /// </summary>
    public class FormInput
    {
        public String type;
        public String value;
        public FormInput(String _type, String _value)
        {
            type = _type;
            value = _value;
        }
    }


    /// <summary>
    /// <para>レスポンスを纏めるクラス</para>
    /// 
    /// <para>
    ///  <para>first:ログイン前のページに普通にアクセスした結果 </para>
    ///  <para>　実URL = loginUrl</para>
    ///  <para>　fix=その要素がログイン後も更新されていない事を示す</para>
    /// </para>
    /// 
    /// 
    /// <para>
    ///  <para>second:ログイン処理ページに普通にアクセスした結果 </para>
    ///  <para>　実URL = homeUrl</para>
    ///  <para>　fix=その要素がログイン後、二画面遷移後(302)も更新されていない事を示す</para>
    /// </para>　　　
    /// 
    /// <para>
    ///  <para>secondRandom:ログイン処理ページにおいて、セッションと思われるものをランダムに書き換えてログインを試みた結果</para>
    ///  <para>　実URL = homeUrl</para>
    ///  <para>　fix=ログイン前からユーザーが持ってる任意のセッションIDを利用して、セッションを使用している事を示す</para>
    /// </para>
    /// 
    /// <para>
    ///  <para>secondResponseFixedCheck:GETリクエスト若しくは攻撃者にとって既知のPOSTリクエストを受け取るページに遷移した結果</para>
    ///  <para>　実URL = homeUrlにおいてLocationが指定されていた場合はそのURLに、指定されておらず、かつgetOnlyUrlがnullでなければgetOnlyUrl</para>
    ///  <para>　これはsecondのfix判定に使っている。実URLがnullであればこのリクエストは実際には飛ばず、nullが代入される。</para>
    ///  <para>　当然secondのfixチェックも行われない</para>
    /// </para>
    /// 
    /// </summary>
    /// 
    public class CheckResult
    {
        public Hashtable firstCookieCheck;
        public Hashtable secondCookieCheck;
        public Hashtable secondRandomCookieCheck;
        public Hashtable secondResponseFixedCheck;
        public String firstBody;
        public String secondBody;
        public String secondRandomBody;
        public String secondResponseFixedCheckBody;
    }


    public class CookieData
    {
        public String name, value, path, domain, expires;
        public bool httponly, secure, fix;
        public CookieData(String _name, String _value, String _path, String _domain, String _expires, bool _httponly, bool _secure)
        {
            name = _name;
            value = _value;
            path = _path;
            domain = _domain;
            httponly = _httponly;
            secure = _secure;
            expires = _expires;
            fix = false;
        }
        public CookieData()
        {
            httponly = false;
            secure = false;
        }
        public bool nvEqual(CookieData a)
        {
            if (a == null)
            {
                return false;
            }
            if (this.name == a.name && this.value == a.value)
            {
                return true;
            }
            return false;

        }
    }


    class EncodeLib
    {
        /// <summary>
        /// 文字コードを判別する
        /// </summary>
        /// <remarks>
        /// Jcode.pmのgetcodeメソッドを移植したものです。
        /// Jcode.pm(http://openlab.ring.gr.jp/Jcode/index-j.html)
        /// Jcode.pmのCopyright: Copyright 1999-2005 Dan Kogai
        /// </remarks>
        /// <param name="bytes">文字コードを調べるデータ</param>
        /// <returns>適当と思われるEncodingオブジェクト。
        /// 判断できなかった時はnull。</returns>
        /// From http://dobon.net/vb/dotnet/string/detectcode.html 

        public static System.Text.Encoding GetCode(byte[] bytes)
        {
            const byte bEscape = 0x1B;
            const byte bAt = 0x40;
            const byte bDollar = 0x24;
            const byte bAnd = 0x26;
            const byte bOpen = 0x28;    //'('
            const byte bB = 0x42;
            const byte bD = 0x44;
            const byte bJ = 0x4A;
            const byte bI = 0x49;

            int len = bytes.Length;
            byte b1, b2, b3, b4;

            //Encode::is_utf8 は無視

            bool isBinary = false;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
                {
                    //'binary'
                    isBinary = true;
                    if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F)
                    {
                        //smells like raw unicode
                        return System.Text.Encoding.Unicode;
                    }
                }
            }
            if (isBinary)
            {
                return null;
            }

            //not Japanese
            bool notJapanese = true;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 == bEscape || 0x80 <= b1)
                {
                    notJapanese = false;
                    break;
                }
            }
            if (notJapanese)
            {
                return System.Text.Encoding.ASCII;
            }

            for (int i = 0; i < len - 2; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                b3 = bytes[i + 2];

                if (b1 == bEscape)
                {
                    if (b2 == bDollar && b3 == bAt)
                    {
                        //JIS_0208 1978
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bDollar && b3 == bB)
                    {
                        //JIS_0208 1983
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && (b3 == bB || b3 == bJ))
                    {
                        //JIS_ASC
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && b3 == bI)
                    {
                        //JIS_KANA
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    if (i < len - 3)
                    {
                        b4 = bytes[i + 3];
                        if (b2 == bDollar && b3 == bOpen && b4 == bD)
                        {
                            //JIS_0212
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                        if (i < len - 5 &&
                            b2 == bAnd && b3 == bAt && b4 == bEscape &&
                            bytes[i + 4] == bDollar && bytes[i + 5] == bB)
                        {
                            //JIS_0208 1990
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                    }
                }
            }

            //should be euc|sjis|utf8
            //use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
            int sjis = 0;
            int euc = 0;
            int utf8 = 0;
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0x81 <= b1 && b1 <= 0x9F) || (0xE0 <= b1 && b1 <= 0xFC)) &&
                    ((0x40 <= b2 && b2 <= 0x7E) || (0x80 <= b2 && b2 <= 0xFC)))
                {
                    //SJIS_C
                    sjis += 2;
                    i++;
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0xA1 <= b1 && b1 <= 0xFE) && (0xA1 <= b2 && b2 <= 0xFE)) ||
                    (b1 == 0x8E && (0xA1 <= b2 && b2 <= 0xDF)))
                {
                    //EUC_C
                    //EUC_KANA
                    euc += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if (b1 == 0x8F && (0xA1 <= b2 && b2 <= 0xFE) &&
                        (0xA1 <= b3 && b3 <= 0xFE))
                    {
                        //EUC_0212
                        euc += 3;
                        i += 2;
                    }
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if ((0xC0 <= b1 && b1 <= 0xDF) && (0x80 <= b2 && b2 <= 0xBF))
                {
                    //UTF8
                    utf8 += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if ((0xE0 <= b1 && b1 <= 0xEF) && (0x80 <= b2 && b2 <= 0xBF) &&
                        (0x80 <= b3 && b3 <= 0xBF))
                    {
                        //UTF8
                        utf8 += 3;
                        i += 2;
                    }
                }
            }
            //M. Takahashi's suggestion
            //utf8 += utf8 / 2;

            System.Diagnostics.Debug.WriteLine(
                string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
            if (euc > sjis && euc > utf8)
            {
                //EUC
                return System.Text.Encoding.GetEncoding(51932);
            }
            else if (sjis > euc && sjis > utf8)
            {
                //SJIS
                return System.Text.Encoding.GetEncoding(932);
            }
            else if (utf8 > euc && utf8 > sjis)
            {
                //UTF8
                return System.Text.Encoding.UTF8;
            }
            return null;
        }
    }
}