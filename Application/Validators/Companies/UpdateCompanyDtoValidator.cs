using InvoiceSaaS.Application.DTOs.Companies;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Companies
{
    public class UpdateCompanyDtoValidator : AbstractValidator<UpdateCompanyDto>
    {
        public UpdateCompanyDtoValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Company ID is required.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Company name is required.")
                .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters.");

            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Please enter a valid email address.")
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Phone)
                .MaximumLength(20).WithMessage("Phone cannot exceed 20 characters.")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.Website)
                .MaximumLength(200).WithMessage("Website URL cannot exceed 200 characters.")
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Please enter a valid website URL (e.g. https://example.com).")
                .When(x => !string.IsNullOrEmpty(x.Website));

            RuleFor(x => x.CurrencyCode)
                .NotEmpty().WithMessage("Currency code is required.")
                .Length(3).WithMessage("Currency code must be exactly 3 characters (e.g. USD, EUR).");

            RuleFor(x => x.TaxNumber)
                .MaximumLength(100).WithMessage("Tax number cannot exceed 100 characters.")
                .When(x => !string.IsNullOrEmpty(x.TaxNumber));
        }
    }
}
