using Fleck;
using Newtonsoft.Json;
using Serilog;
using Serilog.Sinks.SystemConsole;
using ServidorTBD;
using System;
using System.Collections.Generic;
using System.IO;

namespace ServidorTBD
{
    class Program
    {
        public static Dictionary<IWebSocketConnection, ClientSession> Clients = new();

        public static string connStr =
            "User Id=protrack_user;" +
            "Password=Password1234;" +
            "Data Source=tbdej2025_high;" +
            @"TNS_ADMIN=C:\Wallet_TBDEJ2025;";

        static void Main(string[] args)
        {
            // Leer configuración
            var config = Config.Load("config.json");

            // Configurar Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/server_log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Servidor ProTrack iniciando...");

            FleckLog.Level = LogLevel.Warn;

            var server = new WebSocketServer($"ws://{config.IP}:{config.Port}");
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Log.Information($"Cliente conectado: {socket.ConnectionInfo.ClientIpAddress}");
                    Clients[socket] = new ClientSession(socket);
                };

                socket.OnClose = () =>
                {
                    Log.Information($"Cliente desconectado: {socket.ConnectionInfo.ClientIpAddress}");
                    Clients.Remove(socket);
                };

                socket.OnMessage = message =>
                {
                    Log.Information($"Mensaje recibido: {message}");
                    var handler = new MessageHandler();
                    handler.HandleMessage(socket, message);
                };
            });


            

            using var db = new Database(connStr);

            //if (db.TestConnection())
            //{
            //    Console.WriteLine("Conexión a Oracle OK");
            //}
            //else
            //{
            //    Console.WriteLine("Error al conectar a Oracle");
            //}

            Console.WriteLine("Presiona Enter para detener el servidor...");
            Console.ReadLine();
            db.Dispose();
        }
    }
}
