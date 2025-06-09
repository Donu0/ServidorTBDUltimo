using System;
using System.Collections.Generic;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Serilog;
using ServidorTBD;

namespace ServidorTBD
{
    public class MessageHandler
    {
        public void HandleMessage(IWebSocketConnection socket, string message)
        {
            try
            {
                // Parsear mensaje JSON
                var json = JObject.Parse(message);

                // Esperamos un campo "action" para identificar qué hacer
                var action = json["accion"]?.ToString();

                if (string.IsNullOrEmpty(action))
                {
                    SendError(socket, "Falta campo 'accion' en el mensaje.");
                    return;
                }

                switch (action.ToLower())
                {
                    case "login":
                        HandleLogin(socket, json);
                        break;

                    case "crear_proyecto":
                        HandleCrearProyecto(socket, json);
                        break;

                    case "getprojects":
                        HandleGetProjects(socket, json);
                        break;

                    case "listar_proyectos_alumno":
                        HandleListarProyectosAlumno(socket, json);
                        break;

                    case "listar_proyectos_asesor":
                        HandleListarProyectosAsesor(socket, json);
                        break;

                    // Agrega más casos según lo que necesites

                    default:
                        SendError(socket, $"Acción desconocida: {action}");
                        break;
                }
            }
            catch (JsonReaderException)
            {
                SendError(socket, "Mensaje JSON inválido.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al manejar mensaje");
                SendError(socket, "Error interno en el servidor.");
            }
        }

        private void HandleLogin(IWebSocketConnection socket, JObject json)
        {
            var usuario = json["datos"]?["usuario"]?.ToString();
            var contrasena = json["datos"]?["contrasena"]?.ToString();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                SendJson(socket, new
                {
                    estado = "login_fail",
                    datos = "Usuario o contraseña faltante"
                });
                return;
            }

            using var db = new Database(Program.connStr);

            var query = @"SELECT id_usuario, nombre_usuario, rol
                          FROM UsuariosSistema
                          WHERE nombre_usuario = :usuario AND contrasena = :contrasena";

            using var cmd = new OracleCommand(query, db._connection);
            cmd.Parameters.Add(new OracleParameter("usuario", usuario));
            cmd.Parameters.Add(new OracleParameter("contrasena", contrasena));

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int idUsuario = reader.GetInt32(0);
                string nombreUsuario = reader.GetString(1);
                string rol = reader.GetString(2);

                if (Program.Clients.TryGetValue(socket, out var session))
                {
                    session.IsAuthenticated = true;
                    session.Username = nombreUsuario;
                    session.Rol = rol;
                    session.UserId = idUsuario;
                }

