$guid = [System.Guid]::NewGuid().ToString()
Write-Host "Generated GUID: $guid (Length: $($guid.Length))"

$body = @{
	accountId = 1
	amount = 25.00
	idempotencyKey = $guid
} | ConvertTo-Json

Write-Host "Request body:"
Write-Host $body

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5000/BankAccount/withdraw" -Method POST -ContentType "application/json" -Body $body
    Write-Host "Success response:"
    $response | ConvertTo-Json
} catch {
    Write-Host "Error occurred:"
    Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
    Write-Host "Status Description: $($_.Exception.Response.StatusDescription)"
    
    # Try to get the response body
    if ($_.Exception.Response) {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
        Write-Host "Error response body:"
        Write-Host $responseBody
    }
}