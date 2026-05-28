using InvoiceSaaS.Application.DTOs.Users;
using InvoiceSaaS.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Users
{
    public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;

        public CreateUserDtoValidator(IUserRepository userRepo, IRoleRepository roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;

            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full name is required.")
                .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Please enter a valid email address.")
                .MaximumLength(200).WithMessage("Email cannot exceed 200 characters.")
                .MustAsync(BeUniqueEmail).WithMessage("This email address is already registered.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
                .Matches(@"[0-9]").WithMessage("Password must contain at least one number.")
                .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

            RuleFor(x => x.ConfirmPassword)
                .NotEmpty().WithMessage("Please confirm the password.")
                .Equal(x => x.Password).WithMessage("Passwords do not match.");

            RuleFor(x => x.Phone)
                .MaximumLength(20).WithMessage("Phone cannot exceed 20 characters.")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.RoleId)
                .NotEmpty().WithMessage("Please select a role for this user.")
                .MustAsync(RoleExists).WithMessage("The selected role does not exist.");
        }

        private async Task<bool> BeUniqueEmail(string email, CancellationToken ct)
            => !await _userRepo.EmailExistsAsync(email, null, ct);

        private async Task<bool> RoleExists(Guid roleId, CancellationToken ct)
            => await _roleRepo.GetByIdAsync(roleId, ct) is not null;
    }
}
