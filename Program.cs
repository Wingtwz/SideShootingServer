using Serilog;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ServidorSS
{
    class Program
    {
        static void Main(string[] args)
        {
            int[] ports = { 31416, 31417 };
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = null;
            bool end = false;
            StreamReader sr = null;

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
                        string message;
                        Socket client = s.Accept();
                        IPEndPoint epCliente = (IPEndPoint)client.RemoteEndPoint;
                        Log.Information("Cliente {Address}:{Port} conectado", epCliente.Address, epCliente.Port);
                        NetworkStream ns = new NetworkStream(client);
                        sr = new StreamReader(ns);
                        StreamWriter sw = new StreamWriter(ns);

                        sw.WriteLine("ConectadoASideShooting");
                        sw.Flush();

                        message = sr.ReadLine();

                        ns.Close();
                        sr.Close();
                        sw.Close();
                        client.Close();

                        Log.Information("Desconectado el cliente {Address}:{Port}", epCliente.Address, epCliente.Port);
                    }
                    catch (SocketException ex) when (ex.ErrorCode == 10004) { }
                }
            }
            else
            {
                Log.Information("Pulsa una tecla para cerrar");
            }
        }
    }
}
