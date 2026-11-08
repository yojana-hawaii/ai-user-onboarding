param([switch]$Apply)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Credentials stay in process memory and are never written to output or files.
$previousPrompt = $env:GIT_TERMINAL_PROMPT
$previousInteractive = $env:GCM_INTERACTIVE
$credential = @{}
$headers = @{}
$credentialLines = $null
try {
    $env:GIT_TERMINAL_PROMPT = '0'
    $env:GCM_INTERACTIVE = 'Never'
    $credentialLines = "protocol=https`nhost=github.com`n`n" | git credential fill 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'No noninteractive GitHub credential is available.' }
    foreach ($line in $credentialLines) {
        $parts = $line.Split('=',2)
        if ($parts.Count -eq 2) { $credential[$parts[0]] = $parts[1] }
    }
    if (-not $credential['password']) { throw 'GitHub credential is unavailable.' }
    $headers = @{Authorization='Bearer '+$credential['password']; Accept='application/vnd.github+json'; 'X-GitHub-Api-Version'='2022-11-28'}
    $api = 'https://api.github.com/repos/yojana-hawaii/ai-user-onboarding'
    $repository = Invoke-RestMethod $api -Headers $headers
    if ($Apply -and -not $repository.permissions.admin) { throw 'Repository administrator access is required.' }
    if ($Apply) {
        Invoke-RestMethod "$api/vulnerability-alerts" -Headers $headers -Method Put | Out-Null
        Invoke-RestMethod "$api/automated-security-fixes" -Headers $headers -Method Put | Out-Null
        $workflowPermissions = @{default_workflow_permissions='read'; can_approve_pull_request_reviews=$false} | ConvertTo-Json
        Invoke-RestMethod "$api/actions/permissions/workflow" -Headers $headers -Method Put -ContentType application/json -Body $workflowPermissions | Out-Null

        $branch = $repository.default_branch
        $existing = $null
        try { $existing = Invoke-RestMethod "$api/branches/$branch/protection" -Headers $headers }
        catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw 'Cannot inspect existing branch protection.' } }
        if ($existing) {
            Write-Warning 'Existing branch protection was preserved; review it manually rather than overwrite a custom policy.'
        }
        else {
            $collaborators = @(Invoke-RestMethod "$api/collaborators" -Headers $headers)
            $reviewerCount = @($collaborators | Where-Object { $_.permissions.push -and $_.type -ne 'Bot' }).Count
            $approvals = if ($reviewerCount -ge 2) { 1 } else { 0 }
            $actionsApp = Invoke-RestMethod 'https://api.github.com/apps/github-actions' -Headers $headers
            $protection = @{
                required_status_checks=@{strict=$true; checks=@(
                    @{context='Build, test and format'; app_id=$actionsApp.id},
                    @{context='Security checks'; app_id=$actionsApp.id}
                )}
                enforce_admins=$true
                required_pull_request_reviews=@{dismiss_stale_reviews=$true; require_code_owner_reviews=$false; required_approving_review_count=$approvals; require_last_push_approval=$false}
                restrictions=$null
                required_linear_history=$true
                allow_force_pushes=$false
                allow_deletions=$false
                required_conversation_resolution=$true
            } | ConvertTo-Json -Depth 8
            Invoke-RestMethod "$api/branches/$branch/protection" -Headers $headers -Method Put -ContentType application/json -Body $protection | Out-Null
            Write-Output "$branch now requires a pull request, current build/security checks, resolved conversations and linear history; force pushes/deletion are disabled, including for admins."
            if ($approvals -eq 0) { Write-Warning 'Only one collaborator has write access. Independent approval is not required yet; add a reviewer and increase the approval requirement when available.' }
        }
    }
    $security = (Invoke-RestMethod $api -Headers $headers).security_and_analysis
    $permissions = Invoke-RestMethod "$api/actions/permissions/workflow" -Headers $headers
    [pscustomobject]@{
        Repository=$repository.full_name
        Public=(-not $repository.private)
        SecretScanning=$security.secret_scanning.status
        PushProtection=$security.secret_scanning_push_protection.status
        DependabotSecurityUpdates=$security.dependabot_security_updates.status
        DefaultWorkflowToken=$permissions.default_workflow_permissions
        WorkflowsCanApprovePullRequests=$permissions.can_approve_pull_request_reviews
    } | ConvertTo-Json
}
catch {
    # Do not render request objects, headers, raw credential responses or exception bodies.
    Write-Error 'GitHub security configuration failed. No credentials were displayed. Inspect repository permissions and the documented API endpoints.' -ErrorAction Continue
    exit 1
}
finally {
    $headers.Clear(); $credential.Clear(); $credentialLines=$null
    $env:GIT_TERMINAL_PROMPT=$previousPrompt
    $env:GCM_INTERACTIVE=$previousInteractive
}
