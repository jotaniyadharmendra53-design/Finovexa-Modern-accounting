using InvoiceSaaS.Application.DTOs.Roles;
using InvoiceSaaS.Domain.Interfaces;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Validators.Roles
{
    public class UpdateRoleDtoValidator : AbstractValidator<UpdateRoleDto>
    {
        private readonly IRoleRepository _roleRepo;

        public UpdateRoleDtoValidator(IRoleRepository roleRepo)
        {
            _roleRepo = roleRepo;

            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("Role ID is required.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Role name is required.")
                .MaximumLength(100).WithMessage("Role name cannot exceed 100 characters.")
                .MustAsync(BeUniqueName).WithMessage("A role with this name already exists.");

            RuleFor(x => x.Description)
                .MaximumLength(300).WithMessage("Description cannot exceed 300 characters.")
                .When(x => x.Description is not null);

            RuleFor(x => x.PermissionIds)
                .NotEmpty().WithMessage("Please select at least one permission for this role.");
        }

        private async Task<bool> BeUniqueName(UpdateRoleDto dto, string name, CancellationToken ct)
            => !await _roleRepo.NameExistsAsync(name, dto.CompanyId, dto.Id, ct);
    }
}
