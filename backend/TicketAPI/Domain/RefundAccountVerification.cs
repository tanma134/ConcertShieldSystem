using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Domain;

public sealed record RefundAccountCheck(
    string IdentityName, string BankReportedName, string IdentityEvidence,
    string BankEvidence, string CustomerConfirmationEvidence, string Notes,
    int VerifiedBy, DateTime VerifiedAt, string DestinationFingerprint, bool IsSimulation,
    string? TransferReceipt = null);

public static class RefundAccountVerification
{
    public static string? MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return accountNumber;

        var value = accountNumber.Trim();
        if (value.Length <= 4) return new string('*', value.Length);
        return new string('*', value.Length - 4) + value[^4..];
    }

    public static RefundAccountCheck Verify(TicketReturnRequest request, VerifyRefundAccountDto dto, int actorId, bool allowSimulation)
    {
        if (dto.IsSimulation && !allowSimulation)
            throw new InvalidOperationException("Refund simulation is disabled in this environment.");
        if (string.IsNullOrWhiteSpace(dto.IdentityName) || string.IsNullOrWhiteSpace(dto.BankReportedName))
            throw new ArgumentException("The verified identity name and the name returned by the bank are required.");
        if (Normalize(dto.IdentityName) != Normalize(dto.BankReportedName) ||
            Normalize(dto.BankReportedName) != Normalize(request.BankAccountName))
            throw new ArgumentException("The bank-reported name must match the checked buyer identity and the requested account holder. Keep this refund awaiting verification.");
        if (dto.IdentityName.Length > 200 || dto.BankReportedName.Length > 200)
            throw new ArgumentException("Account holder names are too long.");
        ValidateEvidence(dto.IdentityEvidence, "Identity check evidence");
        ValidateEvidence(dto.BankEvidence, "Bank beneficiary lookup evidence");
        ValidateEvidence(dto.CustomerConfirmationEvidence, "Confirmation through the customer's registered contact");
        if (string.IsNullOrWhiteSpace(dto.Notes) || dto.Notes.Trim().Length < 10 || dto.Notes.Length > 1000)
            throw new ArgumentException("Verification notes must contain 10 to 1000 characters.");
        return new(dto.IdentityName.Trim(), dto.BankReportedName.Trim(), dto.IdentityEvidence!, dto.BankEvidence!,
            dto.CustomerConfirmationEvidence!, dto.Notes.Trim(), actorId, DateTime.UtcNow, Fingerprint(request), dto.IsSimulation);
    }

    public static RefundAccountCheck? Read(TicketReturnRequest request)
    {
        if (string.IsNullOrEmpty(request.BankVerificationJson)) return null;
        try
        {
            var check = JsonSerializer.Deserialize<RefundAccountCheck>(request.BankVerificationJson);
            return check?.DestinationFingerprint == Fingerprint(request) ? check : null;
        }
        catch (JsonException) { return null; }
    }

    public static string Normalize(string? value) => new((value ?? "").Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
        .Select(c => char.ToUpperInvariant(c == 'đ' || c == 'Đ' ? 'D' : c)).ToArray());

    public static void ValidateEvidence(string? evidence, string label)
    {
        if (string.IsNullOrWhiteSpace(evidence)) throw new ArgumentException(label + " is required.");
        if (evidence.Length > 3_000_000) throw new ArgumentException(label + " exceeds the 2 MB attachment limit.");
        if (Uri.TryCreate(evidence, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps) return;
        var prefixes = new[] { "data:application/pdf;base64,", "data:image/png;base64,", "data:image/jpeg;base64," };
        var prefix = prefixes.FirstOrDefault(p => evidence.StartsWith(p, StringComparison.Ordinal));
        if (prefix == null) throw new ArgumentException(label + " must be a PDF/JPG/PNG attachment or an HTTPS evidence link.");
        try
        {
            var bytes = Convert.FromBase64String(evidence[prefix.Length..]);
            if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) throw new FormatException();
            var valid = prefix.Contains("pdf") ? bytes.AsSpan().StartsWith("%PDF-"u8)
                : prefix.Contains("png") ? bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                : bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 });
            if (!valid) throw new FormatException();
        }
        catch (FormatException) { throw new ArgumentException(label + " is not a valid attachment."); }
    }

    private static string Fingerprint(TicketReturnRequest request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{request.TicketReturnRequestId}|{request.RequesterUserId}|{request.BankName?.Trim()}|{request.BankAccountNumber?.Trim()}|{request.BankAccountName?.Trim()}")));
}
