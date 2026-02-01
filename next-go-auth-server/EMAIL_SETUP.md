# Email Configuration Setup

## Overview
The system sends email notifications for:
- User invitations to organizations (with temporary password)
- Future: Password reset, account notifications, etc.

## Configuration

Add the following settings to your `appsettings.json` or `appsettings.Development.json`:

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@ckhrc.com",
    "FromName": "CKHRC Immigration Platform"
  },
  "App": {
    "FrontendUrl": "http://localhost:3000"
  }
}
```

## Gmail Setup (Recommended for Development)

### Option 1: Using Gmail App Password (Recommended)

1. **Enable 2-Factor Authentication** on your Gmail account
2. **Generate App Password:**
   - Go to [Google Account Security](https://myaccount.google.com/security)
   - Select "2-Step Verification"
   - Scroll to "App passwords"
   - Generate a new app password for "Mail"
   - Copy the 16-character password
3. **Update Configuration:**
   ```json
   {
     "Email": {
       "SmtpHost": "smtp.gmail.com",
       "SmtpPort": "587",
       "SmtpUsername": "your-email@gmail.com",
       "SmtpPassword": "your-16-char-app-password",
       "FromEmail": "your-email@gmail.com",
       "FromName": "CKHRC Immigration Platform"
     }
   }
   ```

### Option 2: Using SendGrid (Recommended for Production)

SendGrid offers 100 emails/day for free, which is great for production use.

1. **Sign up at [SendGrid](https://sendgrid.com/)**
2. **Create an API Key:**
   - Go to Settings → API Keys
   - Create a new API key with "Mail Send" permissions
   - Copy the API key
3. **Update Configuration:**
   ```json
   {
     "Email": {
       "SmtpHost": "smtp.sendgrid.net",
       "SmtpPort": "587",
       "SmtpUsername": "apikey",
       "SmtpPassword": "YOUR_SENDGRID_API_KEY",
       "FromEmail": "noreply@yourdomain.com",
       "FromName": "CKHRC Immigration Platform"
     }
   }
   ```

### Option 3: Using Mailtrap (Best for Testing)

Mailtrap captures all emails in a sandbox for testing.

1. **Sign up at [Mailtrap.io](https://mailtrap.io/)**
2. **Get SMTP credentials from your inbox**
3. **Update Configuration:**
   ```json
   {
     "Email": {
       "SmtpHost": "sandbox.smtp.mailtrap.io",
       "SmtpPort": "2525",
       "SmtpUsername": "your-mailtrap-username",
       "SmtpPassword": "your-mailtrap-password",
       "FromEmail": "test@example.com",
       "FromName": "CKHRC Immigration Platform"
     }
   }
   ```

## Email Template Features

The invitation email includes:
- ✅ Professional HTML design with gradient header
- ✅ Clear display of temporary password
- ✅ Security warning about password change requirement
- ✅ Direct login button
- ✅ Organization name and user's first name personalization
- ✅ Responsive design

## Testing Email Functionality

1. **Invite a new user** from the organization dashboard
2. **Check the email inbox** (or Mailtrap if using for testing)
3. **User should receive:**
   - Welcome message
   - Organization name
   - Temporary password
   - Login link
   - Security notice about password change

## Security Notes

- ⚠️ **Never commit email passwords to Git**
- ✅ Use environment variables for production:
  ```bash
  export Email__SmtpPassword="your-password"
  ```
- ✅ Use app-specific passwords for Gmail
- ✅ Temporary passwords are automatically generated with:
  - 12 characters
  - Uppercase, lowercase, digits, and special characters
  - Cryptographically random

## Troubleshooting

### Email not sending?
1. Check SMTP credentials are correct
2. Check firewall isn't blocking port 587
3. Check logs for error messages
4. Verify "Less secure app access" is enabled (for Gmail without 2FA)

### Email going to spam?
1. Use a verified domain email address
2. Set up SPF and DKIM records
3. Use a dedicated email service like SendGrid
4. Add a physical address in the footer

## Future Enhancements

- [ ] Email templates for password reset
- [ ] Email templates for organization approval/rejection
- [ ] Email queue system for bulk operations
- [ ] Email tracking and delivery status
- [ ] Customizable email templates per organization
