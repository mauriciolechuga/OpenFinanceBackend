<#
.SYNOPSIS
    Seeds the OpenFinance dev database with deterministic MockProvider data.

.DESCRIPTION
    The aggregation database is empty until a connection is linked, because the MockProvider
    is the (fake) data source and AggregationService only persists rows on link/sync. This
    script drives the EXISTING public API against a running dev backend to inject data:

        1. Log in as a test account to obtain a JWT.
        2. Ensure the banks referenced by the app exist (POST /bankslist/addBanks).
        3. POST /connections/exchange -> triggers a MockProvider sync that persists
           ~3 accounts, 2 holdings (VFV.TO, XEQT.TO), 4 transactions, and a balance snapshot.

    No schema or backend code changes are involved. By default the exchange step is skipped
    if the account already has accounts (so re-running is safe); pass -Force to link again
    (each exchange creates an additional connection's worth of data).

    Prereq: the backend must be running in Development on $BaseUrl. In a fresh terminal:
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        dotnet run --project WebAPI.OpenFinance

.EXAMPLE
    ./scripts/seed-dev-data.ps1
    ./scripts/seed-dev-data.ps1 -BaseUrl http://localhost:5280 -Email test2@lechuga.cc
    ./scripts/seed-dev-data.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]   $BaseUrl  = "http://localhost:5280",
    [string]   $Email    = "test@lechuga.cc",
    [string]   $Password = "TestOpenFinance1!",
    [int]      $BankId   = 3,
    [switch]   $Force
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

# Banks the Flutter app expects (matches its offline fallback list).
$DefaultBanks = @(
    @{ bankID = 1; bankName = "TD Bank" },
    @{ bankID = 2; bankName = "Scotiabank" },
    @{ bankID = 3; bankName = "Royal Bank of Canada" }
)

function Invoke-Api {
    param(
        [string] $Method,
        [string] $Path,
        [string] $Token,
        $Body
    )
    $headers = @{ "Accept" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }

    $args = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($null -ne $Body) {
        $args["ContentType"] = "application/json"
        $args["Body"]        = (ConvertTo-Json $Body -Depth 6)
    }
    return Invoke-RestMethod @args
}

Write-Host "Seeding OpenFinance dev data via $BaseUrl" -ForegroundColor Cyan

# --- 1. Authenticate ---
Write-Host "-> Logging in as $Email ..."
try {
    $auth = Invoke-Api -Method POST -Path "/authentication/login" -Body @{ email = $Email; password = $Password }
}
catch {
    Write-Error "Login failed for $Email. Is the backend running in Development on $BaseUrl, and does the account exist? $($_.Exception.Message)"
    exit 1
}
$token = $auth.token
if (-not $token) { Write-Error "Login returned no token."; exit 1 }
Write-Host "   ok (clientId=$($auth.clientId), name=$($auth.clientName))" -ForegroundColor Green

# --- 2. Ensure banks exist ---
$banks = Invoke-Api -Method GET -Path "/bankslist" -Token $token
if (-not $banks -or $banks.Count -eq 0) {
    Write-Host "-> No banks found; inserting defaults ..."
    Invoke-Api -Method POST -Path "/bankslist/addBanks" -Token $token -Body $DefaultBanks | Out-Null
    $banks = Invoke-Api -Method GET -Path "/bankslist" -Token $token
    Write-Host "   inserted $($banks.Count) banks" -ForegroundColor Green
}
else {
    Write-Host "-> Banks already present ($($banks.Count))" -ForegroundColor Green
}
if (-not ($banks.bankID -contains $BankId)) {
    Write-Warning "BankId $BankId not in the bank list; using the first available bank instead."
    $BankId = $banks[0].bankID
}

# --- 3. Link a connection (unless data already exists) ---
$accounts = Invoke-Api -Method GET -Path "/portfolio/accounts" -Token $token
if ($accounts.Count -gt 0 -and -not $Force) {
    Write-Host "-> Account already has $($accounts.Count) accounts; skipping link (use -Force to add more)." -ForegroundColor Yellow
}
else {
    Write-Host "-> Linking connection (bankId=$BankId) and syncing MockProvider snapshot ..."
    $result = Invoke-Api -Method POST -Path "/connections/exchange" -Token $token -Body @{ bankId = $BankId; publicToken = "mock-public-token" }
    Write-Host ("   synced: connectionId={0}, accounts={1}, holdings={2}, transactions={3}" -f `
        $result.connectionId, $result.accounts, $result.holdings, $result.transactions) -ForegroundColor Green
}

# --- Summary ---
$netWorth    = Invoke-Api -Method GET -Path "/portfolio/networth"    -Token $token
$holdings    = Invoke-Api -Method GET -Path "/portfolio/holdings"     -Token $token
$txns        = Invoke-Api -Method GET -Path "/portfolio/transactions" -Token $token
$accounts    = Invoke-Api -Method GET -Path "/portfolio/accounts"     -Token $token
Write-Host ""
Write-Host "Done. Current state for $Email :" -ForegroundColor Cyan
Write-Host ("   net worth : {0} {1}" -f $netWorth.netWorth, $netWorth.currency)
Write-Host ("   accounts  : {0}" -f $accounts.Count)
Write-Host ("   holdings  : {0}" -f $holdings.Count)
Write-Host ("   txns      : {0}" -f $txns.Count)
