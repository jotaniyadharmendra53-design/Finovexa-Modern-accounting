using InvoiceSaaS.Application.DTOs.Clients;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Clients
{
    public class CreateClientDtoValidator : AbstractValidator<CreateClientDto>
    {
        public CreateClientDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Client name is required.")
                .MaximumLength(200).WithMessage("Client name cannot exceed 200 characters.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Please enter a valid email address.")
                .MaximumLength(200).WithMessage("Email cannot exceed 200 characters.")
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Phone)
                .MaximumLength(30).WithMessage("Phone cannot exceed 30 characters.")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Address)
                .MaximumLength(500).WithMessage("Address cannot exceed 500 characters.")
                .When(x => !string.IsNullOrEmpty(x.Address));

            RuleFor(x => x.Notes)
                .MaximumLength(1000).WithMessage("Notes cannot exceed 1000 characters.")
                .When(x => !string.IsNullOrEmpty(x.Notes));

            RuleFor(x => x.TaxNumber)
                .MaximumLength(100).WithMessage("Tax number cannot exceed 100 characters.")
                .When(x => !string.IsNullOrEmpty(x.TaxNumber));
        }
    }
}
