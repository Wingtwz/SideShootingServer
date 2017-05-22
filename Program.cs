using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Serilog;

namespace ServidorSS
{
    /// <summary>
    /// Servidor que gestiona las conexiones asociadas al juego SideShooting
    /// </summary>
    class Program
    {
        /// <summary>
        /// Número máximo de clientes conectados simultáneamente
        /// </summary>
        private const int MaxConnections = 2;

        /// <summary>
        /// Objeto para evitar errores de concurrencia
        /// </summary>
        private static readonly object l = new object ();
        /// <summary>
        /// Indica si el servidor debe cerrarse
        /// </summary>
        private static bool end;
        /// <summary>
        /// Colección para la comunicación con los clientes conectados
        /// </summary>
        private static List<StreamWriter> swClients;

        /// <summary>
        /// Método principal de arranque del servidor
        /// </summary>
        /// <param name="args">Argumentos pasados para su inicio, no usados</param>
        static void Main(string[] args)
        {
            Console.Title = "Servidor SideShooting";
            int[] ports = { 31416, 31417 };
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = null;
            swClients = new List<StreamWriter>();
            end = false;

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

        /// <summary>
        /// Gestiona el intercambio de conexión con un cliente conectado al servidor
        /// </summary>
        /// <param name="socket"><see cref="Socket"/> de la conexión de un cliente</param>
        private static void NewClient(object socket)
        {
            bool disconnect = false, gameReady;
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

            gameReady = swClients.Count > 1;

            sw.WriteLine(gameReady ? "READY" : "WAIT");
            sw.Flush();

            if (gameReady)
            {
                lock (l)
                {
                    foreach (StreamWriter swClient in swClients)
                    {
                        if (sw != swClient)
                        {
                            swClient.WriteLine("GO");
                            swClient.Flush();
                        }
                    }
                }
            }

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
                            lock (l)
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
                    }
                    else
                        break;
                }
                catch (IOException ex)
                {
                    Log.Error(ex.Message);
                    lock (l)
                    {
                        foreach (StreamWriter swClient in swClients)
                        {
                            try
                            {
                                if (sw != swClient)
                                {
                                    swClient.WriteLine("VICTORY");
                                    swClient.Flush();
                                }
                            }
                            catch (IOException)
                            {
                                //Simplemente que pase al siguiente
                            }
                        }
                    }
                    break;
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    Log.Error(ex.Message);
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
