using FoneSplitHttpService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FoneSplitHttpServer
{
    public class Program
    {
        private static bool _isRunning = true;

        static void Main(string[] args)
        {
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            var server = new HttpServer();
            server.ServerStart();
            _isRunning = true;
            while (_isRunning) { }
        }
    }
}
