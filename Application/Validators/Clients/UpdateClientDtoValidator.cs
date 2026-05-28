using InvoiceSaaS.Application.DTOs.Clients;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Clients
{
    public class UpdateClientDtoValidator : AbstractValidator<UpdateClientDto>
    {
        public UpdateClientDtoValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Client ID is required.");

            Include(new CreateClientDtoValidator());
        }
    }
}
