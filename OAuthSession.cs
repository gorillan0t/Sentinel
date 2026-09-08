using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sentinel;
using UnityEngine;

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

    private readonly object stateLock = new();

    private readonly string tokenEndpoint;

    private readonly SemaphoreSlim tokenLock = new(1, 1);

    private string accessToken;

    private DateTime accessTokenExpiry;

    private TcpListener callbackListener;

    private string clientId = "";

    public string ClientSecret = "";

    public bool IsConnected;

    private DateTime lastRefreshAttempt;

    private int loginGeneration;

    public bool LoginPending;

    private string refreshToken;

    public string StatusMessage = "";

    [MethodImpl(MethodImplOptions.NoInlining)]
    public OAuthSession(string sessionFileName, string authorizationEndpoint, string tokenEndpoint, string scopes, int callbackPort, string callbackPath, string extraAuthorizationQuery)
    {
        sessionPath                  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sentinel", sessionFileName);
        this.authorizationEndpoint   = authorizationEndpoint;
        this.tokenEndpoint           = tokenEndpoint;
        this.scopes                  = scopes;
        this.callbackPort            = callbackPort;
        this.extraAuthorizationQuery = extraAuthorizationQuery;
        redirectUri                  = "http://127.0.0.1:" + callbackPort + callbackPath;
    }

    public string ClientId
    {
        get
        {
            lock (stateLock)
            {
                return clientId;
            }
        }

        set
        {
            lock (stateLock)
            {
                value = (value ?? "").Trim();
                if (!(clientId == value))
                {
                    clientId = value;
                    ResetState();
                }
            }
        }
    }

    public int Generation
    {
        get
        {
            lock (stateLock)
            {
                return loginGeneration;
            }
        }
    }

    private void ResetState()
    {
        loginGeneration++;
        accessToken       = refreshToken       = null;
        accessTokenExpiry = lastRefreshAttempt = DateTime.MinValue;
        LoginPending      = false;
        IsConnected       = false;
        StatusMessage     = "";
        try
        {
            callbackListener?.Stop();
        }
        catch { }

        callbackListener = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LoadSession()
    {
        lock (stateLock)
        {
            if (clientId.Length == 0 || refreshToken != null)
            {
                return;
            }

            try
            {
                JObject val  = JObject.Parse(File.ReadAllText(sessionPath));
                string  text = (string)val["client_id"];
                if (!string.IsNullOrEmpty(text) && text != clientId)
                {
                    StatusMessage = "CLIENT ID CHANGED: LOG IN AGAIN";

                    return;
                }

                accessToken  = (string)val["access_token"];
                refreshToken = (string)val["refresh_token"];
                IsConnected  = !string.IsNullOrEmpty(refreshToken);
                if (IsConnected && string.IsNullOrEmpty(text))
                {
                    SaveSession();
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch
            {
                StatusMessage = "SAVED LOGIN COULD NOT BE READ";
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ClearSession()
    {
        lock (stateLock)
        {
            ResetState();
            try
            {
                File.Delete(sessionPath);
            }
            catch
            {
                StatusMessage = "SAVED LOGIN COULD NOT BE REMOVED";
            }
        }
    }

    public void ExpireAccessToken()
    {
        lock (stateLock)
        {
            accessTokenExpiry = DateTime.MinValue;
        }
    }

    private static string Base64UrlEncode(byte[] arg1) =>
            Convert.ToBase64String(arg1).TrimEnd('=').Replace('+', '-')
                   .Replace('/', '_');

    private void RunDelayed(int arg1, Action arg2) =>
            MainThreadDispatch.Enqueue(delegate
                                       {
                                           lock (stateLock)
                                           {
                                               if (arg1 == loginGeneration)
                                               {
                                                   arg2();
                                               }
                                           }
                                       });

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void BeginLogin()
    {
        int    generation;
        string stringToEscape;
        lock (stateLock)
        {
            if (LoginPending || clientId.Length == 0)
            {
                return;
            }

            generation       = loginGeneration;
            stringToEscape   = clientId;
            callbackListener = new TcpListener(IPAddress.Loopback, callbackPort);
            try
            {
                callbackListener.Start();
            }
            catch
            {
                callbackListener = null;
                StatusMessage    = "PORT " + callbackPort + " IS TAKEN";

                return;
            }

            LoginPending  = true;
            StatusMessage = "CHECK YOUR BROWSER";
        }

        byte[] array = new byte[64];
        using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
        {
            randomNumberGenerator.GetBytes(array);
        }

        string codeVerifier  = Base64UrlEncode(array);
        string expectedState = Base64UrlEncode(Guid.NewGuid().ToByteArray());
        string text4;
        using (SHA256 sHA = SHA256.Create())
        {
            text4 = Base64UrlEncode(sHA.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        TcpListener listener = callbackListener;
        Task.Run(() => HandleOAuthCallback(listener, codeVerifier, expectedState, generation));
        Application.OpenURL(authorizationEndpoint + "?response_type=code&client_id=" + Uri.EscapeDataString(stringToEscape) + "&scope=" + Uri.EscapeDataString(scopes) + "&code_challenge_method=S256&code_challenge=" + text4 + "&redirect_uri=" + Uri.EscapeDataString(redirectUri) + "&state=" + expectedState + extraAuthorizationQuery);
    }

    private async Task HandleOAuthCallback(TcpListener arg1, string arg2, string arg3, int arg4)
    {
        try
        {
            DateTime dateTime = DateTime.UtcNow.AddMinutes(3.0);
            while (DateTime.UtcNow < dateTime && arg4 == Generation)
            {
                if (arg1.Pending())
                {
                    using TcpClient     tcpClient     = arg1.AcceptTcpClient();
                    using NetworkStream networkStream = tcpClient.GetStream();
                    networkStream.ReadTimeout = 5000;
                    string text;
                    using (StreamReader streamReader = new(networkStream, Encoding.ASCII, false, 1024, true))
                    {
                        text = streamReader.ReadLine();
                    }

                    string   codeVerifier  = null;
                    string   expectedState = null;
                    string[] array         = (text ?? "").Split(' ');
                    Uri      result        = array.Length >= 2 && array[0] == "GET" && Uri.TryCreate("http://127.0.0.1:" + callbackPort + array[1], UriKind.Absolute, out result) ? new Uri("http://127.0.0.1:" + callbackPort + array[1]) : null;
                    if (result != null && result.AbsolutePath == new Uri(redirectUri).AbsolutePath)
                    {
                        string[] array2 = result.Query.TrimStart('?').Split('&');
                        foreach (string text4 in array2)
                        {
                            int num = text4.IndexOf('=');
                            if (num >= 0)
                            {
                                string text5 = Uri.UnescapeDataString(text4.Substring(num + 1).Replace("+", " "));
                                if (text4.Substring(0, num) == "code")
                                {
                                    codeVerifier = text5;
                                }

                                if (text4.Substring(0, num) == "state")
                                {
                                    expectedState = text5;
                                }
                            }
                        }
                    }

                    bool flag = !string.IsNullOrEmpty(codeVerifier) && expectedState == arg3;
                    bool flag2;
                    if (flag2 = flag)
                    {
                        flag2 = await ExchangeTokenLocked("grant_type=authorization_code&code=" + Uri.EscapeDataString(codeVerifier) + "&redirect_uri=" + Uri.EscapeDataString(redirectUri) + "&code_verifier=" + arg2, arg4).ConfigureAwait(false);
                    }

                    bool   flag3  = flag2;
                    byte[] bytes  = Encoding.UTF8.GetBytes(flag3 ? "Sentinel is connected. You can close this tab." : "Login did not complete. Return to Sentinel for details.");
                    byte[] bytes2 = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nConnection: close\r\nContent-Length: " + bytes.Length + "\r\n\r\n");
                    networkStream.Write(bytes2, 0, bytes2.Length);
                    networkStream.Write(bytes,  0, bytes.Length);
                    if (flag)
                    {
                        RunDelayed(arg4, delegate
                                         {
                                             LoginPending = false;
                                         });

                        return;
                    }
                }
                else
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            RunDelayed(arg4, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                             {
                                 LoginPending  = false;
                                 StatusMessage = "LOGIN TIMED OUT";
                             });
        }
        catch
        {
            RunDelayed(arg4, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                             {
                                 LoginPending  = false;
                                 StatusMessage = "LOGIN FAILED: TRY AGAIN";
                             });
        }
        finally
        {
            try
            {
                arg1.Stop();
            }
            catch { }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void SaveSession()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath));
        File.WriteAllText(sessionPath, new JObject
        {
                ["access_token"]  = JToken.Parse(accessToken),
                ["refresh_token"] = JToken.Parse(refreshToken),
                ["client_id"]     = JToken.Parse(clientId),
        }.ToString());
    }

    private async Task<bool> ExchangeTokenLocked(string arg1, int arg2)
    {
        await tokenLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ExchangeToken(arg1, arg2).ConfigureAwait(false);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private async Task<bool> ExchangeToken(string arg1, int arg2)
    {
        string stringToEscape;
        string clientId = "";
        lock (stateLock)
        {
            if (arg2 != loginGeneration || clientId.Length == 0)
            {
                return false;
            }

            stringToEscape = clientId;
            clientId       = ClientSecret;
        }

        arg1 = arg1 + "&client_id=" + Uri.EscapeDataString(stringToEscape) + (!string.IsNullOrEmpty(clientId) ? "&client_secret=" + Uri.EscapeDataString(clientId) : "");
        try
        {
            using StringContent       content             = new(arg1, Encoding.UTF8, "application/x-www-form-urlencoded");
            using HttpResponseMessage httpResponseMessage = await HttpClient.PostAsync(tokenEndpoint, content).ConfigureAwait(false);
            string                    text                = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
            lock (stateLock)
            {
                if (arg2 != loginGeneration)
                {
                    return false;
                }

                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    int statusCode = (int)httpResponseMessage.StatusCode;
                    if (statusCode != 400 && statusCode != 401)
                    {
                        lastRefreshAttempt = DateTime.UtcNow.AddSeconds(GetResponseStatusCode(httpResponseMessage));
                        RunDelayed(arg2, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                         {
                                             StatusMessage = statusCode == 429 ? "LOGIN RATE LIMITED: WAIT AND RETRY" : "LOGIN SERVICE UNAVAILABLE: TRY AGAIN";
                                         });
                    }
                    else
                    {
                        accessToken       = refreshToken = null;
                        accessTokenExpiry = DateTime.MinValue;
                        RunDelayed(arg2, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                                         {
                                             IsConnected   = false;
                                             StatusMessage = "LOGIN EXPIRED: LOG IN AGAIN";
                                         });
                    }

                    return false;
                }

                JObject val   = JObject.Parse(text);
                string  value = (string)val["access_token"];
                if (string.IsNullOrEmpty(value))
                {
                    throw new FormatException();
                }

                accessToken        = value;
                refreshToken       = (string)val["refresh_token"] ?? refreshToken;
                accessTokenExpiry  = DateTime.UtcNow.AddSeconds(Math.Max(1, ((int?)val["expires_in"] ?? 3600) - 60));
                lastRefreshAttempt = DateTime.MinValue;
                string codeVerifier = "";
                try
                {
                    SaveSession();
                }
                catch
                {
                    codeVerifier = "CONNECTED: LOGIN COULD NOT BE SAVED";
                }

                RunDelayed(arg2, delegate
                                 {
                                     IsConnected   = true;
                                     StatusMessage = codeVerifier;
                                 });

                return true;
            }
        }
        catch
        {
            lock (stateLock)
            {
                if (arg2 == loginGeneration)
                {
                    lastRefreshAttempt = DateTime.UtcNow.AddSeconds(10.0);
                }
            }

            RunDelayed(arg2, [MethodImpl(MethodImplOptions.NoInlining)]() =>
                             {
                                 StatusMessage = "LOGIN NETWORK ERROR: TRY AGAIN";
                             });

            return false;
        }
    }

    public bool EnsureAccessTokenSync() => EnsureAccessToken().GetAwaiter().GetResult();

    async public Task<bool> EnsureAccessToken()
    {
        int num = Generation;
        await tokenLock.WaitAsync().ConfigureAwait(false);
        try
        {
            string stringToEscape;
            lock (stateLock)
            {
                if (num != loginGeneration)
                {
                    return false;
                }

                if (DateTime.UtcNow < accessTokenExpiry && !string.IsNullOrEmpty(accessToken))
                {
                    return true;
                }

                if (DateTime.UtcNow < lastRefreshAttempt || string.IsNullOrEmpty(refreshToken) || clientId.Length == 0)
                {
                    return false;
                }

                stringToEscape = refreshToken;
            }

            return await ExchangeToken("grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(stringToEscape), num).ConfigureAwait(false);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    internal static int GetResponseStatusCode(HttpResponseMessage arg1)
    {
        RetryConditionHeaderValue retryAfter = arg1.Headers.RetryAfter;
        double                    a          = retryAfter?.Delta?.TotalSeconds ?? (retryAfter == null || !retryAfter.Date.HasValue ? 10.0 : (retryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds);

        return (int)Math.Max(1.0, Math.Min(2147483647.0, Math.Ceiling(a)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public HttpRequestMessage CreateAuthorizedRequest(HttpMethod arg1, string arg2, int arg3 = -1)
    {
        HttpRequestMessage httpRequestMessage = new(arg1, arg2);
        lock (stateLock)
        {
            if (arg3 >= 0 && loginGeneration != arg3)
            {
                httpRequestMessage.Dispose();

                throw new InvalidOperationException("Login changed");
            }

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (arg1 != HttpMethod.Get)
        {
            httpRequestMessage.Content = new StringContent("");
        }

        return httpRequestMessage;
    }
}