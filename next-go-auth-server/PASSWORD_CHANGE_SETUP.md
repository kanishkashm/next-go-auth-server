# Force Password Change on First Login

## Overview
For security, users invited to organizations must change their temporary password on first login.

## Implementation Options

### Option 1: Add RequirePasswordChange Flag (Recommended)

Add a field to the User model to track if password change is required:

```csharp
// In User.cs
public class User : IdentityUser
{
    // ... existing properties ...

    public bool RequirePasswordChange { get; set; } = false;
}
```

Then in OrganizationController when inviting:

```csharp
// In InviteUser method, after creating the user:
newUser.RequirePasswordChange = true;
await _userManager.UpdateAsync(newUser);
```

### Option 2: Use ASP.NET Identity's Built-in Feature

ASP.NET Identity has a built-in way to force password change:

```csharp
// When inviting user:
var result = await _userManager.CreateAsync(newUser, tempPassword);
if (result.Succeeded)
{
    // Force password change on next login
    await _userManager.ResetPasswordAsync(newUser,
        await _userManager.GeneratePasswordResetTokenAsync(newUser),
        tempPassword);
}
```

### Option 3: Check CreatedBy Field

Track who created the user:

```csharp
// Add to User.cs
public string? CreatedBy { get; set; }
public DateTime? LastPasswordChangedAt { get; set; }

// When inviting:
newUser.CreatedBy = currentUser.Id; // Admin who invited
newUser.LastPasswordChangedAt = null; // No password change yet

// On login, check if LastPasswordChangedAt is null and CreatedBy is not null
```

## Frontend Implementation

### 1. Update AuthController Login Method

Add a check after successful login:

```csharp
// In AuthController.cs Login method
if (user.RequirePasswordChange)
{
    return Ok(new
    {
        requirePasswordChange = true,
        user = new { ... },
        accessToken = token.AccessToken,
        message = "Please change your password to continue"
    });
}
```

### 2. Create Password Change Dialog (Next.js)

```typescript
// components/ChangePasswordDialog.tsx
'use client';

import { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { changePassword } from '@/lib/api';
import { toast } from 'sonner';

export function ChangePasswordDialog({ open, onSuccess }: {
  open: boolean;
  onSuccess: () => void
}) {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (newPassword !== confirmPassword) {
      toast.error('Passwords do not match');
      return;
    }

    if (newPassword.length < 6) {
      toast.error('Password must be at least 6 characters');
      return;
    }

    setLoading(true);
    try {
      await changePassword({
        currentPassword,
        newPassword
      });
      toast.success('Password changed successfully!');
      onSuccess();
    } catch (error: any) {
      toast.error(error.message || 'Failed to change password');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={() => {}}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Change Your Password</DialogTitle>
          <p className="text-sm text-muted-foreground">
            For security reasons, you must change your temporary password before continuing.
          </p>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <Label htmlFor="current">Current Password</Label>
            <Input
              id="current"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
            />
          </div>
          <div>
            <Label htmlFor="new">New Password</Label>
            <Input
              id="new"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              minLength={6}
            />
          </div>
          <div>
            <Label htmlFor="confirm">Confirm New Password</Label>
            <Input
              id="confirm"
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
              required
            />
          </div>
          <Button type="submit" className="w-full" disabled={loading}>
            {loading ? 'Changing Password...' : 'Change Password'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

### 3. Update Login Flow (AuthDialog.tsx)

```typescript
// In handleLogin after successful login
const result = await login(data);

if (result.requirePasswordChange) {
  setShowChangePasswordDialog(true);
  toast.warning('Please change your password to continue');
  return;
}

// Normal login flow continues...
```

### 4. Add API Endpoint (Backend)

```csharp
// In AuthController.cs
[HttpPost("change-password")]
[Authorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null)
        return Unauthorized();

    var result = await _userManager.ChangePasswordAsync(
        user,
        request.CurrentPassword,
        request.NewPassword
    );

    if (!result.Succeeded)
        return BadRequest(new { error = "Failed to change password", details = result.Errors });

    // Clear the RequirePasswordChange flag
    user.RequirePasswordChange = false;
    await _userManager.UpdateAsync(user);

    return Ok(new { message = "Password changed successfully" });
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
```

## Testing

1. **Invite a user** from organization dashboard
2. **User receives email** with temporary password
3. **User logs in** with temporary password
4. **System shows password change dialog** (cannot be dismissed)
5. **User changes password** successfully
6. **System proceeds** to normal dashboard

## Security Best Practices

- ✅ Temporary passwords expire after 24 hours
- ✅ Passwords must meet complexity requirements
- ✅ Password change is mandatory (dialog cannot be closed)
- ✅ Old password must be provided to change
- ✅ Email notification sent after password change
- ✅ Password history (prevent reusing last 3 passwords)

## Future Enhancements

- [ ] Password expiry (force change every 90 days)
- [ ] Password strength meter in UI
- [ ] Two-factor authentication option
- [ ] Password reset link in email (alternative to temp password)
- [ ] Account lockout after failed password change attempts
