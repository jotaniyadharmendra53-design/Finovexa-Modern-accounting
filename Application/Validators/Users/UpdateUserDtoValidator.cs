using InvoiceSaaS.Application.DTOs.Users;
using InvoiceSaaS.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Users
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;

        public UpdateUserDtoValidator(IUserRepository userRepo, IRoleRepository roleRepo)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("User ID is required.");

            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full name is required.")
                .MaximumLength(150).WithMessage("Full name cannot exceed 150 characters.");

            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Please enter a valid email address.")
                .MaximumLength(200).WithMessage("Email cannot exceed 200 characters.")
                .MustAsync(BeUniqueEmail).WithMessage("This email address is already used by another account.");

            RuleFor(x => x.Phone)
                .MaximumLength(20).WithMessage("Phone cannot exceed 20 characters.")
                .When(x => !string.IsNullOrEmpty(x.Phone));

            RuleFor(x => x.RoleId)
                .NotEmpty().WithMessage("Please select a role for this user.")
                .MustAsync(RoleExists).WithMessage("The selected role does not exist.");
        }

        private async Task<bool> BeUniqueEmail(UpdateUserDto dto, string email, CancellationToken ct)
            => !await _userRepo.EmailExistsAsync(email, dto.Id, ct);

        private async Task<bool> RoleExists(Guid roleId, CancellationToken ct)
            => await _roleRepo.GetByIdAsync(roleId, ct) is not null;
    }
}
