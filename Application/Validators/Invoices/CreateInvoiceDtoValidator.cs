using InvoiceSaaS.Application.DTOs.Invoices;
using InvoiceSaaS.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Invoices
{
    public class CreateInvoiceDtoValidator : AbstractValidator<CreateInvoiceDto>
    {
        private readonly IClientRepository _clientRepo;
        private readonly ICurrentUserService _currentUser;

        public CreateInvoiceDtoValidator(IClientRepository clientRepo, ICurrentUserService currentUser)
        {
            _clientRepo = clientRepo;
            _currentUser = currentUser;

            RuleFor(x => x.ClientId)
                .NotEmpty().WithMessage("Please select a client.")
                .MustAsync(ClientBelongsToCompany).WithMessage("The selected client was not found.");

            RuleFor(x => x.IssueDate)
                .NotEmpty().WithMessage("Issue date is required.")
                .LessThanOrEqualTo(DateTime.Today.AddDays(1))
                .WithMessage("Issue date cannot be in the future.");

            RuleFor(x => x.DueDate)
                .NotEmpty().WithMessage("Due date is required.")
                .GreaterThanOrEqualTo(x => x.IssueDate)
                .WithMessage("Due date must be on or after the issue date.");

            RuleFor(x => x.TaxRate)
                .InclusiveBetween(0, 100).WithMessage("Tax rate must be between 0 and 100.");

            RuleFor(x => x.Discount)
                .GreaterThanOrEqualTo(0).WithMessage("Discount cannot be negative.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("Notes cannot exceed 2000 characters.")
                .When(x => !string.IsNullOrEmpty(x.Notes));

            RuleFor(x => x.Terms)
                .MaximumLength(2000).WithMessage("Terms cannot exceed 2000 characters.")
                .When(x => !string.IsNullOrEmpty(x.Terms));

            RuleFor(x => x.Items)
                .NotEmpty().WithMessage("At least one line item is required.")
                .Must(items => items.Count <= 100).WithMessage("An invoice cannot have more than 100 line items.");

            RuleForEach(x => x.Items).SetValidator(new InvoiceItemDtoValidator());

            // Discount cannot exceed subtotal
            RuleFor(x => x)
                .Must(x => {
                    var subtotal = x.Items.Sum(i => i.Quantity * i.UnitPrice);
                    return x.Discount <= subtotal;
                })
                .WithMessage("Discount cannot exceed the invoice subtotal.")
                .When(x => x.Items.Any());
        }

        private async Task<bool> ClientBelongsToCompany(Guid clientId, CancellationToken ct)
        {
            var companyId = _currentUser.CompanyId;
            if (companyId is null) return false;
            var client = await _clientRepo.GetByIdAsync(clientId, ct);
            return client is not null && client.CompanyId == companyId && client.IsActive;
        }
    }
}
