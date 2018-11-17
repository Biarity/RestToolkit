using Microsoft.AspNetCore.Identity.UI.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace RestToolkit.Services
{
    public class DevEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string message)
        {
            Console.WriteLine($"\nEMAIL SENT: \n" +
                $"TO: {email} \n" +
                $"SUBJECT: {subject} \n" +
                $"BODY: {message} \n");

            return Task.FromResult(0);
        }
    }
}
