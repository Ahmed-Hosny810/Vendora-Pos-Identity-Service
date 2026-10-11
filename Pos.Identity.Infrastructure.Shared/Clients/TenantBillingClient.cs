using Microsoft.Extensions.Options;
using Pos.Identity.Application.Dtos;
using Pos.Identity.Application.Exceptions;
using Pos.Identity.Application.Interfaces.Clients;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Application.Wrappers;
using Pos.Identity.Infrastructure.Shared.Settings;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Pos.Identity.Infrastructure.Shared.Clients
{
    public class TenantBillingClient : ITenantBillingClient
    {
        private readonly HttpClient _httpClient;
        private readonly ICurrentUserService _currentUserService;

        private readonly InternalApiKeyOptions _internalApiKeyOptions;

        public TenantBillingClient(
            HttpClient httpClient,
            ICurrentUserService currentUserService,
            IOptions<InternalApiKeyOptions> internalApiKeyOptions)
        {
            _httpClient = httpClient;
            _currentUserService = currentUserService;
            _internalApiKeyOptions = internalApiKeyOptions.Value;
        }

        public async Task<IncreaseCashierUsageResult> IncreaseCashierUsageAsync(IncreaseCashierUsageRequest request, CancellationToken cancellationToken)
        {
            var accessToken = _currentUserService.AccessToken;

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ApiException("Access token is missing.");

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/tenants/usage/increase-cashier");

            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            httpRequest.Content = JsonContent.Create(request);

            using var response = await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    $"Tenant Billing request failed. StatusCode: {(int)response.StatusCode}. Response: {responseBody}");
            }

            var apiResponse = JsonSerializer.Deserialize<Response<IncreaseCashierUsageResult>>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (apiResponse == null)
                throw new ApiException("Tenant Billing returned an empty response.");

            if (!apiResponse.Succeeded)
                throw new ApiException("Tenant Billing rejected cashier usage increase.");

            if (apiResponse.Data == null)
                throw new ApiException("Tenant Billing returned empty cashier usage result.");

            return apiResponse.Data;

        }

        public async Task<DecreaseCashierUsageResult> DecreaseCashierUsageAsync(DecreaseCashierUsageRequest request,CancellationToken cancellationToken)
        {
            var accessToken = _currentUserService.AccessToken;

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ApiException("Access token is missing.");

            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/v1/tenants/usage/decrease-cashier");

            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    $"Tenant Billing request failed. StatusCode: {(int)response.StatusCode}. Response: {responseBody}");
            }

            var apiResponse = JsonSerializer.Deserialize<Response<DecreaseCashierUsageResult>>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (apiResponse == null)
                throw new ApiException("Tenant Billing returned an empty response.");

            if (!apiResponse.Succeeded)
                throw new ApiException("Tenant Billing rejected cashier usage decrease.");

            if (apiResponse.Data == null)
                throw new ApiException("Tenant Billing returned empty usage result.");

            return apiResponse.Data;
        }

        public async Task<CreateTenantResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken)
        {
            var accessToken=_currentUserService.AccessToken;

            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ApiException("Access token is missing.");

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenants");

            httpRequest.Headers.Authorization=new AuthenticationHeaderValue("Bearer", accessToken);

            httpRequest.Content = JsonContent.Create(request);

            var response = await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiException(
                    $"Tenant Billing request failed. StatusCode: {(int)response.StatusCode}. Response: {responseBody}");
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<Response<Guid>>(
                cancellationToken: cancellationToken);

            if (apiResponse == null)
                throw new ApiException("Tenant Billing returned an empty response.");

            if (!apiResponse.Succeeded)
                throw new ApiException(apiResponse.Message ?? "Tenant onboarding was rejected.");

            if (apiResponse.Data == Guid.Empty)
                throw new ApiException("Tenant Billing returned an invalid TenantId.");

            return new CreateTenantResult
            {
                TenantId = apiResponse.Data
            }; ;
        }

        public async Task<TenantSubscriptionAccessDto> GetSubscriptionAccessAsync(
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException(
                    "A valid tenant ID is required.",
                    nameof(tenantId));

            if (string.IsNullOrWhiteSpace(_internalApiKeyOptions.ApiKey))
                throw new InvalidOperationException(
                    "The internal API key is not configured.");

            // 1. Authenticate this request with the API key.
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/internal/tenants/{tenantId}/subscription-access");

            httpRequest.Headers.Add(
                "X-Internal-Api-Key",
                _internalApiKeyOptions.ApiKey);

            // 2. Send the request.
            using var response = await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // 3. Read the existing Response<T> wrapper.
            var apiResponse = await response.Content
                .ReadFromJsonAsync<Response<TenantSubscriptionAccessDto>>(
                    cancellationToken: cancellationToken);

            if (apiResponse == null ||
                !apiResponse.Succeeded ||
                apiResponse.Data == null)
            {
                throw new InvalidOperationException(
                    "TenantService returned an invalid subscription-access response.");
            }

            var access = apiResponse.Data;

            // 4. Verify that the response belongs to the requested tenant.
            if (access.TenantId != tenantId)
                throw new InvalidOperationException(
                    "TenantService returned subscription access for a different tenant.");

            if (access.CheckedAtUtc == default)
                throw new InvalidOperationException(
                    "TenantService did not return the access-check time.");

            if (access.IsAllowed && !access.SubscriptionEndsAt.HasValue)
                throw new InvalidOperationException(
                    "Allowed subscription access must include an expiry date.");

            return access;
        }
    }
}
