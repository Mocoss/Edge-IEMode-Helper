using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Xml;

namespace IETabHelper
{
    #region 消息数据模型
    public class CommandMessage
    {
        public string action { get; set; }
        public string url { get; set; }
    }

    public class ResponseMessage
    {
        public bool success { get; set; }
        public string error { get; set; }
    }
    #endregion

    internal class Program
    {
        private static readonly JavaScriptSerializer _jsonSerializer = new JavaScriptSerializer();
        private static Thread _listenThread;
        private static string _siteListPath;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                _siteListPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "iemode_sitelist.xml");
                
                // 启动时配置全局IE模式策略
                ConfigureGlobalPolicy();

                _listenThread = new Thread(StartNativeMessagingLoop);
                _listenThread.IsBackground = true;
                _listenThread.Name = "NativeMessaging_Listener";
                _listenThread.Start();

                Application.Run();
            }
            catch (Exception ex)
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    " 启动失败：" + ex.Message + "\r\n" + ex.StackTrace + "\r\n\r\n");
            }
        }

        #region 全局策略配置
        private static void ConfigureGlobalPolicy()
        {
            try
            {
                string edgePolicyPath = @"Software\Policies\Microsoft\Edge";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(edgePolicyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    // 启用IE模式总开关
                    key.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                    // 站点列表自动刷新间隔（官方最小值30分钟，单位：分钟）
                    key.SetValue("InternetExplorerIntegrationSiteListRefreshInterval", 30, RegistryValueKind.DWord);
                    // 允许右键手动切换IE模式
                    key.SetValue("InternetExplorerIntegrationReloadInIEModeAllowed", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
        #endregion

        #region 原生消息核心循环（Chromium 标准协议）
        private static void StartNativeMessagingLoop()
        {
            Stream stdin = Console.OpenStandardInput();
            Stream stdout = Console.OpenStandardOutput();
            byte[] lengthBuffer = new byte[4];

            while (true)
            {
                try
                {
                    // 读取4字节消息长度（小端序）
                    int bytesRead = ReadFull(stdin, lengthBuffer, 4);
                    if (bytesRead < 4) break;

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0 || messageLength > 10 * 1024 * 1024) break;

                    // 读取完整JSON消息体
                    byte[] messageBuffer = new byte[messageLength];
                    bytesRead = ReadFull(stdin, messageBuffer, messageLength);
                    if (bytesRead < messageLength) break;

                    string jsonRequest = Encoding.UTF8.GetString(messageBuffer);
                    string jsonResponse = ProcessCommand(jsonRequest);

                    // 按标准格式返回响应
                    byte[] responseBytes = Encoding.UTF8.GetBytes(jsonResponse);
                    byte[] responseLength = BitConverter.GetBytes(responseBytes.Length);
                    stdout.Write(responseLength, 0, 4);
                    stdout.Write(responseBytes, 0, responseBytes.Length);
                    stdout.Flush();
                }
                catch
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 保证读取完整字节数，解决流分片读取导致的解析错误
        /// </summary>
        private static int ReadFull(Stream stream, byte[] buffer, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, totalRead, count - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }
        #endregion

        #region 业务命令处理
        private static string ProcessCommand(string jsonRequest)
        {
            try
            {
                CommandMessage cmd = _jsonSerializer.Deserialize<CommandMessage>(jsonRequest);
                if (cmd == null || string.IsNullOrEmpty(cmd.action))
                    return BuildErrorResponse("无效的命令格式");

                switch (cmd.action.ToLower())
                {
                    case "openurl":
                        if (string.IsNullOrEmpty(cmd.url))
                            return BuildErrorResponse("网址不能为空");
                        AddSiteToIEModeList(cmd.url);
                        OpenUrlInCurrentEdge(cmd.url);
                        break;

                    default:
                        return BuildErrorResponse("不支持的命令：" + cmd.action);
                }

                return BuildSuccessResponse();
            }
            catch (Exception ex)
            {
                return BuildErrorResponse("命令执行失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 追加站点到IE模式企业列表，自动去重，版本号递增触发Edge识别更新
        /// </summary>
        private static void AddSiteToIEModeList(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                string siteHost = uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port);

                XmlDocument xmlDoc = new XmlDocument();
                int currentVersion = 1;
                bool siteExists = false;

                // 读取现有XML文件
                if (File.Exists(_siteListPath))
                {
                    try
                    {
                        xmlDoc.Load(_siteListPath);
                        XmlNode root = xmlDoc.SelectSingleNode("site-list");
                        if (root.Attributes["version"] != null)
                        {
                            int.TryParse(root.Attributes["version"].Value, out currentVersion);
                        }

                        // 检查站点是否已存在，自动去重
                        XmlNodeList siteNodes = xmlDoc.SelectNodes("site-list/site");
                        foreach (XmlNode node in siteNodes)
                        {
                            if (node.Attributes["url"] != null && node.Attributes["url"].Value == siteHost)
                            {
                                siteExists = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        xmlDoc = new XmlDocument();
                    }
                }

                // 创建根节点（文件不存在或损坏时）
                if (xmlDoc.DocumentElement == null)
                {
                    XmlDeclaration decl = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null);
                    xmlDoc.AppendChild(decl);
                    XmlElement root = xmlDoc.CreateElement("site-list");
                    root.SetAttribute("version", currentVersion.ToString());
                    xmlDoc.AppendChild(root);
                }

                // 站点不存在则追加
                if (!siteExists)
                {
                    XmlElement siteNode = xmlDoc.CreateElement("site");
                    siteNode.SetAttribute("url", siteHost);

                    XmlElement compatNode = xmlDoc.CreateElement("compat-mode");
                    compatNode.InnerText = "Default";
                    siteNode.AppendChild(compatNode);

                    XmlElement openNode = xmlDoc.CreateElement("open-in");
                    openNode.SetAttribute("allow-redirect", "true");
                    openNode.InnerText = "IE11";
                    siteNode.AppendChild(openNode);

                    xmlDoc.DocumentElement.AppendChild(siteNode);

                    // 版本号+1，确保Edge识别到列表更新
                    currentVersion++;
                    xmlDoc.DocumentElement.SetAttribute("version", currentVersion.ToString());

                    xmlDoc.Save(_siteListPath);
                }

                // 更新注册表站点列表路径
                string fileUrl = "file:///" + _siteListPath.Replace('\\', '/');
                string edgePolicyPath = @"Software\Policies\Microsoft\Edge";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(edgePolicyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
                {
                    key.SetValue("InternetExplorerIntegrationSiteList", fileUrl, RegistryValueKind.String);
                }
            }
            catch { }
        }

        /// <summary>
        /// 在当前Edge窗口新建标签页打开网址
        /// </summary>
        private static void OpenUrlInCurrentEdge(string url)
        {
            string edgePath = FindEdgePath();
            if (string.IsNullOrEmpty(edgePath))
                throw new Exception("未检测到 Edge 浏览器安装路径");

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = edgePath;
            psi.Arguments = string.Format("\"{0}\"", url);
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        /// <summary>
        /// 自动查找 Edge 浏览器安装路径
        /// </summary>
        private static string FindEdgePath()
        {
            string[] commonPaths = new string[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\Edge\Application\msedge.exe")
            };

            foreach (string path in commonPaths)
            {
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        private static string BuildSuccessResponse()
        {
            return _jsonSerializer.Serialize(new ResponseMessage { success = true });
        }

        private static string BuildErrorResponse(string errorMsg)
        {
            return _jsonSerializer.Serialize(new ResponseMessage { success = false, error = errorMsg });
        }
        #endregion
    }
}