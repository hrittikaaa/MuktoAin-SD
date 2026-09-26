using MuktoAin.Domain.Enums;

namespace MuktoAin.Application.DTOs;

public record PaymentOrderDto(
    int PaymentOrderId,
    int? CaseId,
    string Purpose,       // TopUp | Honorarium
    string Status,        // Pending | Paid | Failed | Refunded
    decimal Amount,
    decimal Commission,
    decimal NetToLawyer,
    string? GatewayRef,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? RefundedAt,
    string? UserEmail,    // anonymized citizen (email domain only where needed; admin sees full)
    string? LawyerName
);

// Balance: net honoraria not yet paid out or requested (what a new payout
// request may claim). PendingPayout: requested and awaiting the admin.
public record LawyerEarningsDto(
    decimal Balance,
    List<EarningRowDto> History,
    decimal PendingPayout = 0m
);

// One page of the honoraria history; Balance and PendingPayout still cover
// every row, same as LawyerEarningsDto.
public record LawyerEarningsPageDto(
    decimal Balance,
    decimal PendingPayout,
    int TotalCount,
    int Page,
    IReadOnlyList<EarningRowDto> Items
);

public enum PayoutRequestResult
{
    Requested,
    NothingToPay,
    AlreadyPending
}

public record EarningRowDto(
    int PaymentOrderId,
    int CaseId,
    decimal Gross,
    decimal Commission,
    decimal Net,
    DateTime PaidAt
);
