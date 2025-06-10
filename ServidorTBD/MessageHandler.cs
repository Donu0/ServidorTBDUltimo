using System;
using System.Collections.Generic;
using System.Data;
using System.Transactions;
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

                    case "auditoria_logout":
                        HandleAuditoriaLogout(socket, json);
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

                    case "listar_avances":
                        HandleListarAvances(socket, json);
                        break;

                    case "listar_entregas":
                        HandleListarEntregas(socket, json);
                        break;

                    case "listar_estudiantes":
                        HandleListarEstudiantes(socket, json);
                        break;
                    case "insertar_avance":
                        HandleInsertarAvance(socket, json);
                        break;

                    case "actualizar_avance":
                        HandleActualizarAvance(socket, json);
                        break;

                    case "insertar_entrega":
                        HandleInsertarEntrega(socket, json);
                        break;

                    case "actualizar_entrega":
                        HandleActualizarEntrega(socket, json);
                        break;

                    case "actualizar_estudiante":
                        HandleActualizarEstudiante(socket, json);
                        break;

                    case "asignar_proyecto_estudiante":
                        HandleAsignarProyectoEstudiante(socket, json);
                        break;

                    case "insertar_estudiante":
                        HandleInsertarEstudiante(socket, json);
                        break;

                    case "insertar_asesor":
                        HandleInsertarAsesor(socket, json);
                        break;
                    case "reporte_avances_proyecto":
                        HandleListarAvancesPorProyecto(socket, json);
                        break;

                    case "reporte_entregas_proximas":
                        HandleListarEntregasProximas(socket, json);
                        break;

                    case "reporte_proyectos_sin_avances":
                        HandleListarProyectosSinAvancesRecientes(socket, json);
                        break;

                    case "proyecto_asesor":
                        HandleListarProyectosAsesorCMB(socket, json);
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

                using var auditCmd = new OracleCommand(@"INSERT INTO AuditoriaSistema (usuario, accion, fecha, descripcion, id_proyecto)
                                                         VALUES (:usuario, :accion, CURRENT_TIMESTAMP AT TIME ZONE 'America/Mexico_City', :descripcion, NULL)", db._connection);

                auditCmd.Parameters.Add(new OracleParameter("usuario", nombreUsuario));
                auditCmd.Parameters.Add(new OracleParameter("accion", "login"));
                auditCmd.Parameters.Add(new OracleParameter("descripcion", $"El usuario '{nombreUsuario}' inició sesión correctamente."));

                auditCmd.ExecuteNonQuery();

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

        private void HandleAuditoriaLogout(IWebSocketConnection socket, JObject json)
        {
            var usuario = json["datos"]?["usuario"]?.ToString();

            using var db = new Database(Program.connStr);
            var query = "INSERT INTO AuditoriaSistema (usuario, accion, fecha, descripcion) VALUES (:usuario, :accion, CURRENT_TIMESTAMP AT TIME ZONE 'America/Mexico_City', :descripcion)";
            using var cmd = new OracleCommand(query, db._connection);
            cmd.Parameters.Add(new OracleParameter("usuario", usuario));
            cmd.Parameters.Add(new OracleParameter("accion", "logout"));
            cmd.Parameters.Add(new OracleParameter("descripcion", $"El usuario '{usuario}' cerró sesión correctamente."));
            cmd.ExecuteNonQuery();
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
                // Establecer el usuario lógico para auditoría
                using (var cmdCtx = new OracleCommand("BEGIN pkg_ctx_usuario.set_usuario(:usuario); END;", db._connection))
                {
                    cmdCtx.Transaction = transaction;
                    cmdCtx.Parameters.Add("usuario", OracleDbType.Varchar2).Value = session.Username; // Ej: "Ulloa"
                    cmdCtx.ExecuteNonQuery();
                }

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
                transaction.Rollback();
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

        private void HandleAsignarProyectoEstudiante(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para asignar proyectos.");
                return;
            }

            if (!json.TryGetValue("datos", out var datosToken))
            {
                SendError(socket, "Faltan los datos para asignar.");
                return;
            }

            var datos = (JObject)datosToken;

            int idEstudiante = datos.Value<int>("id_estudiante");
            int idProyecto = datos.Value<int>("id_proyecto");

            using var db = new Database(Program.connStr);
            try
            {
                string sql = @"INSERT INTO Estudiantes_Proyectos (id_estudiante, id_proyecto)
                               VALUES (:idEstudiante, :idProyecto)";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idEstudiante", idEstudiante);
                cmd.Parameters.Add("idProyecto", idProyecto);

                cmd.ExecuteNonQuery();

                SendSuccess(socket, "Proyecto asignado al estudiante correctamente.");
            }
            catch (OracleException ex) when (ex.Number == 1) // Violación de PK = ya existe
            {
                SendError(socket, "Este estudiante ya está asignado a este proyecto.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al asignar proyecto al estudiante.");
                SendError(socket, "Error interno al asignar el proyecto.");
            }
        }

        private void HandleListarAvances(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver avances.");
                return;
            }

            if (json["id_proyecto"] == null)
            {
                SendError(socket, "Falta el ID del proyecto.");
                return;
            }

            int idProyecto = json["id_proyecto"].Value<int>();

            using var db = new Database(Program.connStr);

            try
            {
                // Verificar que el proyecto pertenezca al asesor
                var validarSql = @"SELECT COUNT(*) FROM Proyectos WHERE id_proyecto = :idProyecto AND id_asesor = 
                          (SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario)";
                using var cmdValidar = new OracleCommand(validarSql, db._connection);
                cmdValidar.Parameters.Add("idProyecto", idProyecto);
                cmdValidar.Parameters.Add("idUsuario", session.UserId);

                if (Convert.ToInt32(cmdValidar.ExecuteScalar()) == 0)
                {
                    SendError(socket, "No autorizado para ver avances de este proyecto.");
                    return;
                }

                var sql = @"SELECT id_avance, descripcion, 
                           TO_CHAR(fecha_registro, 'YYYY-MM-DD') AS fecha_registro, 
                           porcentaje_completado 
                    FROM Avances WHERE id_proyecto = :idProyecto";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idProyecto", idProyecto);

                using var reader = cmd.ExecuteReader();
                var avances = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    avances.Add(new Dictionary<string, string>
                    {
                        ["id_avance"] = reader["id_avance"].ToString(),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["fecha_registro"] = reader["fecha_registro"].ToString(),
                        ["porcentaje_completado"] = reader["porcentaje_completado"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = avances });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar avances");
                SendError(socket, "Error interno al listar avances.");
            }
        }

        private void HandleListarEntregas(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver entregas.");
                return;
            }

            if (json["id_proyecto"] == null)
            {
                SendError(socket, "Falta el ID del proyecto.");
                return;
            }

            int idProyecto = json["id_proyecto"].Value<int>();

            using var db = new Database(Program.connStr);

            try
            {
                var validarSql = @"SELECT COUNT(*) FROM Proyectos WHERE id_proyecto = :idProyecto AND id_asesor = 
                          (SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario)";
                using var cmdValidar = new OracleCommand(validarSql, db._connection);
                cmdValidar.Parameters.Add("idProyecto", idProyecto);
                cmdValidar.Parameters.Add("idUsuario", session.UserId);

                if (Convert.ToInt32(cmdValidar.ExecuteScalar()) == 0)
                {
                    SendError(socket, "No autorizado para ver entregas de este proyecto.");
                    return;
                }

                var sql = @"SELECT id_entrega, nombre_entrega, 
                           TO_CHAR(fecha_programada, 'YYYY-MM-DD') AS fecha_programada,
                           TO_CHAR(fecha_real, 'YYYY-MM-DD') AS fecha_real,
                           estatus 
                    FROM Entregas WHERE id_proyecto = :idProyecto";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idProyecto", idProyecto);

                using var reader = cmd.ExecuteReader();
                var entregas = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    entregas.Add(new Dictionary<string, string>
                    {
                        ["id_entrega"] = reader["id_entrega"].ToString(),
                        ["nombre_entrega"] = reader["nombre_entrega"].ToString(),
                        ["fecha_programada"] = reader["fecha_programada"].ToString(),
                        ["fecha_real"] = reader["fecha_real"]?.ToString() ?? "",
                        ["estatus"] = reader["estatus"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = entregas });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar entregas");
                SendError(socket, "Error interno al listar entregas.");
            }
        }

        private void HandleListarProyectosAsesorCMB(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver proyectos del asesor.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                // Obtener ID del asesor asociado al usuario actual
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

                // Obtener solo id_proyecto y nombre
                string sql = @"SELECT id_proyecto, nombre
                       FROM Proyectos
                       WHERE id_asesor = :idAsesor";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idAsesor", idAsesor);

                using var reader = cmd.ExecuteReader();
                var proyectos = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    proyectos.Add(new Dictionary<string, string>
                    {
                        ["id_proyecto"] = reader["id_proyecto"].ToString(),
                        ["nombre"] = reader["nombre"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = proyectos });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar proyectos para ComboBox");
                SendError(socket, "Error interno al obtener proyectos.");
            }
        }

        private void HandleListarAvancesPorProyecto(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver avances.");
                return;
            }

            if (json["id_proyecto"] == null)
            {
                SendError(socket, "Falta el ID del proyecto.");
                return;
            }

            int idProyecto = json["id_proyecto"].Value<int>();

            using var db = new Database(Program.connStr);

            try
            {
                var validarSql = @"SELECT COUNT(*) FROM Proyectos WHERE id_proyecto = :idProyecto AND id_asesor = 
                          (SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario)";
                using var cmdValidar = new OracleCommand(validarSql, db._connection);
                cmdValidar.Parameters.Add("idProyecto", idProyecto);
                cmdValidar.Parameters.Add("idUsuario", session.UserId);

                if (Convert.ToInt32(cmdValidar.ExecuteScalar()) == 0)
                {
                    SendError(socket, "No autorizado para ver avances de este proyecto.");
                    return;
                }

                var sql = @"SELECT id_avance, descripcion, 
                           TO_CHAR(fecha_registro, 'YYYY-MM-DD') AS fecha, 
                           porcentaje_completado
                    FROM Avances
                    WHERE id_proyecto = :idProyecto
                    ORDER BY fecha_registro DESC";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idProyecto", idProyecto);

                using var reader = cmd.ExecuteReader();
                var avances = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    avances.Add(new Dictionary<string, string>
                    {
                        ["id_avance"] = reader["id_avance"].ToString(),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["fecha"] = reader["fecha"].ToString(),
                        ["porcentaje_completado"] = reader["porcentaje_completado"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = avances });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar avances");
                SendError(socket, "Error interno al listar avances.");
            }
        }

        private void HandleListarEntregasProximas(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver entregas próximas.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                var sql = @"SELECT nombre_entrega, 
                           TO_CHAR(fecha_programada, 'YYYY-MM-DD') AS fecha_programada, 
                           estatus 
                    FROM Entregas 
                    WHERE fecha_programada <= SYSDATE + 7 
                    ORDER BY fecha_programada";

                using var cmd = new OracleCommand(sql, db._connection);
                using var reader = cmd.ExecuteReader();
                var entregas = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    entregas.Add(new Dictionary<string, string>
                    {
                        ["nombre_entrega"] = reader["nombre_entrega"].ToString(),
                        ["fecha_programada"] = reader["fecha_programada"].ToString(),
                        ["estatus"] = reader["estatus"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = entregas });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar entregas próximas");
                SendError(socket, "Error interno al listar entregas próximas.");
            }
        }

        private void HandleListarProyectosSinAvancesRecientes(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver proyectos.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                var sql = @"SELECT p.id_proyecto, p.nombre, p.descripcion, p.estatus
                    FROM Proyectos p
                    WHERE p.id_asesor = (SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario)
                      AND NOT EXISTS (
                          SELECT 1 FROM Avances a
                          WHERE a.id_proyecto = p.id_proyecto
                            AND a.fecha_registro >= SYSDATE - 14
                      )";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idUsuario", session.UserId);
                using var reader = cmd.ExecuteReader();
                var proyectos = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    proyectos.Add(new Dictionary<string, string>
                    {
                        ["id_proyecto"] = reader["id_proyecto"].ToString(),
                        ["nombre"] = reader["nombre"].ToString(),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["estatus"] = reader["estatus"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = proyectos });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar proyectos sin avances recientes");
                SendError(socket, "Error interno al listar proyectos.");
            }
        }


        private void HandleListarEstudiantes(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver estudiantes.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                var sql = @"SELECT id_estudiante, nombre, carrera, semestre, correo 
                    FROM Estudiantes";

                using var cmd = new OracleCommand(sql, db._connection);
                using var reader = cmd.ExecuteReader();

                var estudiantes = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    estudiantes.Add(new Dictionary<string, string>
                    {
                        ["id_estudiante"] = reader["id_estudiante"].ToString(),
                        ["nombre"] = reader["nombre"].ToString(),
                        ["carrera"] = reader["carrera"].ToString(),
                        ["semestre"] = reader["semestre"].ToString(),
                        ["correo"] = reader["correo"].ToString()
                    });
                }

                SendJson(socket, new { estado = "exito", datos = estudiantes });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar estudiantes");
                SendError(socket, "Error al listar estudiantes.");
            }
        }

        private void HandleInsertarEstudiante(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para insertar estudiantes.");
                return;
            }

            string nombreUsuario = json["nombre_usuario"]?.ToString();
            string contrasena = json["contrasena"]?.ToString();
            string nombre = json["nombre"]?.ToString();
            string carrera = json["carrera"]?.ToString();
            int semestre = json["semestre"]?.Value<int>() ?? 1;
            string correo = json["correo"]?.ToString();

            if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrWhiteSpace(contrasena) ||
                string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(carrera) ||
                string.IsNullOrWhiteSpace(correo) || semestre < 1)
            {
                SendError(socket, "Datos incompletos o inválidos.");
                return;
            }

            OracleTransaction transaction = null;

            try
            {

                using var db = new Database(Program.connStr);
                if (db._connection.State != ConnectionState.Open)
                    db._connection.Open();

                transaction = db._connection.BeginTransaction();

                // Establecer el usuario lógico para auditoría
                using (var cmdCtx = new OracleCommand("BEGIN pkg_ctx_usuario.set_usuario(:usuario); END;", db._connection))
                {
                    cmdCtx.Transaction = transaction;
                    cmdCtx.Parameters.Add("usuario", OracleDbType.Varchar2).Value = session.Username; // Ej: "Ulloa"
                    cmdCtx.ExecuteNonQuery();
                }

                string sqlUsuario = @"INSERT INTO UsuariosSistema (nombre_usuario, contrasena, rol)
                             VALUES (:nombre_usuario, :contrasena, 'estudiante')
                             RETURNING id_usuario INTO :id_usuario";

                using var cmdUsuario = new OracleCommand(sqlUsuario, db._connection)
                {
                    Transaction = transaction
                };

                cmdUsuario.Parameters.Add("nombre_usuario", OracleDbType.Varchar2, 50).Value = nombreUsuario;
                cmdUsuario.Parameters.Add("contrasena", OracleDbType.Varchar2, 50).Value = contrasena;
                cmdUsuario.Parameters.Add("id_usuario", OracleDbType.Int32).Direction = ParameterDirection.Output;

                cmdUsuario.ExecuteNonQuery();

                var oracleDecimal = (Oracle.ManagedDataAccess.Types.OracleDecimal)cmdUsuario.Parameters["id_usuario"].Value;
                int idUsuario = oracleDecimal.ToInt32();

                string sqlEstudiante = @"INSERT INTO Estudiantes (nombre, carrera, semestre, correo, id_usuario)
                                 VALUES (:nombre, :carrera, :semestre, :correo, :id_usuario)";

                using var cmdEst = new OracleCommand(sqlEstudiante, db._connection)
                {
                    Transaction = transaction
                };
                cmdEst.Parameters.Add("nombre", OracleDbType.Varchar2, 100).Value = nombre;
                cmdEst.Parameters.Add("carrera", OracleDbType.Varchar2, 100).Value = carrera;
                cmdEst.Parameters.Add("semestre", OracleDbType.Int32).Value = semestre;
                cmdEst.Parameters.Add("correo", OracleDbType.Varchar2, 100).Value = correo;
                cmdEst.Parameters.Add("id_usuario", OracleDbType.Int32).Value = idUsuario;

                cmdEst.ExecuteNonQuery();

                transaction.Commit();
                SendSuccess(socket, "Alumno creado correctamente.");
            }
            catch (Exception ex)
            {
                try { transaction?.Rollback(); } catch { }
                Log.Error(ex, "Error al insertar estudiante");
                SendError(socket, "Error al insertar estudiante: " + ex.Message);
            }
        }

        private void HandleInsertarAsesor(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ADMIN")
            {
                SendError(socket, "No autorizado para insertar asesores.");
                return;
            }

            try
            {
                string nombreUsuario = json["nombre_usuario"]?.ToString();
                string contrasena = json["contrasena"]?.ToString();
                string nombre = json["nombre"]?.ToString();
                string departamento = json["departamento"]?.ToString();
                string correo = json["correo"]?.ToString();

                using var db = new Database(Program.connStr);

                using var transaction = db._connection.BeginTransaction();

                // 1. Insertar en UsuariosSistema
                string sqlUsuario = @"INSERT INTO UsuariosSistema (nombre_usuario, contrasena, rol)
                              VALUES (:nombre_usuario, :contrasena, 'asesor')
                              RETURNING id_usuario INTO :id_usuario";

                using var cmdUsuario = new OracleCommand(sqlUsuario, db._connection);
                cmdUsuario.Transaction = transaction;
                cmdUsuario.Parameters.Add("nombre_usuario", OracleDbType.Varchar2).Value = nombreUsuario;
                cmdUsuario.Parameters.Add("contrasena", OracleDbType.Varchar2).Value = contrasena;
                cmdUsuario.Parameters.Add("id_usuario", OracleDbType.Int32).Direction = ParameterDirection.Output;

                cmdUsuario.ExecuteNonQuery();
                int idUsuario = Convert.ToInt32(cmdUsuario.Parameters["id_usuario"].Value);

                // 2. Insertar en Asesores
                string sqlAsesor = @"INSERT INTO Asesores (nombre, departamento, correo, id_usuario)
                             VALUES (:nombre, :departamento, :correo, :id_usuario)";

                using var cmdAsesor = new OracleCommand(sqlAsesor, db._connection);
                cmdAsesor.Transaction = transaction;
                cmdAsesor.Parameters.Add("nombre", OracleDbType.Varchar2).Value = nombre;
                cmdAsesor.Parameters.Add("departamento", OracleDbType.Varchar2).Value = departamento;
                cmdAsesor.Parameters.Add("correo", OracleDbType.Varchar2).Value = correo;
                cmdAsesor.Parameters.Add("id_usuario", OracleDbType.Int32).Value = idUsuario;

                cmdAsesor.ExecuteNonQuery();

                transaction.Commit();

                SendJson(socket, new { estado = "exito", mensaje = "Asesor registrado correctamente." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al insertar asesor");
                SendError(socket, "Error al insertar asesor.");
            }
        }



        private void HandleInsertarAvance(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para insertar avances.");
                return;
            }

            try
            {
                int idProyecto = json["id_proyecto"].Value<int>();
                string descripcion = json["descripcion"].ToString();
                int porcentaje = json["porcentaje_completado"].Value<int>();

                using var db = new Database(Program.connStr);

                string sql = @"INSERT INTO Avances (descripcion, fecha_registro, porcentaje_completado, id_proyecto)
                       VALUES (:desc, SYSDATE, :porcentaje, :idProyecto)";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("desc", descripcion);
                cmd.Parameters.Add("porcentaje", porcentaje);
                cmd.Parameters.Add("idProyecto", idProyecto);

                int filas = cmd.ExecuteNonQuery();
                SendJson(socket, new { estado = "exito", mensaje = "Avance registrado." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al insertar avance");
                SendError(socket, "Error al insertar avance.");
            }
        }

        private void HandleActualizarAvance(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para actualizar avances.");
                return;
            }

            try
            {
                int idAvance = json["id_avance"].Value<int>();
                string descripcion = json["descripcion"].ToString();
                int porcentaje = json["porcentaje_completado"].Value<int>();

                using var db = new Database(Program.connStr);

                string sql = @"UPDATE Avances SET descripcion = :desc, porcentaje_completado = :porcentaje
                       WHERE id_avance = :idAvance";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("desc", descripcion);
                cmd.Parameters.Add("porcentaje", porcentaje);
                cmd.Parameters.Add("idAvance", idAvance);

                int filas = cmd.ExecuteNonQuery();
                SendJson(socket, new { estado = "exito", mensaje = "Avance actualizado." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al actualizar avance");
                SendError(socket, "Error al actualizar avance.");
            }
        }

        private void HandleInsertarEntrega(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para insertar entregas.");
                return;
            }

            try
            {
                int idProyecto = json["id_proyecto"].Value<int>();
                string nombreEntrega = json["nombre_entrega"].ToString();
                string fechaProgramada = json["fecha_programada"].ToString();  // formato: YYYY-MM-DD
                string estatus = json["estatus"].ToString();

                using var db = new Database(Program.connStr);

                string sql = @"INSERT INTO Entregas (nombre_entrega, fecha_programada, estatus, id_proyecto)
                       VALUES (:nombre, TO_DATE(:fecha, 'YYYY-MM-DD'), :estatus, :idProyecto)";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("nombre", nombreEntrega);
                cmd.Parameters.Add("fecha", fechaProgramada);
                cmd.Parameters.Add("estatus", estatus);
                cmd.Parameters.Add("idProyecto", idProyecto);

                int filas = cmd.ExecuteNonQuery();
                SendJson(socket, new { estado = "exito", mensaje = "Entrega registrada." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al insertar entrega");
                SendError(socket, "Error al insertar entrega.");
            }
        }

        private void HandleActualizarEntrega(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para actualizar entregas.");
                return;
            }

            try
            {
                int idEntrega = json["id_entrega"].Value<int>();
                string nombreEntrega = json["nombre_entrega"].ToString();
                string estatus = json["estatus"].ToString();
                string? fechaReal = json["fecha_real"]?.ToString();

                using var db = new Database(Program.connStr);

                string sql = @"UPDATE Entregas 
                       SET nombre_entrega = :nombre, estatus = :estatus,
                           fecha_real = CASE WHEN :fechaReal IS NULL THEN NULL 
                                             ELSE TO_DATE(:fechaReal, 'YYYY-MM-DD') END
                       WHERE id_entrega = :idEntrega";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("nombre", nombreEntrega);
                cmd.Parameters.Add("estatus", estatus);
                cmd.Parameters.Add("fechaReal", string.IsNullOrWhiteSpace(fechaReal) ? DBNull.Value : fechaReal);
                cmd.Parameters.Add("idEntrega", idEntrega);

                int filas = cmd.ExecuteNonQuery();
                SendJson(socket, new { estado = "exito", mensaje = "Entrega actualizada." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al actualizar entrega");
                SendError(socket, "Error al actualizar entrega.");
            }
        }

        private void HandleActualizarEstudiante(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ADMIN")
            {
                SendError(socket, "No autorizado para actualizar estudiantes.");
                return;
            }

            try
            {
                int idEstudiante = json["id_estudiante"].Value<int>();
                string nombre = json["nombre"].ToString();
                string carrera = json["carrera"].ToString();
                int semestre = json["semestre"].Value<int>();
                string correo = json["correo"].ToString();

                using var db = new Database(Program.connStr);

                string sql = @"UPDATE Estudiantes 
                       SET nombre = :nombre, carrera = :carrera, semestre = :semestre, correo = :correo 
                       WHERE id_estudiante = :idEstudiante";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("nombre", nombre);
                cmd.Parameters.Add("carrera", carrera);
                cmd.Parameters.Add("semestre", semestre);
                cmd.Parameters.Add("correo", correo);
                cmd.Parameters.Add("idEstudiante", idEstudiante);

                int filas = cmd.ExecuteNonQuery();
                SendJson(socket, new { estado = "exito", mensaje = "Estudiante actualizado." });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al actualizar estudiante");
                SendError(socket, "Error al actualizar estudiante.");
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
