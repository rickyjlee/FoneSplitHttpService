using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FoneSplitHttpService
{
    public class HttpServer
    {
        HttpListener _httpListener;
        FoneSplitService _foneSplitService;
        HttpListenerContext context = null;

        public HttpServer()
        {
            serverInit();
            _foneSplitService = new FoneSplitService();
        }

        private void serverInit()
        {
            if (_httpListener == null)
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add(string.Format("http://127.0.0.1:8686/"));
            }
        }

        private void splitEventToResponse()
        {
            _foneSplitService.FoneSplitProcessEvent += (sender, args) => {
                var result = true;
                var statusCode = 200;
                var messageBuffer = new StringBuilder();
                var splitBuffers = args.Buffers;
                JArray jarray = new JArray();

                messageBuffer.Append("{ \"result\": \"" + result + "\",");
                messageBuffer.Append(" \"buffers\": [");
                for (int i = 0; i < splitBuffers.Count; i++)
                {
                    var bufStr = Convert.ToBase64String(splitBuffers[i]);
                    messageBuffer.Append("\"" + bufStr + "\"");
                    if (i < splitBuffers.Count - 1)
                        messageBuffer.Append(",");
                    else
                        messageBuffer.Append("]}");
                }

                if (context != null)
                    FormatJsonResponse(context.Response, statusCode, messageBuffer.ToString());
            };

            _foneSplitService.FoneSplitStopEvent += (sender, args) => {
                var result = false;
                var statusCode = 200;
                var messageBuffer = new StringBuilder();
                result = args.IsSuccess;
                messageBuffer.Append("{ \"result\": \"" + result + "\"}");
                if (!result)
                    statusCode = 405;

                if (context != null)
                    FormatJsonResponse(context.Response, statusCode, messageBuffer.ToString());
            };

        }

        public void ServerStart()
        {

            if (!_httpListener.IsListening)
            {
                _httpListener.IgnoreWriteExceptions = true;
                //_httpListener.Prefixes.Add("http://*:8686/");
                _httpListener.Start();
                WriteLogs("FoneSplit Server Start!!");

                Task.Run(() =>
                {
                    splitEventToResponse();
                    while (_httpListener != null)
                    {
                        context = this._httpListener.GetContext();

                        string rawurl = context.Request.RawUrl;
                        string httpmethod = context.Request.HttpMethod;
                        string param = "";
                        context.Response.AppendHeader("Access-Control-Allow-Origin", "*");
                        //param += string.Format("httpmethod = {0}\r\n", httpmethod);
                        //param += string.Format("rawurl = {0}\r\n", rawurl);

                        if (context.Request.HttpMethod == HttpMethod.Post.Method)
                        {
                            // body 데이터를 json 으로 받아서 Parsing 
                            using (var reader = new StreamReader(context.Request.InputStream,
                                     context.Request.ContentEncoding))
                            {
                                param += reader.ReadToEnd();
                            }
                            HttpPostController(context, param);

                        }
                        else if (context.Request.HttpMethod == HttpMethod.Get.Method)
                        {
                        }

                    }
                });
               /*
                FoneSplitNative.NativeStart(_foneProc, _deviceWaveFormat.Channels);
                FoneSplitNative.NativeRegisterCallBack(_foneProc, FoneSplitCallBackFunc);  //화자분리 콜백 버전
                FoneSplitNative.NativeProcess(_foneProc, IntPtr.Zero, 0, 1);
                FoneSplitNative.NativeComplete(foneProc);
                FoneSplitNative.NativeDestroyFoneMultiChanProc(foneProc);
                FoneSplitNative.NativeGetOutputMultiChanData(_foneProc, BSSFramesSize, foneProcCB);*/
            }
        }

        public void HttpPostController(HttpListenerContext context, string param)
        {
            JObject json = JObject.Parse(param);
            var messageBuffer = new StringBuilder();
            var statusCode = 200;
            var result = false;
            WriteLogs("req >> " + context.Request.Url.AbsolutePath + " " + param);
            switch (context.Request.Url.AbsolutePath)
            {
                case "/init":
                    var channelCnt = json["channel_count"];
                    if (channelCnt != null)
                    {
                        result = _foneSplitService.InitializeFoneSplit(Int32.Parse(channelCnt.ToString()));
                        messageBuffer.Append("{ \"result\": \"" + result + "\"}");
                        if (!result)
                            statusCode = 401;
                        FormatJsonResponse(context.Response, statusCode, messageBuffer.ToString());
                    }
                    break;

                case "/start":
                    result = _foneSplitService.StartRecording();
                    messageBuffer.Append("{ \"result\": \"" + result + "\"}");
                    if (!result)
                        statusCode = 405;
                    FormatJsonResponse(context.Response, statusCode, messageBuffer.ToString());
                    break;

                case "/stop":
                    result = _foneSplitService.StopRecording();
                    break;

                case "/process":
                    var buffer = json["buffer"].ToString();
                    if (buffer != null)
                    {
                        byte[] decodedBytes = Convert.FromBase64String(buffer);
                        _foneSplitService.SendBuffer(decodedBytes);
                    }
                    break;

                case "/destroy":
                    _foneSplitService.FinalizeFoneSplit();                    
                    break;

                default:
                    statusCode = 404;
                    FormatJsonResponse(context.Response, statusCode, messageBuffer.ToString());
                    break;
            }
        }

        private void FormatJsonResponse(HttpListenerResponse response, int statusCode, string jsonString)
        {
            try
            {
                response.ContentType = "application/json";
                response.StatusCode = statusCode;
                response.StatusDescription = "OK";
                if (statusCode != 200)
                {
                    response.StatusDescription = "Failed";
                }          

                byte[] binaryPayload = Encoding.UTF8.GetBytes(jsonString);
                response.ContentLength64 = binaryPayload.LongLength;
                response.OutputStream.Write(binaryPayload, 0, binaryPayload.Length);
                WriteLogs("res >> " + jsonString);
            }
            catch(Exception e)
            {
                WriteLogs(e.Message);
            }
            finally
            {
                response.Close();
            }
        }

        /*public void HttpGetController(HttpListenerContext context)
        {
            switch(context.Request.Url.ToString())
            {
                case "/init":
                    break;

                case "/start":
                    break;

                case "/stop":
                    break;

                case "/process":
                    break;

                case "/destroy":
                    break;
            }
            //context.
        }*/
        public void WriteLogs(string msg)
        {
            string datetime = DateTime.Now.ToString("[yyyy-MM-dd HH:mm:dd:FFF]");
            Console.WriteLine(datetime + " : " + msg);
        }
    }
}
