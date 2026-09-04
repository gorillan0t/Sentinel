using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sentinel;

internal class OAuthSession
{

    public static readonly HttpClient HttpClient = new()
    {
            Timeout = TimeSpan.FromSeconds(10.0),
    };

    private readonly string authorizationEndpoint;

    private readonly int callbackPort;

    private readonly string extraAuthorizationQuery;

    private readonly string redirectUri;

    private readonly string scopes;

    private readonly string sessionPath;

    private readonly string tokenEndpoint;

    private string accessToken;

    private DateTime accessTokenExpiry;

    public string ClientId = "";

    public string ClientSecret = "";

    public bool IsConnected;

    public bool LoginPending;

    private string refreshToken;

    public string StatusMessage = "";

    public OAuthSession(string sessionFileName, string authorizationEndpoint, string tokenEndpoint, string scopes, int callbackPort, string callbackPath, string extraAuthorizationQuery)
    {
        sessionPath                  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SentinelLoader", sessionFileName);
        this.authorizationEndpoint   = authorizationEndpoint;
        this.tokenEndpoint           = tokenEndpoint;
        this.scopes                  = scopes;
        this.callbackPort            = callbackPort;
        this.extraAuthorizationQuery = extraAuthorizationQuery;
        redirectUri                  = "http://127.0.0.1:" + callbackPort + callbackPath;
    }

    public void LoadSession()
    {
        try
        {
            JObject val = JObject.Parse(File.ReadAllText(sessionPath));
            accessToken  = (string)val["access_token"];
            refreshToken = (string)val["refresh_token"];
            IsConnected  = !string.IsNullOrEmpty(refreshToken);
        }
        catch { }
    }

    public void ClearSession()
    {
        IsConnected   = false;
        accessToken   = refreshToken = null;
        StatusMessage = "";
        try
        {
            File.Delete(sessionPath);
        }
        catch { }
    }

    public void ExpireAccessToken() => accessTokenExpiry = DateTime.MinValue;

    private static string Base64UrlEncode(byte[] byte_0) =>
            Convert.ToBase64String(byte_0).TrimEnd('=').Replace('+', '-')
                   .Replace('/', '_');

