param([string]$BaseUrl='http://localhost:5090')
$ErrorActionPreference='Stop'
function Assert($condition,$message){if(!$condition){throw $message};Write-Host "PASS: $message"}
function New-Client {
 $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
 $html=Invoke-WebRequest "$BaseUrl/stationery/cart" -WebSession $session
 $token=[regex]::Match($html.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value
 Assert ($token.Length -gt 20) 'Antiforgery token provided'
 return @{Session=$session;Token=$token}
}
function Post($client,$path,$data){Invoke-RestMethod "$BaseUrl/stationery/api/$path" -Method Post -WebSession $client.Session -Headers @{RequestVerificationToken=$client.Token} -ContentType 'application/json' -Body ($data|ConvertTo-Json -Depth 8 -Compress)}
function Refresh($client){$html=Invoke-WebRequest "$BaseUrl/stationery/cart" -WebSession $client.Session;$client.Token=[regex]::Match($html.Content,'name="__RequestVerificationToken"[^>]*value="([^"]+)"').Groups[1].Value}
function Login($client,$phone){$sent=Post $client 'otp/send' @{phone=$phone};Assert $sent.success 'Mock OTP sent';$again=Post $client 'otp/send' @{phone=$phone};Assert (!$again.success) 'OTP request cooldown enforced';$bad=Post $client 'otp/verify' @{phone=$phone;code='000000'};Assert (!$bad.success) 'Wrong OTP rejected';$ok=Post $client 'otp/verify' @{phone=$phone;code=$sent.testCode};Assert $ok.success 'OTP accepted';Refresh $client;$replay=Post $client 'otp/verify' @{phone=$phone;code=$sent.testCode};Assert (!$replay.success) 'Consumed OTP cannot be replayed'}
$a=New-Client
$catalog=Invoke-RestMethod "$BaseUrl/stationery/api/catalog" -WebSession $a.Session
$product=$catalog|Where-Object {$_.attributes.Count -gt 0 -and $_.available}|Select-Object -First 1
$attr=$product.attributes[0];$green=$attr.options|Where-Object name -eq 'سبز';$orange=$attr.options|Where-Object name -eq 'نارنجی'
$selection=@{productId=$product.id;values=@{[string]$attr.id=$green.id};quantity=1}
$quote=Post $a 'quote' $selection
Assert ($quote.available -and $quote.price -eq ($product.price+$green.adjustment)) 'Real option changes price'
$cart=Post $a 'cart/add' $selection
Assert ($cart.count -eq 1 -and !$cart.signedIn) 'Guest adds real cart line'
$unavailable=@{productId=$product.id;values=@{[string]$attr.id=$orange.id};quantity=1}
$q=Post $a 'quote' $unavailable;Assert (!$q.available) 'Zero-stock combination unavailable'
try{Post $a 'cart/add' $unavailable|Out-Null;throw 'Unavailable add incorrectly accepted'}catch{Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Zero-stock add rejected'}
$phone='0900'+(Get-Random -Minimum 1000000 -Maximum 9999999)
Login $a $phone
$cart=Invoke-RestMethod "$BaseUrl/stationery/api/cart" -WebSession $a.Session
Assert ($cart.signedIn -and $cart.count -eq 1) 'Login persists and guest cart migrates'
$b=New-Client
Post $b 'cart/add' $selection|Out-Null
Login $b $phone
$merged=Invoke-RestMethod "$BaseUrl/stationery/api/cart" -WebSession $b.Session
Assert ($merged.signedIn -and $merged.count -eq 2) 'Existing account cart merges with guest cart'
$outsider=New-Client
try{Post $outsider 'cart/quantity' @{lineId=$merged.lines[0].id;quantity=0}|Out-Null;throw 'Cross-customer update incorrectly accepted'}catch{Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Other customer cannot edit cart line'}
$changed=Post $b 'cart/quantity' @{lineId=$merged.lines[0].id;quantity=3};Assert ($changed.count -eq 3 -and $changed.total -eq 3*$quote.price) 'Quantity and real unit price agree'
$deleted=Post $b 'cart/quantity' @{lineId=$changed.lines[0].id;quantity=0};Assert ($deleted.count -eq 0) 'Cart removal succeeds'
try{Invoke-RestMethod "$BaseUrl/stationery/api/cart/add" -Method Post -WebSession $outsider.Session -Headers @{RequestVerificationToken=""} -ContentType 'application/json' -Body ($selection|ConvertTo-Json -Depth 8)|Out-Null;throw 'Missing antiforgery incorrectly accepted'}catch{Assert ($_.Exception.Response.StatusCode.value__ -eq 400) 'Missing antiforgery token rejected'}
$spec=Invoke-RestMethod "$BaseUrl/stationery/openapi/v1.json";Assert (@($spec.paths.PSObject.Properties).Count -ge 7) 'Swagger OpenAPI generated'
Assert ((Invoke-WebRequest "$BaseUrl/stationery/swagger").StatusCode -eq 200) 'Swagger UI serves'
Assert ((Invoke-WebRequest "$BaseUrl/stationery/scalar").StatusCode -eq 200) 'Scalar serves'
