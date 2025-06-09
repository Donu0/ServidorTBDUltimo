using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Serilog;

namespace ServidorTBD
{
    public class Database : IDisposable
    {
        public OracleConnection _connection;

        // Cadena de conexión básica (modifícala con tus datos)
        private readonly string _connectionString;

        public bool TestConnection()
        {
            try
            {
                using var cmd = new OracleCommand("SELECT 1 FROM DUAL", _connection);
                var result = cmd.ExecuteScalar();
                return result != null && Convert.ToInt32(result) == 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error probando conexión a Oracle");
                return false;
            }
        }

        public Database(string connectionString)
        {

            try
            {
                _connection = new OracleConnection(connectionString);
                _connection.Open();

                Log.Information("Conexión a Oracle abierta correctamente.");

                // (Opcional) Prueba de conexión o logging de bienvenida:
                //using var cmd = new OracleCommand("SELECT COUNT(*) FROM UsuariosSistema", _connection);
                //var count = Convert.ToInt32(cmd.ExecuteScalar());
                //Log.Information("UsuariosSistema tiene {Count} usuarios.", count);
            }

            catch (Exception ex)
            {
                Log.Error(ex, "Error al abrir la conexión en el constructor Database");
                throw;
            }

        }



        //public bool AuthenticateUser(string username, string password)
        //{
        //    try
        //    {
        //        // Aquí validamos con una consulta la existencia del usuario y su contraseña
        //        // Suponemos que hay una tabla UsuariosSistema con campos Usuario y Password
        //        string sql = "SELECT COUNT(*) FROM ADMIN.UsuariosSistema WHERE Usuario = :user AND Password = :pass";

        //        using var cmd = new OracleCommand(sql, _connection);
        //        cmd.Parameters.Add(new OracleParameter("user", username));
        //        cmd.Parameters.Add(new OracleParameter("pass", password));

        //        int count = Convert.ToInt32(cmd.ExecuteScalar());

        //        return count > 0;
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex, "Error en AuthenticateUser");
        //        return false;
        //    }
        //}

        public List<(int Id, string Nombre)> GetProjects()
        {
            var projects = new List<(int, string)>();

            try
            {
                string sql = "SELECT Id, Nombre FROM ADMIN.Proyectos";

                using var cmd = new OracleCommand(sql, _connection);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    projects.Add((reader.GetInt32(0), reader.GetString(1)));
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error en GetProjects");
            }

            return projects;
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                    _connection.Close();

                _connection.Dispose();
                Log.Information("Conexión a Oracle cerrada.");
            }
        }

        public OracleCommand CreateCommand(string sql)
        {
            return new OracleCommand(sql, _connection);
        }

        public void AddParameter(OracleCommand cmd, string name, object value)
        {
            cmd.Parameters.Add(new OracleParameter(name, value));
        }

    }

}

