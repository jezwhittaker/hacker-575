<#
.SYNOPSIS
  One-time setup for deploying OctoCAT Supply to Azure from GitHub Actions with OIDC.

.DESCRIPTION
  Creates what Terraform itself cannot create for itself:
    1. A resource group + storage account + container for the Terraform remote state.
    2. An Entra ID app registration with a federated credential for the GitHub
       environment (no client secret is created).
    3. Role assignments for that identity.
    4. The GitHub environment plus the secrets and variables the workflow reads.

  Requires: Azure CLI (az login) and GitHub CLI (gh auth login). Safe to re-run.

.EXAMPLE
  ./infra/bootstrap/bootstrap.ps1 -Repo jezwhittaker/hacker-575
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Repo,
    [string] $Location = 'uksouth',
    [string] $Environment = 'dev',
    [string] $StateResourceGroup = 'rg-octocat-tfstate',
    [string] $AppDisplayName = 'octocat-github-deploy'
)

$ErrorActionPreference = 'Stop'

function Invoke-Az {
    $output = & az @args
    if ($LASTEXITCODE -ne 0) { throw "az $($args -join ' ') failed" }
    $output
}

$account = Invoke-Az account show -o json | ConvertFrom-Json
$subscriptionId = $account.id
$tenantId = $account.tenantId
$scope = "/subscriptions/$subscriptionId"
Write-Host "Subscription: $($account.name) ($subscriptionId)"

# Storage account names are global, so derive a stable suffix from the subscription id.
$suffix = ([System.BitConverter]::ToString(
        [System.Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($subscriptionId))
    ) -replace '-', '').Substring(0, 6).ToLower()
$stateAccount = "stoctocattf$suffix"
$stateContainer = 'tfstate'

Write-Host "==> Terraform state storage ($stateAccount)"
Invoke-Az group create -n $StateResourceGroup -l $Location -o none
Invoke-Az storage account create -n $stateAccount -g $StateResourceGroup -l $Location `
    --sku Standard_LRS --min-tls-version TLS1_2 --allow-blob-public-access false `
    --allow-shared-key-access false -o none
$stateAccountId = (Invoke-Az storage account show -n $stateAccount -g $StateResourceGroup --query id -o tsv)
# Management-plane call, so it works with shared keys disabled and without a data role.
Invoke-Az storage container-rm create --storage-account $stateAccount -g $StateResourceGroup -n $stateContainer -o none

Write-Host '==> Entra ID app registration with GitHub OIDC federation'
$clientId = (Invoke-Az ad app list --display-name $AppDisplayName --query '[0].appId' -o tsv)
if (-not $clientId) {
    $clientId = (Invoke-Az ad app create --display-name $AppDisplayName --query appId -o tsv)
}
if (-not (Invoke-Az ad sp list --filter "appId eq '$clientId'" --query '[0].id' -o tsv)) {
    Invoke-Az ad sp create --id $clientId -o none
}
$spObjectId = (Invoke-Az ad sp show --id $clientId --query id -o tsv)

$subject = "repo:${Repo}:environment:${Environment}"
$existing = Invoke-Az ad app federated-credential list --id $clientId --query "[?subject=='$subject'].name" -o tsv
if (-not $existing) {
    $credential = @{
        name      = "github-$Environment"
        issuer    = 'https://token.actions.githubusercontent.com'
        subject   = $subject
        audiences = @('api://AzureADTokenExchange')
    } | ConvertTo-Json -Compress
    $file = New-TemporaryFile
    Set-Content -Path $file -Value $credential
    Invoke-Az ad app federated-credential create --id $clientId --parameters "@$file" -o none
    Remove-Item $file
}

Write-Host '==> Role assignments'
# Contributor creates the resource group and resources. Role Based Access Control Administrator
# lets Terraform grant the AcrPull role to the app identity. Both are scoped to the subscription
# because Terraform creates the resource group itself.
$assignments = @(
    @{ Role = 'Contributor'; Scope = $scope },
    @{ Role = 'Role Based Access Control Administrator'; Scope = $scope },
    @{ Role = 'Storage Blob Data Contributor'; Scope = $stateAccountId }
)
foreach ($a in $assignments) {
    $has = Invoke-Az role assignment list --assignee $spObjectId --role $a.Role --scope $a.Scope --query '[0].id' -o tsv
    if (-not $has) {
        Invoke-Az role assignment create --assignee-object-id $spObjectId --assignee-principal-type ServicePrincipal `
            --role $a.Role --scope $a.Scope -o none
    }
}

Write-Host '==> GitHub environment, secrets and variables'
& gh api -X PUT "repos/$Repo/environments/$Environment" --silent
if ($LASTEXITCODE -ne 0) { throw 'Could not create the GitHub environment' }
$clientId | & gh secret set AZURE_CLIENT_ID --repo $Repo --env $Environment
$tenantId | & gh secret set AZURE_TENANT_ID --repo $Repo --env $Environment
$subscriptionId | & gh secret set AZURE_SUBSCRIPTION_ID --repo $Repo --env $Environment
& gh variable set TFSTATE_RESOURCE_GROUP --repo $Repo --env $Environment --body $StateResourceGroup
& gh variable set TFSTATE_STORAGE_ACCOUNT --repo $Repo --env $Environment --body $stateAccount
& gh variable set TFSTATE_CONTAINER --repo $Repo --env $Environment --body $stateContainer

Write-Host "`nDone. Run the 'Deploy to Azure' workflow (or push to master)."
