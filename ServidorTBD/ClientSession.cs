using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace ServidorTBD
{
    public class ClientSession
    {
        public IWebSocketConnection Socket { get; private set; }

        // Estado de la sesión (ejemplo)
        public bool IsAuthenticated { get; set; } = false;
        public string Username { get; set; }
        public string Rol { get; set; }       // <-- Añadir esta propiedad
        public int UserId { get; set; }

        public ClientSession(IWebSocketConnection socket)
        {
            Socket = socket;
        }

        // Puedes agregar métodos para enviar mensajes, cerrar conexión, etc.
        public void SendMessage(string message)
        {
            Socket.Send(message);
        }
    }
}
