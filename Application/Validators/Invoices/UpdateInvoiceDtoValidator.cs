using InvoiceSaaS.Application.DTOs.Invoices;
using InvoiceSaaS.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Invoices
{
    public class UpdateInvoiceDtoValidator : AbstractValidator<UpdateInvoiceDto>
    {
        public UpdateInvoiceDtoValidator(IClientRepository clientRepo, ICurrentUserService currentUser)
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Invoice ID is required.");

            Include(new CreateInvoiceDtoValidator(clientRepo, currentUser));
        }
    }
}
