using System.Collections.Generic;
using Newtonsoft.Json;

namespace ServidorTBD
{
    public class BaseResponse
    {
        [JsonProperty("estado")]
        public string Estado { get; set; }

        [JsonProperty("datos")]
        public object Datos { get; set; }  // Puede ser string, lista, objeto...
    }

    public class ErrorResponse : BaseResponse
    {
        public ErrorResponse(string mensajeError)
        {
            Estado = "error";
            Datos = mensajeError;
        }
    }

    public class SuccessResponse : BaseResponse
    {
        public SuccessResponse(string mensajeExito)
        {
            Estado = "exito";
            Datos = mensajeExito;
        }
    }

    public class GetProjectsResponse : BaseResponse
    {
        public GetProjectsResponse(List<ProjectDto> proyectos)
        {
            Estado = "getprojects_response";
            Datos = proyectos;
        }
    }

    public class ProjectDto
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("nombre")]
        public string Nombre { get; set; }
    }

}
