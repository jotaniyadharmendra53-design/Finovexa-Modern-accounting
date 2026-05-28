using InvoiceSaaS.Application.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;

namespace InvoiceSaaS.Application.Validators.AuthValidator
{
    public class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
    {
        public ForgotPasswordDtoValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Please enter a valid email address.");
        }
    }
}
