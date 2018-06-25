using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

namespace RestToolkit.Infrastructure
{
    public class DbUpdateExceptionHandler
    {
        private readonly RequestDelegate _next;

        public DbUpdateExceptionHandler(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (DbUpdateException ex)
            {
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            object result;

            var code = 400;

            if (exception.InnerException.Message.ToLower().Contains("duplicate"))
            {
                code = 409;
                result = new
                {
                    Error = "Error updating database. Duplicate value."
                };
            }
            else
            {
                result = new
                {
                    Error = "Error updating database."
                };
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = code;
            await context.Response.WriteAsync(JsonConvert.SerializeObject(result));
        }
    }

    public static class HandleDbUpdateExceptionExtensions
    {
        public static IApplicationBuilder UseDbUpdateExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<DbUpdateExceptionHandler>();
        }
    }

}
