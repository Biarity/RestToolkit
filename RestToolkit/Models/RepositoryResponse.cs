using System;
using System.Collections.Generic;
using System.Text;

namespace RestToolkit.Models
{
    public class RepositoryResponse
    {
        public bool Success { get; set; } = true;
        public string ErrorMessage { get; set; }
    }
}