                SendJson(socket, new
                {
                    estado = "login_ok",
                    datos = new
                    {
                        id_usuario = idUsuario,
                        nombre_usuario = nombreUsuario,
                        rol = rol
                    }
                });
            }
            else
            {
                SendJson(socket, new
                {
                    estado = "login_fail",
                    datos = "Credenciales inválidas"
                });
            }
        }


        private void HandleGetProjects(IWebSocketConnection socket, JObject json)
        {
            // Simulación de datos; reemplazar con llamada real a DB.GetProjects() cuando esté listo.
            var projects = new List<ProjectDto>
            {
                new ProjectDto { Id = 1, Nombre = "Proyecto 1" },
                new ProjectDto { Id = 2, Nombre = "Proyecto 2" }
            };

            var response = new GetProjectsResponse(projects);
            SendJson(socket, response);
        }

        private void HandleCrearProyecto(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol != "ASESOR")
            {
                SendError(socket, "No autorizado para crear proyectos.");
                return;
            }

            var datos = json["datos"];
            var nombre = datos?["nombre"]?.ToString();
            var descripcion = datos?["descripcion"]?.ToString();
            var fechaInicio = datos?["fecha_inicio"]?.ToString();
            var fechaEntrega = datos?["fecha_estimada_entrega"]?.ToString();
            var estatus = datos?["estatus"]?.ToString();
            var idAsesor = session.UserId;

            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(descripcion) ||
                string.IsNullOrWhiteSpace(fechaInicio) || string.IsNullOrWhiteSpace(fechaEntrega) ||
                string.IsNullOrWhiteSpace(estatus))
            {
                SendError(socket, "Todos los campos son obligatorios.");
                return;
            }

            using var db = new Database(Program.connStr);
            using var transaction = db._connection.BeginTransaction();

            try
            {
                var query = @"INSERT INTO Proyectos (nombre, descripcion, fecha_inicio, fecha_estimada_entrega, estatus, id_asesor)
                              VALUES (:nombre, :descripcion, TO_DATE(:fechaInicio, 'YYYY-MM-DD'), TO_DATE(:fechaEntrega, 'YYYY-MM-DD'), :estatus, :idAsesor)";

                using var cmd = new OracleCommand(query, db._connection);
                cmd.Transaction = transaction;

                cmd.Parameters.Add("nombre", nombre);
                cmd.Parameters.Add("descripcion", descripcion);
                cmd.Parameters.Add("fechaInicio", fechaInicio);
                cmd.Parameters.Add("fechaEntrega", fechaEntrega);
                cmd.Parameters.Add("estatus", estatus);
                cmd.Parameters.Add("idAsesor", idAsesor);

                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    transaction.Commit();
                    SendSuccess(socket, "Proyecto creado correctamente.");
                }
                else
                {
                    transaction.Rollback(); 
                    SendError(socket, "Error al crear el proyecto.");
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback(); // Muy importante para evitar que se quede colgada la transacción
                Log.Error(ex, "Error al crear proyecto");
                SendError(socket, "Error interno al crear el proyecto.");
            }
        }

        private void HandleListarProyectosAlumno(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol != "ALUMNO")
            {
                SendError(socket, "No autorizado para ver proyectos de alumno.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                var query = @"SELECT p.id_proyecto, p.nombre, p.descripcion, 
                             TO_CHAR(p.fecha_inicio, 'YYYY-MM-DD') AS fecha_inicio,
                             TO_CHAR(p.fecha_estimada_entrega, 'YYYY-MM-DD') AS fecha_estimada_entrega,
                             p.estatus
                      FROM Proyectos p
                      JOIN ProyectosAlumnos pa ON pa.id_proyecto = p.id_proyecto
                      WHERE pa.id_alumno = :idAlumno";

                using var cmd = new OracleCommand(query, db._connection);
                cmd.Parameters.Add("idAlumno", session.UserId);

                using var reader = cmd.ExecuteReader();

                var proyectos = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    var proyecto = new Dictionary<string, string>
                    {
                        ["id_proyecto"] = reader["id_proyecto"].ToString(),
                        ["nombre"] = reader["nombre"].ToString(),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["fecha_inicio"] = reader["fecha_inicio"].ToString(),
                        ["fecha_estimada_entrega"] = reader["fecha_estimada_entrega"].ToString(),
                        ["estatus"] = reader["estatus"].ToString()
                    };

                    proyectos.Add(proyecto);
                }

                var respuesta = new
                {
                    estado = "exito",
                    datos = proyectos
                };

                SendJson(socket, respuesta);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar proyectos del alumno");
                SendError(socket, "Error interno al listar proyectos.");
            }
        }

        private void HandleListarProyectosAsesor(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver proyectos de asesor.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                // Paso 1: obtener el id_asesor a partir del id_usuario
                string getAsesorIdSql = "SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario";

                using var cmdGetAsesor = new OracleCommand(getAsesorIdSql, db._connection);
                cmdGetAsesor.Parameters.Add("idUsuario", session.UserId);

                object? result = cmdGetAsesor.ExecuteScalar();
                if (result == null)
                {
                    SendError(socket, "No se encontró un asesor vinculado al usuario.");
                    return;
                }

                int idAsesor = Convert.ToInt32(result);

                // Paso 2: buscar los proyectos asociados a ese id_asesor
                var query = @"SELECT id_proyecto, nombre, descripcion, 
                             TO_CHAR(fecha_inicio, 'YYYY-MM-DD') AS fecha_inicio,
                             TO_CHAR(fecha_estimada_entrega, 'YYYY-MM-DD') AS fecha_estimada_entrega,
                             estatus
                      FROM Proyectos
                      WHERE id_asesor = :idAsesor";

                using var cmd = new OracleCommand(query, db._connection);
                cmd.Parameters.Add("idAsesor", idAsesor);

                using var reader = cmd.ExecuteReader();

                var proyectos = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    var proyecto = new Dictionary<string, string>
                    {
                        ["id_proyecto"] = reader["id_proyecto"].ToString(),
                        ["nombre"] = reader["nombre"].ToString(),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["fecha_inicio"] = reader["fecha_inicio"].ToString(),
                        ["fecha_estimada_entrega"] = reader["fecha_estimada_entrega"].ToString(),
                        ["estatus"] = reader["estatus"].ToString()
                    };

                    proyectos.Add(proyecto);
                }

                var respuesta = new
                {
                    estado = "exito",
                    datos = proyectos
                };

                SendJson(socket, respuesta);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar proyectos del asesor");
                SendError(socket, "Error interno al listar proyectos.");
            }
        }



        private void SendError(IWebSocketConnection socket, string errorMessage)
        {
            var error = new ErrorResponse(errorMessage);
            SendJson(socket, error);
        }

        private void SendSuccess(IWebSocketConnection socket, string message)
        {
            var success = new SuccessResponse(message);
            SendJson(socket, success);
        }

        private void SendJson(IWebSocketConnection socket, object obj)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.Indented);
            socket.Send(json);
        }
    }
}
