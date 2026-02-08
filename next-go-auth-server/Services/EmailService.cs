using System.Net;
using System.Net.Mail;

namespace next_go_auth_server.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _frontendUrl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _smtpHost = configuration["Email:SmtpHost"] ?? "";
        _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
        _smtpUsername = configuration["Email:SmtpUsername"] ?? "";
        _smtpPassword = configuration["Email:SmtpPassword"] ?? "";
        _fromEmail = configuration["Email:FromEmail"] ?? "";
        _fromName = configuration["Email:FromName"] ?? "CKHRC Immigration Platform";
        _frontendUrl = configuration["App:FrontendUrl"] ?? "https://ckhrc.com";
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_smtpHost) && !string.IsNullOrEmpty(_fromEmail);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Email configuration is missing. Skipping email to {Email}", to);
            return;
        }

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort);
            client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);
            client.EnableSsl = true;

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {Email}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", to, subject);
            throw;
        }
    }

    private string GetEmailWrapper(string content)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 10px 10px; }}
        .highlight-box {{ background: white; padding: 20px; border-left: 4px solid #667eea; margin: 20px 0; }}
        .success-box {{ background: #d4edda; border-left: 4px solid #28a745; padding: 15px; margin: 20px 0; }}
        .warning-box {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .danger-box {{ background: #f8d7da; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0; }}
        .info-box {{ background: #d1ecf1; border-left: 4px solid #17a2b8; padding: 15px; margin: 20px 0; }}
        .button {{ display: inline-block; padding: 12px 30px; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 30px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>CKHRC Immigration Platform</h1>
        </div>
        <div class='content'>
            {content}
            <p>Best regards,<br>CKHRC Immigration Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email. Please do not reply directly to this message.</p>
            <p>If you need assistance, please contact support@cktravels.com</p>
        </div>
    </div>
</body>
</html>";
    }

    // =====================================================
    // SuperAdmin Notifications
    // =====================================================

    public async Task SendNewOrgRegistrationNotificationAsync(
        string superAdminEmail,
        string superAdminName,
        string applicantName,
        string applicantEmail,
        string requestedOrgName)
    {
        var approvalUrl = $"{_frontendUrl}/admin/org-approvals";
        var subject = $"🔔 New Organization Registration: {requestedOrgName}";
        var content = $@"
            <p>Hi {superAdminName},</p>

            <div class='warning-box'>
                <h3 style='margin-top: 0; color: #856404;'>📋 New Organization Registration Pending Approval</h3>
                <p style='margin-bottom: 0;'>A new organization has registered and is awaiting your approval.</p>
            </div>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Registration Details:</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Organization Name:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{requestedOrgName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Applicant Name:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{applicantName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0;'><strong>Applicant Email:</strong></td>
                        <td style='padding: 8px 0;'><a href='mailto:{applicantEmail}'>{applicantEmail}</a></td>
                    </tr>
                </table>
            </div>

            <p>Please review this registration and take appropriate action:</p>

            <a href='{approvalUrl}' class='button'>Review & Approve/Reject</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><a href='{approvalUrl}'>{approvalUrl}</a></p>

            <div class='info-box'>
                <p style='margin: 0;'><strong>💡 Tip:</strong> Make sure you're logged in as a SuperAdmin before clicking the link.</p>
            </div>
        ";

        await SendEmailAsync(superAdminEmail, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // Organization Admin Approval/Rejection
    // =====================================================

    public async Task SendOrgAdminApprovalEmailAsync(string email, string firstName, string organizationName)
    {
        var subject = $"🎉 Your Organization Has Been Approved - {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='success-box'>
                <h3 style='margin-top: 0; color: #155724;'>✅ Great news! Your organization has been approved!</h3>
                <p style='margin-bottom: 0;'>Your organization <strong>{organizationName}</strong> is now active on the CKHRC Immigration Platform.</p>
            </div>

            <p>You now have access to:</p>
            <ul>
                <li>Upload and analyze CVs for immigration eligibility</li>
                <li>Invite team members to your organization</li>
                <li>Manage your organization's subscription</li>
                <li>View detailed immigration pathway reports</li>
            </ul>

            <p>Click the button below to log in and start exploring:</p>

            <a href='{_frontendUrl}' class='button'>Log In to Your Dashboard</a>

            <div class='info-box'>
                <p><strong>📊 Your Starter Plan includes:</strong></p>
                <ul style='margin-bottom: 0;'>
                    <li>Up to 5 team members</li>
                    <li>50 CV analyses per month</li>
                </ul>
            </div>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendOrgAdminRejectionEmailAsync(string email, string firstName, string reason)
    {
        var subject = "Organization Registration Update";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='danger-box'>
                <h3 style='margin-top: 0; color: #721c24;'>Organization Registration Not Approved</h3>
                <p style='margin-bottom: 0;'>We regret to inform you that your organization registration has not been approved at this time.</p>
            </div>

            <div class='highlight-box'>
                <p><strong>Reason:</strong></p>
                <p>{reason}</p>
            </div>

            <p>If you believe this decision was made in error or would like to provide additional information, please contact our support team.</p>

            <p>You can reach us at:</p>
            <ul>
                <li>Email: support@cktravels.com</li>
            </ul>

            <p>We appreciate your interest in CKHRC Immigration Platform and hope to assist you in the future.</p>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // User Invitation
    // =====================================================

    public async Task SendInvitationEmailAsync(string email, string firstName, string organizationName, string tempPassword)
    {
        var subject = $"You've been invited to join {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <p>You've been invited to join <strong>{organizationName}</strong> on the CKHRC Immigration Platform.</p>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Your Login Credentials:</h3>
                <p><strong>Email:</strong> {email}</p>
                <p><strong>Temporary Password:</strong> <code style='background: #f0f0f0; padding: 5px 10px; border-radius: 3px; font-size: 16px;'>{tempPassword}</code></p>
            </div>

            <div class='warning-box'>
                <p><strong>⚠️ Important Security Notice:</strong></p>
                <p style='margin-bottom: 0;'>This is a temporary password. For security reasons, please change your password after your first login.</p>
            </div>

            <p>Click the button below to log in and get started:</p>

            <a href='{_frontendUrl}' class='button'>Log In Now</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><a href='{_frontendUrl}'>{_frontendUrl}</a></p>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // Account Status Changes
    // =====================================================

    public async Task SendAccountDeactivatedEmailAsync(string email, string firstName, string reason)
    {
        var subject = "Important: Your Account Has Been Deactivated";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='danger-box'>
                <h3 style='margin-top: 0; color: #721c24;'>⚠️ Account Deactivated</h3>
                <p style='margin-bottom: 0;'>Your account on CKHRC Immigration Platform has been deactivated.</p>
            </div>

            <div class='highlight-box'>
                <p><strong>Reason for deactivation:</strong></p>
                <p>{reason}</p>
            </div>

            <p>While your account is deactivated, you will not be able to:</p>
            <ul>
                <li>Log in to the platform</li>
                <li>Upload or analyze CVs</li>
                <li>Access your previous reports</li>
            </ul>

            <p>If you believe this was done in error or would like to appeal this decision, please contact our support team:</p>
            <ul>
                <li>Email: support@cktravels.com</li>
            </ul>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendAccountReactivatedEmailAsync(string email, string firstName)
    {
        var subject = "🎉 Your Account Has Been Reactivated";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='success-box'>
                <h3 style='margin-top: 0; color: #155724;'>✅ Great news! Your account has been reactivated!</h3>
                <p style='margin-bottom: 0;'>You now have full access to the CKHRC Immigration Platform again.</p>
            </div>

            <p>You can now:</p>
            <ul>
                <li>Log in to your account</li>
                <li>Upload and analyze CVs</li>
                <li>Access all your previous reports and data</li>
            </ul>

            <a href='{_frontendUrl}' class='button'>Log In Now</a>

            <p>Thank you for your patience. If you have any questions, please don't hesitate to contact us.</p>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // Organization Status Changes
    // =====================================================

    public async Task SendOrganizationDeactivatedEmailAsync(string email, string firstName, string organizationName, string reason)
    {
        var subject = $"Important: {organizationName} Has Been Deactivated";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='danger-box'>
                <h3 style='margin-top: 0; color: #721c24;'>⚠️ Organization Deactivated</h3>
                <p style='margin-bottom: 0;'>Your organization <strong>{organizationName}</strong> on CKHRC Immigration Platform has been deactivated.</p>
            </div>

            <div class='highlight-box'>
                <p><strong>Reason for deactivation:</strong></p>
                <p>{reason}</p>
            </div>

            <p>While your organization is deactivated:</p>
            <ul>
                <li>All organization members will be unable to log in</li>
                <li>No new CV uploads can be made</li>
                <li>Existing data is preserved but not accessible</li>
            </ul>

            <p>If you are the organization administrator and believe this was done in error, please contact our support team immediately:</p>
            <ul>
                <li>Email: support@cktravels.com</li>
            </ul>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendOrganizationReactivatedEmailAsync(string email, string firstName, string organizationName)
    {
        var subject = $"🎉 {organizationName} Has Been Reactivated";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='success-box'>
                <h3 style='margin-top: 0; color: #155724;'>✅ Great news! Your organization has been reactivated!</h3>
                <p style='margin-bottom: 0;'>Your organization <strong>{organizationName}</strong> is now active again on the CKHRC Immigration Platform.</p>
            </div>

            <p>You and all organization members can now:</p>
            <ul>
                <li>Log in to the platform</li>
                <li>Upload and analyze CVs</li>
                <li>Access all previous reports and data</li>
            </ul>

            <a href='{_frontendUrl}' class='button'>Log In Now</a>

            <p>Thank you for your patience. If you have any questions, please don't hesitate to contact us.</p>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // Upgrade Request Notifications
    // =====================================================

    public async Task SendUpgradeRequestSubmittedToOrgAsync(
        string email,
        string firstName,
        string organizationName,
        string currentPlan,
        string requestedPlan)
    {
        var subject = $"Upgrade Request Submitted - {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='info-box'>
                <h3 style='margin-top: 0; color: #0c5460;'>📤 Upgrade Request Submitted</h3>
                <p style='margin-bottom: 0;'>Your upgrade request for <strong>{organizationName}</strong> has been submitted successfully.</p>
            </div>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Request Details:</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Current Plan:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{currentPlan}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0;'><strong>Requested Plan:</strong></td>
                        <td style='padding: 8px 0;'>{requestedPlan}</td>
                    </tr>
                </table>
            </div>

            <p>Our team will review your request and get back to you shortly. You will receive an email notification once your request has been processed.</p>

            <p>You can check the status of your request at any time by visiting your organization dashboard:</p>

            <a href='{_frontendUrl}/org/dashboard' class='button'>View Dashboard</a>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendUpgradeRequestNotificationToAdminAsync(
        string adminEmail,
        string adminName,
        string organizationName,
        string orgAdminName,
        string currentPlan,
        string requestedPlan,
        string reason)
    {
        var upgradeRequestsUrl = $"{_frontendUrl}/admin/upgrade-requests";
        var subject = $"🔔 New Upgrade Request: {organizationName}";
        var content = $@"
            <p>Hi {adminName},</p>

            <div class='warning-box'>
                <h3 style='margin-top: 0; color: #856404;'>📋 New Upgrade Request Pending Review</h3>
                <p style='margin-bottom: 0;'>An organization has requested a plan upgrade and is awaiting your approval.</p>
            </div>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Request Details:</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Organization:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{organizationName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Requested By:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{orgAdminName}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Current Plan:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{currentPlan}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Requested Plan:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{requestedPlan}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0;' colspan='2'><strong>Reason:</strong><br/>{reason}</td>
                    </tr>
                </table>
            </div>

            <p>Please review this request and take appropriate action:</p>

            <a href='{upgradeRequestsUrl}' class='button'>Review Upgrade Requests</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p><a href='{upgradeRequestsUrl}'>{upgradeRequestsUrl}</a></p>
        ";

        await SendEmailAsync(adminEmail, subject, GetEmailWrapper(content));
    }

    public async Task SendUpgradeRequestApprovedAsync(
        string email,
        string firstName,
        string organizationName,
        string oldPlan,
        string newPlan)
    {
        var subject = $"🎉 Upgrade Approved - {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='success-box'>
                <h3 style='margin-top: 0; color: #155724;'>✅ Your Upgrade Request Has Been Approved!</h3>
                <p style='margin-bottom: 0;'>Great news! Your organization <strong>{organizationName}</strong> has been upgraded successfully.</p>
            </div>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Plan Change:</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Previous Plan:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{oldPlan}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0;'><strong>New Plan:</strong></td>
                        <td style='padding: 8px 0; color: #28a745; font-weight: bold;'>{newPlan} ✨</td>
                    </tr>
                </table>
            </div>

            <p>Your new plan benefits are now active! Enjoy your increased limits and features.</p>

            <a href='{_frontendUrl}/org/dashboard' class='button'>View Your Dashboard</a>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendUpgradeRequestRejectedAsync(
        string email,
        string firstName,
        string organizationName,
        string requestedPlan,
        string rejectionReason)
    {
        var subject = $"Upgrade Request Update - {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='danger-box'>
                <h3 style='margin-top: 0; color: #721c24;'>Upgrade Request Not Approved</h3>
                <p style='margin-bottom: 0;'>We regret to inform you that your upgrade request for <strong>{organizationName}</strong> to the <strong>{requestedPlan}</strong> plan has not been approved at this time.</p>
            </div>

            <div class='highlight-box'>
                <p><strong>Reason:</strong></p>
                <p>{rejectionReason}</p>
            </div>

            <p>If you have any questions about this decision or would like to discuss your upgrade options, please contact our support team.</p>

            <p>You can reach us at:</p>
            <ul>
                <li>Email: support@cktravels.com</li>
            </ul>

            <p>You're welcome to submit a new upgrade request in the future.</p>

            <a href='{_frontendUrl}/org/upgrade' class='button'>View Available Plans</a>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendPlanChangedByAdminAsync(
        string email,
        string firstName,
        string organizationName,
        string oldPlan,
        string newPlan,
        string? adminReason)
    {
        var isUpgrade = true; // Could be determined by comparing plan features
        var subject = $"Plan Update - {organizationName}";
        var content = $@"
            <p>Hi {firstName},</p>

            <div class='info-box'>
                <h3 style='margin-top: 0; color: #0c5460;'>📢 Your Organization's Plan Has Been Updated</h3>
                <p style='margin-bottom: 0;'>An administrator has updated the subscription plan for <strong>{organizationName}</strong>.</p>
            </div>

            <div class='highlight-box'>
                <h3 style='margin-top: 0;'>Plan Change Details:</h3>
                <table style='width: 100%; border-collapse: collapse;'>
                    <tr>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'><strong>Previous Plan:</strong></td>
                        <td style='padding: 8px 0; border-bottom: 1px solid #eee;'>{oldPlan}</td>
                    </tr>
                    <tr>
                        <td style='padding: 8px 0;'><strong>New Plan:</strong></td>
                        <td style='padding: 8px 0;'>{newPlan}</td>
                    </tr>
                    {(string.IsNullOrEmpty(adminReason) ? "" : $@"
                    <tr>
                        <td style='padding: 8px 0; border-top: 1px solid #eee;' colspan='2'><strong>Reason:</strong><br/>{adminReason}</td>
                    </tr>
                    ")}
                </table>
            </div>

            <p>Your plan limits have been updated accordingly. Visit your dashboard to see your current usage and limits.</p>

            <a href='{_frontendUrl}/org/dashboard' class='button'>View Your Dashboard</a>

            <p>If you have any questions about this change, please contact our support team.</p>
        ";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }
}
