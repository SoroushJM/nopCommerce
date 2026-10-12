[CmdletBinding()]
param(
    [string]$MadadrangUrl = 'http://80.75.5.82:9011',
    [string]$BoutiqueUrl = 'http://80.75.5.82:9012',
    [string]$EvidencePath
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Use PowerShell 7.' }
$session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
$session.UserAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36'
$stores = [ordered]@{ madadrang = $MadadrangUrl.TrimEnd('/'); boutique = $BoutiqueUrl.TrimEnd('/') }
if (([uri]$MadadrangUrl).Host -ne ([uri]$BoutiqueUrl).Host) { throw 'This acceptance check requires both stores on one host.' }
$carts = @{}

# Issue both stores' tokens before either POST, using one fresh guest cookie jar.
foreach ($store in $stores.Keys) {
    $base = $stores[$store]
    $cart = Invoke-RestMethod -Uri ($base + '/cart') -Headers @{ 'X-Storefront-Cart' = '1' } -WebSession $session -TimeoutSec 30
    if ($cart.Count -ne 0 -or $cart.Lines.Count -ne 0) { throw "Refuse to update a populated $store guest cart." }
    if (!$cart.Token -or !$cart.TokenField -or !$cart.CartUrl) { throw "Missing $store native cart contract." }
    if ($cart.CheckoutFields.Count -ne 0) { throw "Refuse to change unexpected $store checkout fields." }
    $target = [uri]::new([uri]($base + '/'), $cart.CartUrl)
    if ($target.Authority -ne ([uri]$base).Authority -or $target.Scheme -ne ([uri]$base).Scheme) { throw 'Native cart action escaped its selected origin.' }
    $carts[$store] = @{ Cart = $cart; Target = $target.AbsoluteUri }
}

$records = @()
foreach ($store in $stores.Keys) {
    $entry = $carts[$store]
    $form = @{ updatecart = '1' }
    $form[$entry.Cart.TokenField] = $entry.Cart.Token
    $response = Invoke-WebRequest -Uri $entry.Target -Method Post -ContentType 'application/x-www-form-urlencoded' -Body $form -Headers @{ 'X-Storefront-Cart' = '1' } -WebSession $session -TimeoutSec 30
    if ($response.StatusCode -ne 200 -or $response.Headers['Content-Type'] -notlike '*application/json*') { throw "Invalid $store cart update response." }
    $after = $response.Content | ConvertFrom-Json
    if ($after.Count -ne 0 -or $after.Lines.Count -ne 0) { throw "The $store empty guest cart changed unexpectedly." }
    $records += [ordered]@{ Theme = $store; Status = 200; Json = $true; Before = 0; After = 0 }
}

$names = @($session.Cookies.GetAllCookies() | Select-Object -ExpandProperty Name -Unique)
foreach ($store in $stores.Keys) {
    foreach ($suffix in @('Customer', 'Antiforgery')) {
        if ($names -notcontains ".Nop.$store.$suffix") { throw "Missing isolated $store $suffix cookie." }
    }
}
if ($names -contains '.Nop.Customer' -or $names -contains '.Nop.Antiforgery') { throw 'Generic customer/antiforgery cookies remain active.' }
$proof = [ordered]@{ SameGuestCookieJar = $true; BothTokensIssuedBeforeBothPosts = $true; ProductAddsOrRemovals = 0; AdminCartWrites = 0; Records = $records; CookieNames = $names }
$json = $proof | ConvertTo-Json -Depth 5
if ($EvidencePath) { $json | Set-Content -LiteralPath $EvidencePath -Encoding utf8 }
$json
