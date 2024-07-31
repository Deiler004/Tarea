using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class Mensaje
{
    public string Contenido { get; set; }
    public string Remitente { get; set; }
    public string Destinatario { get; set; }

    public Mensaje(string contenido, string remitente, string destinatario)
    {
        Contenido = contenido;
        Remitente = remitente;
        Destinatario = destinatario;
    }
}

public class ClienteConectado
{
    public string Nombre { get; set; }
    public Socket Socket { get; set; }
    public byte[] Buffer { get; set; }

    public ClienteConectado(string nombre, Socket socket)
    {
        Nombre = nombre;
        Socket = socket;
        Buffer = new byte[socket.ReceiveBufferSize];
    }
}

public class Servidor
{
    private Socket _socket;
    private List<ClienteConectado> _clientes;

    public Servidor()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _clientes = new List<ClienteConectado>();
    }

    public async Task IniciarAsync(string ip, int puerto)
    {
        _socket.Bind(new IPEndPoint(IPAddress.Parse(ip), puerto));
        _socket.Listen(10);
        Console.WriteLine("Servidor iniciado y esperando clientes...");

        while (true)
        {
            Socket clienteSocket = await _socket.AcceptAsync();
            Console.WriteLine("Cliente conectado.");
            ClienteConectado clienteConectado = new ClienteConectado("", clienteSocket);
            _clientes.Add(clienteConectado);
            RecibirMensajes(clienteConectado);
        }
    }

    private void RecibirMensajes(ClienteConectado cliente)
    {
        cliente.Socket.BeginReceive(cliente.Buffer, 0, cliente.Buffer.Length, SocketFlags.None, async ar =>
        {
            try
            {
                int received = cliente.Socket.EndReceive(ar);
                if (received > 0)
                {
                    string text = Encoding.ASCII.GetString(cliente.Buffer, 0, received);
                    if (string.IsNullOrEmpty(cliente.Nombre))
                    {
                        cliente.Nombre = text;
                        Console.WriteLine($"Cliente {cliente.Nombre} registrado.");
                    }
                    else
                    {
                        string[] partes = text.Split(':');
                        if (partes.Length == 2)
                        {
                            string destinatario = partes[0];
                            string contenido = partes[1];

                            if (destinatario.ToLower() == "salir")
                            {
                                cliente.Socket.Shutdown(SocketShutdown.Both);
                                cliente.Socket.Close();
                                _clientes.Remove(cliente);
                                Console.WriteLine($"Cliente {cliente.Nombre} desconectado.");
                                return;
                            }

                            Mensaje mensaje = new Mensaje(contenido, cliente.Nombre, destinatario);
                            await EnviarMensajeAsync(mensaje);
                        }
                    }
                    RecibirMensajes(cliente);
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Error de socket: {ex.Message}");
                cliente.Socket.Close();
                _clientes.Remove(cliente);
            }
        }, null);
    }

    public async Task EnviarMensajeAsync(Mensaje mensaje)
    {
        ClienteConectado destinatario = _clientes.Find(c => c.Nombre == mensaje.Destinatario);
        if (destinatario != null)
        {
            byte[] data = Encoding.ASCII.GetBytes($"{mensaje.Remitente}: {mensaje.Contenido}");
            await destinatario.Socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
        }
    }
}

public class Cliente
{
    private Socket _socket;
    private byte[] _buffer;
    public string Nombre { get; private set; }

    public Cliente(string nombre)
    {
        Nombre = nombre;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }

    public async Task ConectarAsync(string ip, int puerto)
    {
        await _socket.ConnectAsync(new IPEndPoint(IPAddress.Parse(ip), puerto));
        _buffer = new byte[_socket.ReceiveBufferSize];
        await EnviarMensajeAsync(Nombre); // Enviar el nombre al servidor
        RecibirMensajes();
    }

    private void RecibirMensajes()
    {
        _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, RecibirCallback, null);
    }

    private void RecibirCallback(IAsyncResult ar)
    {
        try
        {
            int received = _socket.EndReceive(ar);
            if (received > 0)
            {
                string text = Encoding.ASCII.GetString(_buffer, 0, received);
                Console.WriteLine(text);
                _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, RecibirCallback, null);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"Error de socket: {ex.Message}");
            _socket.Close();
        }
    }

    public async Task EnviarMensajeAsync(string mensaje)
    {
        byte[] data = Encoding.ASCII.GetBytes(mensaje);
        await _socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        Console.Write("¿Desea iniciar como servidor (s) o cliente (c)? ");
        string tipo = Console.ReadLine();

        if (tipo.ToLower() == "s")
        {
            await IniciarServidor();
        }
        else if (tipo.ToLower() == "c")
        {
            await IniciarCliente();
        }
        else
        {
            Console.WriteLine("Opción no válida.");
        }
    }

    private static async Task IniciarServidor()
    {
        Servidor servidor = new Servidor();

        Console.Write("Ingrese la IP del servidor: ");
        string ip = Console.ReadLine();

        Console.Write("Ingrese el puerto del servidor: ");
        int puerto = int.Parse(Console.ReadLine());

        await servidor.IniciarAsync(ip, puerto);
    }

    private static async Task IniciarCliente()
    {
        Console.Write("Ingrese su nombre: ");
        string nombre = Console.ReadLine();

        Cliente cliente = new Cliente(nombre);

        Console.Write("Ingrese la IP del servidor: ");
        string ip = Console.ReadLine();

        Console.Write("Ingrese el puerto del servidor: ");
        int puerto = int.Parse(Console.ReadLine());

        await cliente.ConectarAsync(ip, puerto);
        Console.WriteLine("Conectado al servidor.");

        while (true)
        {
            string entrada = Console.ReadLine();
            await cliente.EnviarMensajeAsync(entrada);
        }
    }
}



