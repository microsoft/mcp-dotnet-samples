using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using McpSamples.OnedriveDownload.HybridApp.Configurations;

var builder = WebApplication.CreateBuilder(args);

// 수동 endpoint 설정 없이 microsoft identity web 사용

builder.Services.Configure<OnedriveDownloadAppSettings>(builder.Configuration);

// Entra ID 인증 구성 추가
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("EntraId"));

// Microsoft login UI 추가
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

// HTTP 요청 파이프라인 구성
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection(); 
app.UseRouting();

// IMPORTANT: 인증 및 권한 부여 미들웨어 추가
app.UseAuthentication(); // 사용자 누구인지 식별하기
app.UseAuthorization(); // 사용자가 리소스에 액세스할 수 있는지 확인하기

app.MapRazorPages();
app.MapGet("/", () => "You are authenticated.").RequireAuthorization();

app.Run();
