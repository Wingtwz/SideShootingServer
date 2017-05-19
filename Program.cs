using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace ServidorSS
{
    class Program
    {
        private const int MaxConnections = 2;

        private static readonly object l = new object();
        private static bool end = false;
        private static List<StreamWriter> swClients = new List<StreamWriter>();

        static void Main(string[] args)
        {
            int[] ports = { 31416, 31417 };
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = null;

            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .WriteTo.RollingFile("log-{Date}.log")
                .CreateLogger();

            if (!end)
            {
                end = true; //Lo cambio ya que lo uso como condicion en el bucle para asegurarme que al menos pasa una vez
                for (int i = 0; end && i < ports.Length; i++)
                {
                    try
                    {
                        ep = new IPEndPoint(IPAddress.Any, ports[i]);
                        s.Bind(ep);
                        end = false;
                    }
                    catch (SocketException ex) when (ex.ErrorCode == 10048)
                    {
                        Log.Error("Puerto {0} ocupado", ports[i]);
                        end = true;
                    }
                }
            }

            if (!end)
            {
                s.Listen(2);
                Log.Information("A la escucha en el puerto {Port}", ep.Port);

                while (!end)
                {
                    try
                    {
                        Socket client = s.Accept();
                        if (swClients.Count >= MaxConnections)
                        {
                            client.Close();
                        }
                        else
                        {
                            Thread thread = new Thread(NewClient);
                            thread.Start(client);
                        }
                    }
                    catch (SocketException ex) when (ex.ErrorCode == 10004)
                    {
                        //logear el error
                    }
                }
            }
            else
            {
                Log.Information("Pulsa una tecla para cerrar");
            }
        }

        private static void NewClient(object socket)
        {
            bool disconnect = false;
            string message;
            Socket client = (Socket)socket;
            IPEndPoint epCliente = (IPEndPoint)client.RemoteEndPoint;
            Log.Information("Cliente {Address}:{Port} conectado", epCliente.Address, epCliente.Port);
            NetworkStream ns = new NetworkStream(client);
            StreamReader sr = new StreamReader(ns);
            StreamWriter sw = new StreamWriter(ns);

            sw.WriteLine("ConectadoASideShooting");
            sw.Flush();

            if (sr.ReadLine() != "OKSS")
            {
                disconnect = true;
            }

            lock (l)
            {
                swClients.Add(sw);
            }

            Log.Information($"Conectados {swClients.Count} clientes");

            while (!disconnect)
            {
                try
                {
                    message = sr.ReadLine();
                    //Log.Information($"{epCliente.Address}:{epCliente.Port} envía: {message}");
                    if (message != null)
                    {
                        if (message == "SALIR")
                        {
                            disconnect = true;
                        }
                        else// if (message.Split(' ')[0] == "LOCATION")
                        {
                            foreach (StreamWriter swClient in swClients)
                            {
                                if (sw != swClient)
                                {
                                    swClient.WriteLine(message);
                                    swClient.Flush();
                                }
                            }
                        }
                    }
                    else
                        break;
                }
                catch (IOException)
                {
                    break;
                }
            }

            ns.Close();
            sr.Close();
            sw.Close();
            client.Close();

            lock (l)
            {
                swClients.Remove(sw);
            }

            Log.Information("Desconectado el cliente {Address}:{Port}", epCliente.Address, epCliente.Port);
            Log.Information($"{swClients.Count} cliente/s todavía conectado/s");
        }
    }
}
