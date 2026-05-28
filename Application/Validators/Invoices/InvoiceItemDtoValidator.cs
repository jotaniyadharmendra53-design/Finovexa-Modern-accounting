using InvoiceSaaS.Application.DTOs.Invoices;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Invoices
{
    public class InvoiceItemDtoValidator : AbstractValidator<InvoiceItemDto>
    {
        public InvoiceItemDtoValidator()
        {
            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Item description is required.")
                .MaximumLength(500).WithMessage("Item description cannot exceed 500 characters.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.")
                .LessThanOrEqualTo(99999).WithMessage("Quantity seems too large.");

            RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("Unit price cannot be negative.")
                .LessThan(1_000_000_000).WithMessage("Unit price seems too large.");
        }
    }
}
