param(
    [string]$BaseUrl = "http://localhost:5048",
    [int]$Requests = 200,
    [int]$WarmUp = 10,
    [string]$Label = "run",
    [string]$Email = "customer@showroom.local",
    [string]$Password = "Customer@12345",
    [string]$OutFile = ""
)

Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$client.BaseAddress = [Uri]$BaseUrl

$body = @{ email = $Email; password = $Password } | ConvertTo-Json
$content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, "application/json")
$login = $client.PostAsync("/api/auth/login", $content).Result
if (-not $login.IsSuccessStatusCode) { throw "Login failed: $($login.StatusCode)" }
$token = ($login.Content.ReadAsStringAsync().Result | ConvertFrom-Json).token
$client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $token)

$scenarios = [ordered]@{
    "Specification example (Chery, 2025, 80k-150k, by price)" = "/api/vehicles?brand=Chery&minPrice=80000&maxPrice=150000&year=2025&page=1&pageSize=10&sortBy=price&sortOrder=asc"
    "All available, sorted by price, page 1"                 = "/api/vehicles?sortBy=price&page=1&pageSize=20"
    "Brand + price range, page 1"                            = "/api/vehicles?brand=OMODA&minPrice=100000&maxPrice=120000&sortBy=price&pageSize=20"
    "Deep page (page 500)"                                   = "/api/vehicles?sortBy=price&page=500&pageSize=20"
    "Vehicle details by id"                                  = "/api/vehicles/12341"
}

$results = foreach ($name in $scenarios.Keys) {
    $url = $scenarios[$name]
    for ($i = 0; $i -lt $WarmUp; $i++) { [void]$client.GetAsync($url).Result }

    $times = New-Object System.Collections.Generic.List[double]
    for ($i = 0; $i -lt $Requests; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $response = $client.GetAsync($url).Result
        [void]$response.Content.ReadAsStringAsync().Result
        $sw.Stop()
        if (-not $response.IsSuccessStatusCode) { throw "$url returned $($response.StatusCode)" }
        $times.Add($sw.Elapsed.TotalMilliseconds)
    }

    $sorted = $times | Sort-Object
    [pscustomobject]@{
        Run      = $Label
        Scenario = $name
        AvgMs    = [math]::Round(($times | Measure-Object -Average).Average, 2)
        P50Ms    = [math]::Round($sorted[[int]($sorted.Count * 0.50)], 2)
        P95Ms    = [math]::Round($sorted[[int]($sorted.Count * 0.95) - 1], 2)
        MinMs    = [math]::Round($sorted[0], 2)
        MaxMs    = [math]::Round($sorted[-1], 2)
    }
}

$results | Format-Table -AutoSize
if ($OutFile) { $results | Export-Csv -Path $OutFile -NoTypeInformation -Append }
