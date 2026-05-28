using InvoiceSaaS.Application.Common;
using InvoiceSaaS.Application.DTOs.Clients;
using InvoiceSaaS.Application.DTOs.Common;
using InvoiceSaaS.Application.Services.Interfaces;
using InvoiceSaaS.Domain.Entities;
using InvoiceSaaS.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InvoiceSaaS.Application.Services.Implementations
{
    // ═══════════════════════════════════════════════════════════
    //  ClientService
    // ═══════════════════════════════════════════════════════════
    public class ClientService : IClientService
    {
        private readonly IClientRepository _clientRepo;
        private readonly ILogger<ClientService> _logger;

        public ClientService(IClientRepository clientRepo, ILogger<ClientService> logger)
        {
            _clientRepo = clientRepo;
            _logger = logger;
        }

        public async Task<ServiceResult<ClientDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var client = await _clientRepo.GetByIdAsync(id, ct);
            if (client is null) return ServiceResult<ClientDto>.Failure("Client not found.");
            return ServiceResult<ClientDto>.Success(MapToDto(client));
        }

        public async Task<ServiceResult<IEnumerable<ClientDto>>> GetByCompanyAsync(Guid companyId, string? search = null, string? status = null, int page = 1, int pageSize = 20, CancellationToken ct = default)

        {
            bool? isActive = status == null ? null : status == "1";
            var clients = await _clientRepo.GetByCompanyAsync(companyId, search, isActive, ct);
            var paged = clients.Skip(Math.Max(0, (page - 1) * pageSize)).Take(pageSize);
            return ServiceResult<IEnumerable<ClientDto>>.Success(paged.Select(MapToDto));
        }

        public async Task<ServiceResult<ClientDto>> CreateAsync(CreateClientDto dto, Guid companyId, Guid createdBy, CancellationToken ct = default)
        {
            try
            {
                var client = new Client
                {
                    CompanyId = companyId,
                    Name = dto.Name.Trim(),
                    Email = dto.Email?.Trim().ToLower(),
                    Phone = dto.Phone?.Trim(),
                    Address = dto.Address?.Trim(),
                    City = dto.City?.Trim(),
                    State = dto.State?.Trim(),
                    Country = dto.Country?.Trim(),
                    PostalCode = dto.PostalCode?.Trim(),
                    TaxNumber = dto.TaxNumber?.Trim(),
                    Notes = dto.Notes?.Trim(),
                    CurrencyCode = !string.IsNullOrWhiteSpace(dto.CurrencyCode)
                       ? dto.CurrencyCode.Trim().ToUpper() : "INR",
                    IsActive = dto.IsActive,
                    CreatedBy = createdBy
                };
                await _clientRepo.AddAsync(client, ct);
                return ServiceResult<ClientDto>.Success(MapToDto(client), "Client created successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client");
                return ServiceResult<ClientDto>.Failure("An error occurred while creating the client.");
            }
        }

        public async Task<ServiceResult<ClientDto>> UpdateAsync(UpdateClientDto dto, Guid updatedBy, CancellationToken ct = default)
        {
            try
            {
                var client = await _clientRepo.GetByIdAsync(dto.Id, ct);
                if (client is null) return ServiceResult<ClientDto>.Failure("Client not found.");

                client.Name = dto.Name.Trim();
                client.Email = dto.Email?.Trim().ToLower();
                client.Phone = dto.Phone?.Trim();
                client.Address = dto.Address?.Trim();
                client.City = dto.City?.Trim();
                client.State = dto.State?.Trim();
                client.Country = dto.Country?.Trim();
                client.PostalCode = dto.PostalCode?.Trim();
                client.TaxNumber = dto.TaxNumber?.Trim();
                client.Notes = dto.Notes?.Trim();
                client.CurrencyCode = !string.IsNullOrWhiteSpace(dto.CurrencyCode)
                              ? dto.CurrencyCode.Trim().ToUpper() : client.CurrencyCode;
                client.IsActive = dto.IsActive;
                client.UpdatedAt = DateTime.UtcNow;
                client.UpdatedBy = updatedBy;
                await _clientRepo.UpdateAsync(client, ct);
                return ServiceResult<ClientDto>.Success(MapToDto(client), "Client updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client {Id}", dto.Id);
                return ServiceResult<ClientDto>.Failure("An error occurred while updating the client.");
            }
        }

        public async Task<ServiceResult> DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct = default)
        {
            var client = await _clientRepo.GetByIdAsync(id, ct);
            if (client is null) return ServiceResult.Failure("Client not found.");
            if (await _clientRepo.HasInvoicesAsync(id, ct))
                return ServiceResult.Failure("Cannot delete a client that has invoices. Consider deactivating instead.");
            await _clientRepo.DeleteAsync(id, deletedBy, ct);
            return ServiceResult.Success("Client deleted successfully.");
        }

        public async Task<ServiceResult<IEnumerable<SelectItemDto>>> GetSelectListAsync(Guid companyId, CancellationToken ct = default)
        {
            var clients = await _clientRepo.GetByCompanyAsync(companyId, null, null, ct);
            var items = clients.Where(c => c.IsActive).Select(c => new SelectItemDto
            {
                Value = c.Id.ToString(),
                Text = c.Name + (string.IsNullOrEmpty(c.Email) ? "" : $" — {c.Email}")
            });
            return ServiceResult<IEnumerable<SelectItemDto>>.Success(items);
        }

        private static ClientDto MapToDto(Client c) => new()
        {
            Id = c.Id,
            CompanyId = c.CompanyId,
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Country = c.Country,
            PostalCode = c.PostalCode,
            TaxNumber = c.TaxNumber,
            Notes = c.Notes,
            CurrencyCode = c.CurrencyCode,
            IsActive = c.IsActive,
            InvoiceCount = c.Invoices?.Count ?? 0,
            CreatedAt = c.CreatedAt
        };

        public async Task<ServiceResult<string>> GetCurrencyAsync(
    Guid clientId, CancellationToken ct = default)
        {
            var client = await _clientRepo.GetByIdAsync(clientId, ct);
            if (client is null) return ServiceResult<string>.Failure("Client not found.");
            return ServiceResult<string>.Success(client.CurrencyCode ?? "INR");
        }
    }
}
