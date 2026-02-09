namespace next_go_auth_server.Services;

public interface IEmailService
{
    // SuperAdmin Notifications
    Task SendNewOrgRegistrationNotificationAsync(string superAdminEmail, string superAdminName,
        string applicantName, string applicantEmail, string requestedOrgName);

    // Organization Admin Approval/Rejection
    Task SendOrgAdminApprovalEmailAsync(string email, string firstName, string organizationName);
    Task SendOrgAdminRejectionEmailAsync(string email, string firstName, string reason);

    // User Invitation
    Task SendInvitationEmailAsync(string email, string firstName, string organizationName, string tempPassword);

    // Account Status Changes
    Task SendAccountDeactivatedEmailAsync(string email, string firstName, string reason);
    Task SendAccountReactivatedEmailAsync(string email, string firstName);

    // Organization Status Changes
    Task SendOrganizationDeactivatedEmailAsync(string email, string firstName, string organizationName, string reason);
    Task SendOrganizationReactivatedEmailAsync(string email, string firstName, string organizationName);

    // Upgrade Request Notifications
    Task SendUpgradeRequestSubmittedToOrgAsync(string email, string firstName, string organizationName,
        string currentPlan, string requestedPlan);
    Task SendUpgradeRequestNotificationToAdminAsync(string adminEmail, string adminName,
        string organizationName, string orgAdminName, string currentPlan, string requestedPlan, string reason);
    Task SendUpgradeRequestApprovedAsync(string email, string firstName, string organizationName,
        string oldPlan, string newPlan);
    Task SendUpgradeRequestRejectedAsync(string email, string firstName, string organizationName,
        string requestedPlan, string rejectionReason);
    Task SendPlanChangedByAdminAsync(string email, string firstName, string organizationName,
        string oldPlan, string newPlan, string? adminReason);
}