    public void BeginLogin()
    {
        if (!LoginPending && ClientId.Length != 0)
        {
            byte[] array = new byte[64];
            using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
            {
                randomNumberGenerator.GetBytes(array);
            }

            string codeVerifier  = Base64UrlEncode(array);
            string expectedState = Base64UrlEncode(Guid.NewGuid().ToByteArray());
            string text;
            using (SHA256 sHA = SHA256.Create())
            {
                text = Base64UrlEncode(sHA.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));
            }

            TcpListener listener = new(IPAddress.Loopback, callbackPort);
            try
            {
                listener.Start();
            }
            catch
            {
                StatusMessage = "PORT " + callbackPort + " IS TAKEN";

                return;
            }

            LoginPending  = true;
            StatusMessage = "CHECK YOUR BROWSER";
            Thread thread = new((ThreadStart)delegate
                                             {
                                                 HandleOAuthCallback(listener, codeVerifier, expectedState);
                                             });

            thread.IsBackground = true;
            thread.Start();
            Application.OpenURL(authorizationEndpoint + "?response_type=code&client_id=" + ClientId + "&scope=" + Uri.EscapeDataString(scopes) + "&code_challenge_method=S256&code_challenge=" + text + "&redirect_uri=" + Uri.EscapeDataString(redirectUri) + "&state=" + expectedState + extraAuthorizationQuery);
        }
    }

    private void HandleOAuthCallback(TcpListener tcpListener_0, string string_11, string string_12)
    {
        string text  = null;
        string text2 = null;
        try
        {
            for (int i = 0; i < 1800; i++)
            {
                if (tcpListener_0.Pending())
                {
                    break;
                }

                Thread.Sleep(100);
            }

            if (!tcpListener_0.Pending())
            {
                SetLoginError("LOGIN TIMED OUT");

                return;
            }

            using TcpClient     tcpClient     = tcpListener_0.AcceptTcpClient();
            using NetworkStream networkStream = tcpClient.GetStream();
            networkStream.ReadTimeout = 5000;
            byte[] array = new byte[4096];
            string text3 = Encoding.ASCII.GetString(array, 0, networkStream.Read(array, 0, array.Length)).Split('\n')[0];
            int    num   = text3.IndexOf('?');
            int    num2  = text3.LastIndexOf(' ');
            if (num > 0 && num2 > num)
            {
                string[] array2 = text3.Substring(num + 1, num2 - num - 1).Split('&');
                foreach (string text4 in array2)
                {
                    int num3 = text4.IndexOf('=');
                    if (num3 < 0)
                    {
                        continue;
                    }

                    string text5 = Uri.UnescapeDataString(text4.Substring(num3 + 1));
                    if (!text4.StartsWith("code="))
                    {
                        if (text4.StartsWith("state="))
                        {
                            text2 = text5;
                        }
                    }
                    else
                    {
                        text = text5;
                    }
                }
            }

            byte[] bytes  = Encoding.UTF8.GetBytes("<html><body style='background:#0a1520;color:#dff;font-family:sans-serif;text-align:center;padding-top:20vh'><h2>" + (text != null ? "Sentinel is connected" : "Login failed") + "</h2>You can close this tab</body></html>");
            byte[] bytes2 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nConnection: close\r\nContent-Length: "                             + bytes.Length                                              + "\r\n\r\n");
            networkStream.Write(bytes2, 0, bytes2.Length);
            networkStream.Write(bytes,  0, bytes.Length);
        }
        catch { }
        finally
        {
            try
            {
                tcpListener_0.Stop();
            }
            catch { }
        }

        if (text != null && !(text2 != string_12))
        {
            if (!ExchangeToken("grant_type=authorization_code&code=" + Uri.EscapeDataString(text) + "&redirect_uri=" + Uri.EscapeDataString(redirectUri) + "&code_verifier=" + string_11, out bool _))
            {
                SetLoginError("LOGIN FAILED");

                return;
            }

            LoginPending  = false;
            StatusMessage = "";
            IsConnected   = true;
        }
        else
        {
            SetLoginError("LOGIN FAILED");
        }
    }

    private void SetLoginError(string string_11)
    {
        LoginPending  = false;
        StatusMessage = string_11;
    }

    private bool ExchangeToken(string string_11, out bool bool_2)
    {
        bool_2    = false;
        string_11 = string_11 + "&client_id=" + ClientId + (ClientSecret.Length > 0 ? "&client_secret=" + Uri.EscapeDataString(ClientSecret) : "");
        try
        {
            using HttpResponseMessage httpResponseMessage = HttpClient.PostAsync(tokenEndpoint, new StringContent(string_11, Encoding.ASCII, "application/x-www-form-urlencoded")).Result;
            string                    result              = httpResponseMessage.Content.ReadAsStringAsync().Result;
            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                bool_2 = httpResponseMessage.StatusCode == HttpStatusCode.BadRequest || httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized;

                return false;
            }

            JObject val = JObject.Parse(result);
            accessToken = (string)val["access_token"];
            string value = (string)val["refresh_token"];
            if (!string.IsNullOrEmpty(value))
            {
                refreshToken = value;
            }

            accessTokenExpiry = DateTime.UtcNow.AddSeconds(((int?)val["expires_in"] ?? 3600) - 60);
            Directory.CreateDirectory(Path.GetDirectoryName(sessionPath));
            File.WriteAllText(sessionPath, new JObject
            {
                    ["access_token"]  = JToken.Parse(accessToken),
                    ["refresh_token"] = JToken.Parse(refreshToken),
            }.ToString());

            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool EnsureAccessToken()
    {
        if (DateTime.UtcNow < accessTokenExpiry)
        {
            return true;
        }

        bool result = ExchangeToken("grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken ?? ""), out bool bool_);
        if (bool_)
        {
            ClearSession();
            StatusMessage = "LOGIN EXPIRED";
        }

        return result;
    }

    public HttpRequestMessage CreateAuthorizedRequest(HttpMethod httpMethod_0, string string_11)
    {
        HttpRequestMessage httpRequestMessage = new(httpMethod_0, string_11);
        httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (httpMethod_0 != HttpMethod.Get)
        {
            httpRequestMessage.Content = new StringContent("");
        }

        return httpRequestMessage;
    }

    private sealed class OAuthCallbackState
    {

        public string codeVerifier;

        public string expectedState;

        public TcpListener  listener;
        public OAuthSession session;

        internal void RunCallbackListener() => session.HandleOAuthCallback(listener, codeVerifier, expectedState);
    }
}