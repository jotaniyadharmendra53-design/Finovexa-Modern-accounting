using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.DTOs.Clients
{
    public class UpdateClientDto : CreateClientDto
    {
        public Guid Id { get; set; }
    }
}
