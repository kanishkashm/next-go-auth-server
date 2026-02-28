using System.Drawing;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

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
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>CK Orbits Immigration Platform</title>
    <!--[if mso]>
    <style type='text/css'>
        body, table, td, p, h1, h2, h3, h4, h5, h6, a, div, span {{font - family: Arial, Helvetica, sans-serif !important; }}
    </style>
    <![endif]-->
    <style>
        body {{margin: 0; padding: 0; min-width: 100%; width: 100% !important; height: 100% !important; background-color: #FCFCFE; font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; }}
        table {{border - spacing: 0; border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }}
        td {{padding: 0; vertical-align: top; }}
        img {{border: 0; -ms-interpolation-mode: bicubic; }}
        a {{text - decoration: none; color: #FFB80A; }}
        
        .container {{width: 100%; max-width: 600px; margin: 0 auto; background-color: #ffffff; }}
        .header {{background - color: #FFB80A; padding: 40px 20px; text-align: center; }}
        .header h1 {{margin: 0; color: #09090A; font-size: 26px; font-weight: 700; letter-spacing: -0.5px; }}
        
        .content {{padding: 40px 30px; color: #09090A; line-height: 1.6; font-size: 16px; }}
        
        .footer {{padding: 30px 20px; text-align: center; color: #71717A; font-size: 13px; border-top: 1px solid #E5E7EB; }}
        .footer p {{margin: 8px 0; }}
        
        @media screen and (max-width: 600px) {{
            .container {{ width: 100% !important; }}
            .content {{ padding: 30px 20px !important; }}
        }}
    </style>
</head>
<body style='margin: 0; padding: 0; background-color: #FCFCFE;'>
    <!--[if (gte mso 9)|(IE)]>
    <table align='center' border='0' cellspacing='0' cellpadding='0' width='600'>
    <tr>
    <td align='center' valign='top' width='600'>
    <![endif]-->
    <table class='container' align='center' border='0' cellpadding='0' cellspacing='0'>
        <tr>
            <td class='header' style='background-color: #FFB80A; padding: 40px 20px; text-align: center;'>
                <h1 style='margin: 0; color: #09090A; font-family: Arial, sans-serif;'>CK Orbits Immigration Platform</h1>
            </td>
        </tr>
        <tr>
            <td class='content' style='padding: 40px 30px; font-family: Arial, sans-serif; color: #09090A;'>
                {content}
                <div style='margin-top: 30px; border-top: 1px solid #E5E7EB; padding-top: 20px;'>
                    <p style='margin: 0; color: #71717A; font-family: Arial, sans-serif;'>Best regards,</p>
                    <p style='margin: 5px 0 0 0; font-weight: 600; color: #09090A; font-family: Arial, sans-serif;'>CK Orbits Immigration Team</p>
                </div>
            </td>
        </tr>
        <tr>
            <td class='footer' style='padding: 30px 20px; background-color: #FCFCFE; color: #71717A; font-family: Arial, sans-serif; border-top: 1px solid #E5E7EB;'>
                <p style='margin: 8px 0; font-family: Arial, sans-serif;'>This is an automated email. Please do not reply directly to this message.</p>
                <p style='margin: 8px 0; font-family: Arial, sans-serif;'>If you need assistance, please contact <a href='mailto:support@cktravels.com' style='color: #FFB80A; text-decoration: none;'>support@cktravels.com</a></p>
            </td>
        </tr>
    </table>
    <!--[if (gte mso 9)|(IE)]>
    </td>
    </tr>
    </table>
    <![endif]-->
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
            <p style='font-family: Arial, sans-serif;'>Hi {superAdminName},</p>

<!-- Warning Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFF7ED; padding: 20px; border-left: 4px solid #F97316; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #9A3412; font-size: 18px; font-family: Arial, sans-serif;'>📋 New Organization Registration Pending Approval</h3>
            <p style='margin: 0; color: #9A3412; font-family: Arial, sans-serif;'>A new organization has registered and is awaiting your approval.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Registration Details:</h3>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Organization Name:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{requestedOrgName}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Applicant Name:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{applicantName}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'><strong>Applicant Email:</strong></td>
                    <td style='padding: 10px 0; font-family: Arial, sans-serif;'><a href='mailto:{applicantEmail}' style='color: #D97706; text-decoration: underline;'>{applicantEmail}</a></td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Please review this registration and take appropriate action:</p>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{approvalUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Review & Approve/Reject</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-size: 14px; color: #71717A; font-family: Arial, sans-serif;'>If the button doesn't work, copy and paste this link into your browser:</p>
<p style='font-size: 14px; word-break: break-all; font-family: Arial, sans-serif;'><a href='{approvalUrl}' style='color: #FFB80A;'>{approvalUrl}</a></p>

<!-- Info Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 30px 0 0 0;'>
    <tr>
        <td style='background-color: #EFF6FF; padding: 15px; border-left: 4px solid #3B82F6; font-family: Arial, sans-serif;'>
            <p style='margin: 0; color: #1E40AF; font-size: 14px; font-family: Arial, sans-serif;'><strong>💡 Tip:</strong> Make sure you're logged in as a SuperAdmin before clicking the link.</p>
        </td>
    </tr>
</table>";

        await SendEmailAsync(superAdminEmail, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // Organization Admin Approval/Rejection
    // =====================================================

    public async Task SendOrgAdminApprovalEmailAsync(string email, string firstName, string organizationName)
    {
        var subject = $"🎉 Your Organization Has Been Approved - {organizationName}";
        var content = $@"
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Success Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #F0FDF4; padding: 20px; border-left: 4px solid #22C55E; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #166534; font-size: 18px; font-family: Arial, sans-serif;'>✅ Great news! Your organization has been approved!</h3>
            <p style='margin: 0; color: #166534; font-family: Arial, sans-serif;'>Your organization <strong>{organizationName}</strong> is now active on the CK Orbits Immigration Platform.</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>You now have access to:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Upload and analyze CVs for immigration eligibility</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Invite team members to your organization</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Manage your organization's subscription</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>View detailed immigration pathway reports</td></tr>
</table>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Log In to Your Dashboard</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<!-- Info Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #EFF6FF; padding: 20px; border-left: 4px solid #3B82F6; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #1E40AF; font-family: Arial, sans-serif;'><strong>📊 Your Starter Plan includes:</strong></p>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr><td width='20' valign='top' style='color: #1E40AF; font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 5px; color: #1E40AF; font-family: Arial, sans-serif;'>Up to 5 team members</td></tr>
                <tr><td width='20' valign='top' style='color: #1E40AF; font-family: Arial, sans-serif;'>•</td><td style='color: #1E40AF; font-family: Arial, sans-serif;'>50 CV analyses per month</td></tr>
            </table>
        </td>
    </tr>
</table>";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendOrgAdminRejectionEmailAsync(string email, string firstName, string reason)
    {
        var subject = "Organization Registration Update";
        var content = $@"
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Danger Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FEF2F2; padding: 20px; border-left: 4px solid #EF4444; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #991B1B; font-size: 18px; font-family: Arial, sans-serif;'>Organization Registration Not Approved</h3>
            <p style='margin: 0; color: #991B1B; font-family: Arial, sans-serif;'>We regret to inform you that your organization registration has not been approved at this time.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #92400E; font-family: Arial, sans-serif;'><strong>Reason:</strong></p>
            <p style='margin: 0; color: #92400E; font-family: Arial, sans-serif;'>{reason}</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>If you believe this decision was made in error or would like to provide additional information, please contact our support team.</p>

<p style='font-family: Arial, sans-serif;'>You can reach us at:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Email: <a href='mailto:support@cktravels.com' style='color: #FFB80A; text-decoration: none;'>support@cktravels.com</a></td></tr>
</table>

<p style='font-family: Arial, sans-serif;'>We appreciate your interest in CK Orbits Immigration Platform and hope to assist you in the future.</p>";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    // =====================================================
    // User Invitation
    // =====================================================

    public async Task SendInvitationEmailAsync(string email, string firstName, string organizationName, string tempPassword)
    {
        var subject = $"You've been invited to join {organizationName}";
        var content = $@"
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<p style='font-family: Arial, sans-serif;'>You've been invited to join <strong>{organizationName}</strong> on the CK Orbits Immigration Platform.</p>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Your Login Credentials:</h3>
            <p style='margin-top: 0; margin-bottom: 10px; color: #92400E; font-family: Arial, sans-serif;'><strong>Email:</strong> {email}</p>
            <p style='margin: 0; color: #92400E; font-family: Arial, sans-serif;'><strong>Temporary Password:</strong> <code style='background: #FEF3C7; color: #78350F; padding: 6px 12px; border-radius: 4px; font-size: 18px; font-weight: 600;'>{tempPassword}</code></p>
        </td>
    </tr>
</table>

<!-- Warning Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFF7ED; padding: 20px; border-left: 4px solid #F97316; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #9A3412; font-family: Arial, sans-serif;'><strong>⚠️ Important Security Notice:</strong></p>
            <p style='margin: 0; color: #9A3412; font-family: Arial, sans-serif;'>This is a temporary password. For security reasons, please change your password after your first login.</p>
        </td>
    </tr>
</table>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Log In Now</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-size: 14px; color: #71717A; font-family: Arial, sans-serif;'>If the button doesn't work, copy and paste this link into your browser:</p>
<p style='font-size: 14px; word-break: break-all; font-family: Arial, sans-serif;'><a href='{_frontendUrl}' style='color: #FFB80A;'>{_frontendUrl}</a></p>
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
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Danger Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FEF2F2; padding: 20px; border-left: 4px solid #EF4444; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #991B1B; font-size: 18px; font-family: Arial, sans-serif;'>⚠️ Account Deactivated</h3>
            <p style='margin: 0; color: #991B1B; font-family: Arial, sans-serif;'>Your account on CK Orbits Immigration Platform has been deactivated.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #92400E; font-family: Arial, sans-serif;'><strong>Reason for deactivation:</strong></p>
            <p style='margin: 0; color: #92400E; font-family: Arial, sans-serif;'>{reason}</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>While your account is deactivated, you will not be able to:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Log in to the platform</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Upload or analyze CVs</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Access your previous reports</td></tr>
</table>

<p style='font-family: Arial, sans-serif;'>If you believe this was done in error or would like to appeal this decision, please contact our support team:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Email: <a href='mailto:support@cktravels.com' style='color: #FFB80A; text-decoration: none;'>support@cktravels.com</a></td></tr>
</table>
";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendAccountReactivatedEmailAsync(string email, string firstName)
    {
        var subject = "🎉 Your Account Has Been Reactivated";
        var content = $@"
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Success Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #F0FDF4; padding: 20px; border-left: 4px solid #22C55E; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #166534; font-size: 18px; font-family: Arial, sans-serif;'>✅ Great news! Your account has been reactivated!</h3>
            <p style='margin: 0; color: #166534; font-family: Arial, sans-serif;'>You now have full access to the CK Orbits Immigration Platform again.</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>You can now:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Log in to your account</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Upload and analyze CVs</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Access all your previous reports and data</td></tr>
</table>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Log In Now</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Thank you for your patience. If you have any questions, please don't hesitate to contact us.</p>
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
           <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Danger Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FEF2F2; padding: 20px; border-left: 4px solid #EF4444; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #991B1B; font-size: 18px; font-family: Arial, sans-serif;'>⚠️ Organization Deactivated</h3>
            <p style='margin: 0; color: #991B1B; font-family: Arial, sans-serif;'>Your organization <strong>{organizationName}</strong> on CK Orbits Immigration Platform has been deactivated.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #92400E; font-family: Arial, sans-serif;'><strong>Reason for deactivation:</strong></p>
            <p style='margin: 0; color: #92400E; font-family: Arial, sans-serif;'>{reason}</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>While your organization is deactivated:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>All organization members will be unable to log in</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>No new CV uploads can be made</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Existing data is preserved but not accessible</td></tr>
</table>

<p style='font-family: Arial, sans-serif;'>If you are the organization administrator and believe this was done in error, please contact our support team immediately:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Email: <a href='mailto:support@cktravels.com' style='color: #FFB80A; text-decoration: none;'>support@cktravels.com</a></td></tr>
</table>
";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }

    public async Task SendOrganizationReactivatedEmailAsync(string email, string firstName, string organizationName)
    {
        var subject = $"🎉 {organizationName} Has Been Reactivated";
        var content = $@"
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Success Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #F0FDF4; padding: 20px; border-left: 4px solid #22C55E; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #166534; font-size: 18px; font-family: Arial, sans-serif;'>✅ Great news! Your organization has been reactivated!</h3>
            <p style='margin: 0; color: #166534; font-family: Arial, sans-serif;'>Your organization <strong>{organizationName}</strong> is now active again on the CK Orbits Immigration Platform.</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>You and all organization members can now:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Log in to the platform</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='padding-bottom: 8px; font-family: Arial, sans-serif;'>Upload and analyze CVs</td></tr>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Access all previous reports and data</td></tr>
</table>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Log In Now</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Thank you for your patience. If you have any questions, please don't hesitate to contact us.</p>
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
           <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Info Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #EFF6FF; padding: 20px; border-left: 4px solid #3B82F6; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #1E40AF; font-size: 18px; font-family: Arial, sans-serif;'>📤 Upgrade Request Submitted</h3>
            <p style='margin: 0; color: #1E40AF; font-family: Arial, sans-serif;'>Your upgrade request for <strong>{organizationName}</strong> has been submitted successfully.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Request Details:</h3>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Current Plan:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{currentPlan}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'><strong>Requested Plan:</strong></td>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'>{requestedPlan}</td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Our team will review your request and get back to you shortly. You will receive an email notification once your request has been processed.</p>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}/org/dashboard' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>View Dashboard</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>";

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
          <p style='font-family: Arial, sans-serif;'>Hi {adminName},</p>

<!-- Warning Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFF7ED; padding: 20px; border-left: 4px solid #F97316; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #9A3412; font-size: 18px; font-family: Arial, sans-serif;'>📋 New Upgrade Request Pending Review</h3>
            <p style='margin: 0; color: #9A3412; font-family: Arial, sans-serif;'>An organization has requested a plan upgrade and is awaiting your approval.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Request Details:</h3>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Organization:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{organizationName}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Requested By:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{orgAdminName}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Current Plan:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{currentPlan}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Requested Plan:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{requestedPlan}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;' colspan='2'><strong>Reason:</strong><br/>{reason}</td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{upgradeRequestsUrl}' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>Review Upgrade Requests</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-size: 14px; color: #71717A; font-family: Arial, sans-serif;'>If the button doesn't work, copy and paste this link into your browser:</p>
<p style='font-size: 14px; word-break: break-all; font-family: Arial, sans-serif;'><a href='{upgradeRequestsUrl}' style='color: #FFB80A;'>{upgradeRequestsUrl}</a></p>";

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
            <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Success Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #F0FDF4; padding: 20px; border-left: 4px solid #22C55E; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #166534; font-size: 18px; font-family: Arial, sans-serif;'>✅ Your Upgrade Request Has Been Approved!</h3>
            <p style='margin: 0; color: #166534; font-family: Arial, sans-serif;'>Great news! Your organization <strong>{organizationName}</strong> has been upgraded successfully.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Plan Change:</h3>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Previous Plan:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{oldPlan}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'><strong>New Plan:</strong></td>
                    <td style='padding: 10px 0; color: #166534; font-weight: bold; font-family: Arial, sans-serif;'>{newPlan} ✨</td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Your new plan benefits are now active! Enjoy your increased limits and features.</p>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}/org/dashboard' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>View Your Dashboard</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>";

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
         <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Danger Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FEF2F2; padding: 20px; border-left: 4px solid #EF4444; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #991B1B; font-size: 18px; font-family: Arial, sans-serif;'>Upgrade Request Not Approved</h3>
            <p style='margin: 0; color: #991B1B; font-family: Arial, sans-serif;'>We regret to inform you that your upgrade request for <strong>{organizationName}</strong> to the <strong>{requestedPlan}</strong> plan has not been approved at this time.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <p style='margin-top: 0; margin-bottom: 10px; color: #92400E; font-family: Arial, sans-serif;'><strong>Reason:</strong></p>
            <p style='margin: 0; color: #92400E; font-family: Arial, sans-serif;'>{rejectionReason}</p>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>If you have any questions about this decision or would like to discuss your upgrade options, please contact our support team.</p>

<p style='font-family: Arial, sans-serif;'>You can reach us at:</p>
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin-bottom: 24px;'>
    <tr><td width='20' valign='top' style='font-family: Arial, sans-serif;'>•</td><td style='font-family: Arial, sans-serif;'>Email: <a href='mailto:support@cktravels.com' style='color: #FFB80A; text-decoration: none;'>support@cktravels.com</a></td></tr>
</table>

<p style='font-family: Arial, sans-serif;'>You're welcome to submit a new upgrade request in the future.</p>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}/org/upgrade' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>View Available Plans</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>";

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
          <p style='font-family: Arial, sans-serif;'>Hi {firstName},</p>

<!-- Info Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #EFF6FF; padding: 20px; border-left: 4px solid #3B82F6; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 10px; color: #1E40AF; font-size: 18px; font-family: Arial, sans-serif;'>📢 Your Organization's Plan Has Been Updated</h3>
            <p style='margin: 0; color: #1E40AF; font-family: Arial, sans-serif;'>An administrator has updated the subscription plan for <strong>{organizationName}</strong>.</p>
        </td>
    </tr>
</table>

<!-- Highlight Alert Box -->
<table width='100%' cellpadding='0' cellspacing='0' border='0' style='margin: 24px 0;'>
    <tr>
        <td style='background-color: #FFFBEB; padding: 20px; border-left: 4px solid #FFB80A; font-family: Arial, sans-serif;'>
            <h3 style='margin-top: 0; margin-bottom: 15px; color: #92400E; font-size: 18px; font-family: Arial, sans-serif;'>Plan Change Details:</h3>
            <table width='100%' cellpadding='0' cellspacing='0' border='0'>
                <tr>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'><strong>Previous Plan:</strong></td>
                    <td style='padding: 10px 0; border-bottom: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;'>{oldPlan}</td>
                </tr>
                <tr>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'><strong>New Plan:</strong></td>
                    <td style='padding: 10px 0; color: #92400E; font-family: Arial, sans-serif;'>{newPlan}</td>
                </tr>
                {(string.IsNullOrEmpty(adminReason) ? "" : $@"
                <tr>
                    <td style='padding: 10px 0; border-top: 1px solid #FEF3C7; color: #92400E; font-family: Arial, sans-serif;' colspan='2'><strong>Reason:</strong><br/>{adminReason}</td>
                </tr>
                ")}
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>Your plan limits have been updated accordingly. Visit your dashboard to see your current usage and limits.</p>

<!-- Bulletproof Button -->
<table width='100%' border='0' cellspacing='0' cellpadding='0' style='margin: 30px 0;'>
    <tr>
        <td align='center'>
            <table border='0' cellspacing='0' cellpadding='0'>
                <tr>
                    <td align='center' bgcolor='#FFB80A' style='border-radius: 6px;'>
                        <a href='{_frontendUrl}/org/dashboard' style='display: inline-block; padding: 14px 32px; font-family: Arial, sans-serif; font-size: 16px; color: #09090A; text-decoration: none; font-weight: bold; border-radius: 6px; border: 1px solid #FFB80A;'>View Your Dashboard</a>
                    </td>
                </tr>
            </table>
        </td>
    </tr>
</table>

<p style='font-family: Arial, sans-serif;'>If you have any questions about this change, please contact our support team.</p>
";

        await SendEmailAsync(email, subject, GetEmailWrapper(content));
    }
}
