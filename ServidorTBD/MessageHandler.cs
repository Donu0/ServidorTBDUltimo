﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Transactions;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
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
                    case "auditoria_asesor":
                        HandleListarAuditoriasAsesor(socket, json);
                        break;
                    case "cambiar_contrasena":
                        HandleCambiarContrasena(socket, json);
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
                // Obtener id_asesor desde id_usuario
                int idUsuario = session.UserId;
                int idAsesor;

                using (var cmdGetAsesor = new OracleCommand("SELECT id_asesor FROM Asesores WHERE id_usuario = :id_usuario", db._connection))
                {
                    cmdGetAsesor.Transaction = transaction;
                    cmdGetAsesor.Parameters.Add("id_usuario", OracleDbType.Int32).Value = idUsuario;

                    using var reader = cmdGetAsesor.ExecuteReader();
                    if (reader.Read())
                    {
                        idAsesor = reader.GetInt32(0);
                    }
                    else
                    {
                        SendError(socket, "No se encontró un asesor asociado al usuario.");
                        return;
                    }
                }

                // Establecer usuario para auditoría
                using (var cmdCtx = new OracleCommand("BEGIN pkg_ctx_usuario.set_usuario(:usuario); END;", db._connection))
                {
                    cmdCtx.Transaction = transaction;
                    cmdCtx.Parameters.Add("usuario", OracleDbType.Varchar2).Value = session.Username;
                    cmdCtx.ExecuteNonQuery();
                }

                // Llamar al procedimiento para insertar proyecto
                using var cmd = new OracleCommand("insertar_proyecto", db._connection);
                cmd.Transaction = transaction;
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_nombre", OracleDbType.Varchar2).Value = nombre;
                cmd.Parameters.Add("p_descripcion", OracleDbType.Varchar2).Value = descripcion;
                cmd.Parameters.Add("p_fecha_inicio", OracleDbType.Date).Value = DateTime.Parse(fechaInicio);
                cmd.Parameters.Add("p_fecha_estimada_entrega", OracleDbType.Date).Value = DateTime.Parse(fechaEntrega);
                cmd.Parameters.Add("p_estatus", OracleDbType.Varchar2).Value = estatus;
                cmd.Parameters.Add("p_id_asesor", OracleDbType.Int32).Value = idAsesor;

                cmd.ExecuteNonQuery();

                transaction.Commit();
                SendSuccess(socket, "Proyecto creado correctamente.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Log.Error(ex, "Error al crear proyecto");
                SendError(socket, "Error interno al crear el proyecto.");
            }
        }

        private void HandleCambiarContrasena(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session))
            {
                SendError(socket, "No autorizado.");
                return;
            }

            var datos = json["datos"];
            var contrasenaActual = datos?["actual"]?.ToString();
            var contrasenaNueva = datos?["nueva"]?.ToString();

            if (string.IsNullOrWhiteSpace(contrasenaActual) || string.IsNullOrWhiteSpace(contrasenaNueva))
            {
                SendError(socket, "Las contraseñas no pueden estar vacías.");
                return;
            }

            using var db = new Database(Program.connStr);
            using var transaction = db._connection.BeginTransaction();

            try
            {
                int idUsuario = session.UserId;

                // 1. Verificar contraseña actual
                using var cmdVerificar = new OracleCommand(
                    "SELECT contrasena FROM UsuariosSistema WHERE id_usuario = :id_usuario", db._connection);
                cmdVerificar.Transaction = transaction;
                cmdVerificar.Parameters.Add("id_usuario", OracleDbType.Int32).Value = idUsuario;

                var contrasenaBD = cmdVerificar.ExecuteScalar()?.ToString();

                if (contrasenaBD == null)
                {
                    SendError(socket, "Usuario no encontrado.");
                    return;
                }

                if (contrasenaBD != contrasenaActual)
                {
                    SendError(socket, "La contraseña actual es incorrecta.");
                    return;
                }

                // 2. Actualizar la contraseña
                using var cmdActualizar = new OracleCommand(
                    "UPDATE UsuariosSistema SET contrasena = :nueva WHERE id_usuario = :id_usuario", db._connection);
                cmdActualizar.Transaction = transaction;
                cmdActualizar.Parameters.Add("nueva", OracleDbType.Varchar2).Value = contrasenaNueva;
                cmdActualizar.Parameters.Add("id_usuario", OracleDbType.Int32).Value = idUsuario;

                cmdActualizar.ExecuteNonQuery();
                transaction.Commit();

                SendSuccess(socket, "Contraseña actualizada correctamente.");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Log.Error(ex, "Error al cambiar contraseña");
                SendError(socket, "Error interno al cambiar la contraseña.");
            }
        }


        private void HandleListarProyectosAlumno(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ESTUDIANTE")
            {
                SendError(socket, "No autorizado para ver proyectos de alumno.");
                return;
            }

            using var db = new Database(Program.connStr);

            try
            {
                using var cmd = new OracleCommand("BEGIN :result := obtener_proyectos_usuario(:idUsuario); END;", db._connection);
                cmd.CommandType = CommandType.Text;

                var refCursorParam = cmd.Parameters.Add("result", OracleDbType.RefCursor);
                refCursorParam.Direction = ParameterDirection.ReturnValue;

                cmd.Parameters.Add("idUsuario", OracleDbType.Int32).Value = session.UserId;

                cmd.ExecuteNonQuery();

                using var reader = ((OracleRefCursor)refCursorParam.Value).GetDataReader();

                var proyectos = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    var proyecto = new Dictionary<string, string>
                    {
                        ["id_proyecto"] = reader["id_proyecto"]?.ToString() ?? "",
                        ["nombre"] = reader["nombre"]?.ToString() ?? "",
                        ["descripcion"] = reader["descripcion"]?.ToString() ?? "",
                        ["fecha_inicio"] = reader["fecha_inicio"]?.ToString() ?? "",
                        ["fecha_estimada_entrega"] = reader["fecha_estimada_entrega"]?.ToString() ?? "",
                        ["estatus"] = reader["estatus"]?.ToString() ?? ""
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
                // Obtener id_asesor desde id_usuario
                string getAsesorIdSql = "SELECT id_asesor FROM Asesores WHERE id_usuario = :idUsuario";

                using var cmdGetAsesor = new OracleCommand(getAsesorIdSql, db._connection);
                cmdGetAsesor.Parameters.Add("idUsuario", OracleDbType.Int32).Value = session.UserId;

                object? result = cmdGetAsesor.ExecuteScalar();
                if (result == null)
                {
                    SendError(socket, "No se encontró un asesor vinculado al usuario.");
                    return;
                }

                int idAsesor = Convert.ToInt32(result);

                // Llamar a la función PL/SQL que retorna SYS_REFCURSOR
                using var cmd = new OracleCommand("BEGIN :result := obtener_proyectos_asesor(:idAsesor); END;", db._connection);
                cmd.CommandType = CommandType.Text;

                var refCursorParam = cmd.Parameters.Add("result", OracleDbType.RefCursor);
                refCursorParam.Direction = ParameterDirection.ReturnValue;

                cmd.Parameters.Add("idAsesor", OracleDbType.Int32).Value = idAsesor;

                cmd.ExecuteNonQuery();

                using var reader = ((OracleRefCursor)refCursorParam.Value).GetDataReader();

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
            using var transaction = db._connection.BeginTransaction();

            try
            {
                using (var cmdCtx = new OracleCommand("BEGIN pkg_ctx_usuario.set_usuario(:usuario); END;", db._connection))
                {
                    cmdCtx.Transaction = transaction;
                    cmdCtx.Parameters.Add("usuario", OracleDbType.Varchar2).Value = session.Username;
                    cmdCtx.ExecuteNonQuery();
                }

                using var cmd = new OracleCommand("asignar_estudiante", db._connection);
                cmd.Transaction = transaction;
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_id_estudiante", OracleDbType.Int32).Value = idEstudiante;
                cmd.Parameters.Add("p_id_proyecto", OracleDbType.Int32).Value = idProyecto;

                cmd.ExecuteNonQuery();

                transaction.Commit();
                SendSuccess(socket, "Proyecto asignado al estudiante correctamente.");
            }
            catch (OracleException ex) when (ex.Number == 20001) // Error definido en el SP
            {
                transaction.Rollback();
                SendError(socket, ex.Message);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
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
            if (!Program.Clients.TryGetValue(socket, out var session))
            {
                SendError(socket, "Sesión no válida.");
                return;
            }

            try
            {
                using var db = new Database(Program.connStr);

                using var cmd = new OracleCommand("sp_listar_entregas_por_usuario", db._connection);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_id_usuario", OracleDbType.Int32).Value = session.UserId;
                cmd.Parameters.Add("p_rol", OracleDbType.Varchar2).Value = session.Rol;
                cmd.Parameters.Add("p_entregas", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

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

        private void HandleListarAuditoriasAsesor(IWebSocketConnection socket, JObject json)
        {
            if (!Program.Clients.TryGetValue(socket, out var session) || session.Rol.ToUpper() != "ASESOR")
            {
                SendError(socket, "No autorizado para ver auditorías.");
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

                // Consultar auditorías relacionadas con los proyectos del asesor
                string sql = @"
            SELECT a.id_auditoria, a.usuario, a.accion, a.fecha, a.descripcion, a.id_proyecto
            FROM AuditoriaSistema a
            JOIN Proyectos p ON a.id_proyecto = p.id_proyecto
            WHERE p.id_asesor = :idAsesor";

                using var cmd = new OracleCommand(sql, db._connection);
                cmd.Parameters.Add("idAsesor", idAsesor);

                using var reader = cmd.ExecuteReader();
                var auditorias = new List<Dictionary<string, string>>();

                while (reader.Read())
                {
                    auditorias.Add(new Dictionary<string, string>
                    {
                        ["id_auditoria"] = reader["id_auditoria"].ToString(),
                        ["usuario"] = reader["usuario"].ToString(),
                        ["accion"] = reader["accion"].ToString(),
                        ["fecha"] = Convert.ToDateTime(reader["fecha"]).ToString("yyyy-MM-dd HH:mm:ss"),
                        ["descripcion"] = reader["descripcion"].ToString(),
                        ["id_proyecto"] = reader["id_proyecto"]?.ToString() ?? ""
                    });
                }

                SendJson(socket, new { estado = "exito", datos = auditorias });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error al listar auditorías del asesor");
                SendError(socket, "Error interno al obtener auditorías.");
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
                             VALUES (:nombre_usuario, :contrasena, 'ESTUDIANTE')
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
                              VALUES (:nombre_usuario, :contrasena, 'ASESOR')
                              RETURNING id_usuario INTO :id_usuario";

                using var cmdUsuario = new OracleCommand(sqlUsuario, db._connection);
                cmdUsuario.Transaction = transaction;
                cmdUsuario.Parameters.Add("nombre_usuario", OracleDbType.Varchar2).Value = nombreUsuario;
                cmdUsuario.Parameters.Add("contrasena", OracleDbType.Varchar2).Value = contrasena;
                cmdUsuario.Parameters.Add("id_usuario", OracleDbType.Int32).Direction = ParameterDirection.Output;

                cmdUsuario.ExecuteNonQuery();
                int idUsuario = ((OracleDecimal)cmdUsuario.Parameters["id_usuario"].Value).ToInt32();

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
                string fechaRegistroStr = json["fecha_registro"].ToString(); // formato: "yyyy-MM-dd"
                int porcentaje = json["porcentaje_completado"].Value<int>();

                DateTime fechaRegistro = DateTime.ParseExact(fechaRegistroStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

                using var db = new Database(Program.connStr);

                using var cmd = new OracleCommand("insertar_avance", db._connection);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_descripcion", OracleDbType.Varchar2).Value = descripcion;
                cmd.Parameters.Add("p_fecha_registro", OracleDbType.Date).Value = fechaRegistro;
                cmd.Parameters.Add("p_porcentaje_completado", OracleDbType.Int32).Value = porcentaje;
                cmd.Parameters.Add("p_id_proyecto", OracleDbType.Int32).Value = idProyecto;

                cmd.ExecuteNonQuery();

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
                string fechaProgramadaStr = json["fecha_programada"].ToString();  // formato: YYYY-MM-DD
                string estatus = json["estatus"].ToString();

                // Convertir a DateTime para pasar parámetro OracleDate
                DateTime fechaProgramada = DateTime.ParseExact(fechaProgramadaStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                DateTime fechaReal = DateTime.Now;

                using var db = new Database(Program.connStr);

                using var cmd = new OracleCommand("insertar_entrega", db._connection);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.Add("p_nombre_entrega", OracleDbType.Varchar2).Value = nombreEntrega;
                cmd.Parameters.Add("p_fecha_programada", OracleDbType.Date).Value = fechaProgramada;
                cmd.Parameters.Add("p_fecha_real", OracleDbType.Date).Value = fechaReal;
                cmd.Parameters.Add("p_estatus", OracleDbType.Varchar2).Value = estatus;
                cmd.Parameters.Add("p_id_proyecto", OracleDbType.Int32).Value = idProyecto;

                cmd.ExecuteNonQuery();

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
            Log.Information("Mensaje enviado: {Json}", json);

        }
    }
}
